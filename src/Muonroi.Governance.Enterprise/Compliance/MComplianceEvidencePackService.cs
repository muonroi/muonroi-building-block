using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.Compliance;

public sealed class MComplianceEvidencePackService(
    LicenseConfigs licenseConfigs,
    IMComplianceExportService exportService,
    IHostEnvironment? hostEnvironment = null) : IMComplianceEvidencePackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly LicenseConfigs _licenseConfigs =
        licenseConfigs ?? throw new ArgumentNullException(nameof(licenseConfigs));
    private readonly IMComplianceExportService _exportService =
        exportService ?? throw new ArgumentNullException(nameof(exportService));
    private readonly IHostEnvironment? _hostEnvironment = hostEnvironment;

    public async Task<MComplianceEvidencePackResult> GenerateAsync(
        MComplianceEvidencePackRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_exportService.IsEnabled)
        {
            throw new InvalidOperationException("Compliance export is not enabled.");
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

        pack.PackHash = ComputePackHash(pack, records);
        pack.Signature = SignPack(pack.PackHash);

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

    private static string ComputePackHash(
        MComplianceEvidencePackDocument pack,
        IReadOnlyList<MComplianceExportRecord> records)
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
            RecordHashes = records.Select(x => x.RecordHash).ToArray()
        }, JsonOptions);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private string SignPack(string packHash)
    {
        string? keyMaterial = _licenseConfigs.ProjectSeed;
        if (string.IsNullOrWhiteSpace(keyMaterial))
        {
            keyMaterial = _licenseConfigs.FingerprintSalt;
        }

        if (string.IsNullOrWhiteSpace(keyMaterial))
        {
            keyMaterial = "muonroi-compliance-default-key";
        }

        byte[] key = Encoding.UTF8.GetBytes(keyMaterial);
        using HMACSHA256 hmac = new(key);
        byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(packHash));
        return Convert.ToHexString(signature);
    }
}
