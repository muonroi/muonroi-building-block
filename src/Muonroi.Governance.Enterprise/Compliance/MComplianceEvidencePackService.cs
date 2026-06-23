using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.ControlPlane;

namespace Muonroi.Governance.Compliance;

/// <summary>
/// Represents the MCompliance Evidence Pack Service.
/// </summary>
/// <remarks>
/// Packs are signed for tamper-evidence. When an <see cref="IMControlPlaneSigner"/> is registered,
/// signing uses asymmetric RSA chain-of-custody (verifiers only need the public key). Otherwise it
/// falls back to a local HMAC keyed on <c>LicenseConfigs.ProjectSeed</c>/<c>FingerprintSalt</c>;
/// the fallback fails closed when no key material is configured (no guessable default key).
/// </remarks>
public sealed class MComplianceEvidencePackService(
    LicenseConfigs licenseConfigs,
    IMComplianceExportService exportService,
    IHostEnvironment? hostEnvironment = null,
    IMControlPlaneSigner? signer = null) : IMComplianceEvidencePackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LicenseConfigs _licenseConfigs = MGuard.NotNull(licenseConfigs);
    private readonly IMComplianceExportService _exportService = MGuard.NotNull(exportService);
    private readonly IHostEnvironment? _hostEnvironment = hostEnvironment;
    private readonly IMControlPlaneSigner? _signer = signer;

    /// <summary>
    /// Executes the Generate Async operation.
    /// </summary>
    public async Task<MComplianceEvidencePackResult> GenerateAsync(
        MComplianceEvidencePackRequest request,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(request);
        if (!_exportService.IsEnabled)
        {
            throw new MInternalException("Compliance export is not enabled.");
        }

        MComplianceExportQuery filters = new()
        {
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            TenantId = request.TenantId,
            Source = request.Source,
            MaxRecords = request.MaxRecords > 0
                ? request.MaxRecords
                : _licenseConfigs.Compliance.MaxRecordsPerPack
        };

        IReadOnlyList<MComplianceExportRecord> records = await _exportService.GetExportRecordsAsync(filters, cancellationToken);
        MComplianceVerificationRequest verificationRequest = new()
        {
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            TenantId = request.TenantId,
            Source = request.Source
        };
        MComplianceVerificationResult verification = await _exportService.VerifyAsync(verificationRequest, cancellationToken);

        MComplianceEvidencePackSummary summary = new()
        {
            TotalRecords = records.Count,
            SourceCounts = records
                .GroupBy(x => x.Source.ToString())
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase),
            EventTypeCounts = records
                .GroupBy(x => x.EventType ?? string.Empty)
                .ToDictionary(x => x.Key, x => x.Count(), StringComparer.OrdinalIgnoreCase)
        };

        MComplianceEvidencePackDocument pack = new()
        {
            PackId = $"pack_{Guid.NewGuid().ToString("N")[..12]}",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Filters = filters,
            Summary = summary,
            Verification = verification,
            RootHash = records.LastOrDefault()?.RecordHash ?? "GENESIS",
            Records = request.IncludeRecords ? [.. records] : null
        };

        pack.PackHash = ComputePackHash(pack, records.Select(r => r.RecordHash).ToArray());
        (pack.Signature, pack.SignatureAlgorithm, pack.SigningKeyId) = SignPack(pack.PackHash);

        string outputPath = ResolveOutputPath(request.OutputPath, pack.PackId);
        string? folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string payload = JsonSerializer.Serialize(pack, new JsonSerializerOptions(JsonOptions) // MBB002-exempt: requires custom JsonOptions not available in wrapper
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(outputPath, payload, cancellationToken);

        _ = await _exportService.PruneEvidencePacksAsync(cancellationToken);

        return new MComplianceEvidencePackResult
        {
            OutputPath = outputPath,
            Pack = pack
        };
    }

    private string ResolveOutputPath(string? outputPath, string packId)
    {
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            if (Path.IsPathRooted(outputPath))
            {
                return outputPath;
            }

            return Path.GetFullPath(Path.Combine(ResolveEvidenceFolder(), outputPath));
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string fileName = $"evidence-pack-{now:yyyyMMdd-HHmmss}-{packId}.json";
        return Path.Combine(ResolveEvidenceFolder(), fileName);
    }

    private string ResolveEvidenceFolder()
    {
        MComplianceConfigs compliance = _licenseConfigs.Compliance;
        string root = compliance.ExportRootPath;
        if (!Path.IsPathRooted(root))
        {
            string basePath = !string.IsNullOrWhiteSpace(_hostEnvironment?.ContentRootPath)
                ? _hostEnvironment.ContentRootPath
                : AppContext.BaseDirectory;
            root = Path.GetFullPath(Path.Combine(basePath, root));
        }

        string folderName = string.IsNullOrWhiteSpace(compliance.EvidencePackFolderName)
            ? "evidence-packs"
            : compliance.EvidencePackFolderName.Trim();
        return Path.Combine(root, folderName);
    }

    /// <summary>
    /// Loads a persisted evidence pack and verifies its signature (always) and content hash
    /// (when records are embedded). See <see cref="IMComplianceEvidencePackService.VerifyAsync"/>.
    /// </summary>
    public async Task<MComplianceEvidencePackVerifyResult> VerifyAsync(
        string packFilePath,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(packFilePath);
        if (!File.Exists(packFilePath))
        {
            throw new MInternalException($"Evidence pack not found: {packFilePath}");
        }

        string payload = await File.ReadAllTextAsync(packFilePath, cancellationToken);
        MComplianceEvidencePackDocument? pack =
            JsonSerializer.Deserialize<MComplianceEvidencePackDocument>(payload, JsonOptions); // MBB002-exempt: custom JsonOptions not available in wrapper

        if (pack is null)
        {
            return new MComplianceEvidencePackVerifyResult
            {
                SignatureValid = false,
                ContentHashValid = false,
                Message = "Evidence pack could not be deserialized."
            };
        }

        // 1) Signature over the stored pack hash — authenticity + hash-tamper detection.
        bool signatureValid = VerifySignature(pack.SignatureAlgorithm, pack.PackHash, pack.Signature);

        // 2) Content integrity — only when records are embedded (otherwise cannot recompute the
        //    per-record hash component of the pack hash).
        bool? contentHashValid = null;
        if (pack.Records is not null)
        {
            string recomputed = ComputePackHash(pack, pack.Records.Select(r => r.RecordHash).ToArray());
            contentHashValid = string.Equals(recomputed, pack.PackHash, StringComparison.OrdinalIgnoreCase);
        }

        string message;
        if (!signatureValid)
        {
            message = "Signature verification failed — pack hash or signature was altered, or the verifying key does not match.";
        }
        else if (contentHashValid == false)
        {
            message = "Content hash mismatch — embedded records were altered after signing.";
        }
        else
        {
            message = string.Empty;
        }

        return new MComplianceEvidencePackVerifyResult
        {
            SignatureValid = signatureValid,
            ContentHashValid = contentHashValid,
            SignatureAlgorithm = pack.SignatureAlgorithm,
            Message = message
        };
    }

    private static string ComputePackHash(
        MComplianceEvidencePackDocument pack,
        IReadOnlyList<string> recordHashes)
    {
        string material = JsonSerializer.Serialize(new // MBB002-exempt: static helper with custom JsonOptions not available in wrapper
        {
            pack.PackId,
            pack.GeneratedAtUtc,
            pack.Filters,
            pack.Summary.TotalRecords,
            pack.RootHash,
            pack.Verification.IsValid,
            pack.Verification.CheckedCount,
            RecordHashes = recordHashes
        }, JsonOptions);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    /// <summary>
    /// Signs the pack hash. Prefers the asymmetric control-plane signer (RSA chain-of-custody);
    /// falls back to a local HMAC. Fails closed when neither a signer nor key material is present.
    /// </summary>
    private (string Signature, string Algorithm, string KeyId) SignPack(string packHash)
    {
        // Preferred: asymmetric chain-of-custody. Verifiers only need the public key, so packs are
        // non-repudiable across trust boundaries.
        if (_signer is not null)
        {
            return (_signer.Sign(packHash), _signer.SignatureAlgorithm, _signer.KeyId);
        }

        // Fallback: local HMAC. Fail closed if no real key material is configured — a guessable
        // default key would make signatures forgeable by anyone reading the OSS source.
        string keyMaterial = ResolveHmacKeyMaterial()
            ?? throw new MInternalException(
                "Compliance evidence-pack signing key is not configured: set LicenseConfigs.ProjectSeed " +
                "or FingerprintSalt, or register an IMControlPlaneSigner for RSA signing.");

        byte[] key = Encoding.UTF8.GetBytes(keyMaterial);
        using HMACSHA256 hmac = new(key);
        byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(packHash));
        return (Convert.ToHexString(signature), "HMACSHA256", string.Empty);
    }

    private bool VerifySignature(string algorithm, string packHash, string signature)
    {
        if (string.IsNullOrWhiteSpace(packHash) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        // RSA (or any non-HMAC) chain-of-custody requires the configured signer to verify.
        if (!string.Equals(algorithm, "HMACSHA256", StringComparison.OrdinalIgnoreCase))
        {
            return _signer is not null && _signer.Verify(packHash, signature);
        }

        // Local HMAC: recompute and constant-time compare.
        string? keyMaterial = ResolveHmacKeyMaterial();
        if (keyMaterial is null)
        {
            return false;
        }

        byte[] key = Encoding.UTF8.GetBytes(keyMaterial);
        using HMACSHA256 hmac = new(key);
        byte[] expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(packHash));

        byte[] actual;
        try
        {
            actual = Convert.FromHexString(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private string? ResolveHmacKeyMaterial()
    {
        if (!string.IsNullOrWhiteSpace(_licenseConfigs.ProjectSeed))
        {
            return _licenseConfigs.ProjectSeed;
        }

        return string.IsNullOrWhiteSpace(_licenseConfigs.FingerprintSalt)
            ? null
            : _licenseConfigs.FingerprintSalt;
    }
}
