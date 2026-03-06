using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var stateLock = new object();
var chainAuditLog = new List<ChainAuditEntry>();
var nonceStore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

var registryPath = ResolvePath(
    builder.Environment.ContentRootPath,
    builder.Configuration["MockLicense:RegistryPath"] ?? Path.Combine("generated-licenses", "license-registry.json"));
var masterKeyId = builder.Configuration["MockLicense:MasterKeyId"] ?? "mock-master-local";
var allowUnknownKeys = builder.Configuration.GetValue<bool>("MockLicense:AllowUnknownKeys");

var licenseRegistry = LoadOrCreateRegistry(registryPath, masterKeyId);
var rsa = LoadOrGenerateRsaKey();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => new
{
    Status = "Healthy",
    Service = "Mock License Server",
    Version = "2.0.0",
    Timestamp = DateTimeOffset.UtcNow,
    MasterKeyId = licenseRegistry.MasterKeyId,
    ChildKeyCount = licenseRegistry.Keys.Count
});

app.MapGet("/api/v1/master", () =>
{
    lock (stateLock)
    {
        return Results.Ok(new
        {
            licenseRegistry.MasterKeyId,
            licenseRegistry.CreatedAtUtc,
            licenseRegistry.UpdatedAtUtc,
            ActiveKeys = licenseRegistry.Keys.Count(x => x.IsActive && !IsExpired(x)),
            TotalKeys = licenseRegistry.Keys.Count,
            RegistryPath = registryPath
        });
    }
});

app.MapGet("/api/v1/master/public-key", () =>
{
    var publicKeyPath = ResolvePath(builder.Environment.ContentRootPath, "server_public_key.pem");
    if (!File.Exists(publicKeyPath))
    {
        return Results.NotFound(new { Error = "Public key not found." });
    }

    var pem = File.ReadAllText(publicKeyPath);
    return Results.Text(pem, "text/plain");
});

app.MapGet("/api/v1/keys", () =>
{
    lock (stateLock)
    {
        var items = licenseRegistry.Keys
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.LicenseKey,
                x.LicenseId,
                x.OrganizationName,
                x.Tier,
                x.IsActive,
                x.CreatedAtUtc,
                x.ExpiresAtUtc,
                IsExpired = IsExpired(x),
                x.IssuedBy,
                x.RevokedAtUtc,
                x.RevokedReason
            })
            .ToList();

        return Results.Ok(new
        {
            licenseRegistry.MasterKeyId,
            Count = items.Count,
            Keys = items
        });
    }
});

app.MapPost("/api/v1/keys/generate", ([FromBody] GenerateChildKeyRequest request) =>
{
    try
    {
        var tier = NormalizeTier(request.Tier);
        var organization = string.IsNullOrWhiteSpace(request.OrganizationName)
            ? "Muonroi Local Test"
            : request.OrganizationName.Trim();
        var validDays = request.ValidDays.GetValueOrDefault(365);
        if (validDays <= 0) validDays = 365;

        ChildLicenseRecord childKey;
        lock (stateLock)
        {
            childKey = CreateChildKeyRecord(tier, organization, licenseRegistry.MasterKeyId, validDays, request.Notes);
            licenseRegistry.Keys.Add(childKey);
            SaveRegistry(registryPath, licenseRegistry);
        }

        return Results.Ok(new
        {
            Message = "Child key generated successfully.",
            ChildKey = childKey
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/v1/keys/import", ([FromBody] ImportChildKeysRequest request) =>
{
    try
    {
        var incoming = request.Keys ?? new List<ImportChildKeyRequest>();
        if (incoming.Count == 0)
        {
            return Results.BadRequest(new { Error = "No keys to import." });
        }

        int imported = 0;
        int skipped = 0;

        lock (stateLock)
        {
            foreach (var item in incoming)
            {
                if (string.IsNullOrWhiteSpace(item.LicenseKey))
                {
                    skipped++;
                    continue;
                }

                var key = item.LicenseKey.Trim();
                var existing = licenseRegistry.Keys.FirstOrDefault(x =>
                    x.LicenseKey.Equals(key, StringComparison.OrdinalIgnoreCase));

                if (existing != null && !request.ReplaceExisting)
                {
                    skipped++;
                    continue;
                }

                var tier = NormalizeTier(item.Tier);
                var normalized = new ChildLicenseRecord
                {
                    LicenseKey = key,
                    LicenseId = string.IsNullOrWhiteSpace(item.LicenseId)
                        ? $"lic_{Guid.NewGuid().ToString("N")[..12]}"
                        : item.LicenseId.Trim(),
                    OrganizationName = string.IsNullOrWhiteSpace(item.OrganizationName)
                        ? "Muonroi Local Test"
                        : item.OrganizationName.Trim(),
                    Tier = tier,
                    IsActive = item.IsActive,
                    CreatedAtUtc = item.CreatedAtUtc ?? DateTimeOffset.UtcNow,
                    ExpiresAtUtc = item.ExpiresAtUtc ?? DateTimeOffset.UtcNow.AddYears(1),
                    IssuedBy = string.IsNullOrWhiteSpace(item.IssuedBy) ? licenseRegistry.MasterKeyId : item.IssuedBy.Trim(),
                    Notes = item.Notes,
                    RevokedAtUtc = item.RevokedAtUtc,
                    RevokedReason = item.RevokedReason
                };

                if (existing != null)
                {
                    licenseRegistry.Keys.Remove(existing);
                }

                licenseRegistry.Keys.Add(normalized);
                imported++;
            }

            SaveRegistry(registryPath, licenseRegistry);
        }

        return Results.Ok(new
        {
            Message = "Import completed.",
            Imported = imported,
            Skipped = skipped
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Json(new { Error = ex.Message }, statusCode: 500);
    }
});

app.MapPost("/api/v1/keys/revoke", ([FromBody] RevokeChildKeyRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.LicenseKey))
    {
        return Results.BadRequest(new { Error = "LicenseKey is required." });
    }

    lock (stateLock)
    {
        var key = request.LicenseKey.Trim();
        var existing = licenseRegistry.Keys.FirstOrDefault(x =>
            x.LicenseKey.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            return Results.NotFound(new { Error = "License key not found." });
        }

        existing.IsActive = false;
        existing.RevokedAtUtc = DateTimeOffset.UtcNow;
        existing.RevokedReason = string.IsNullOrWhiteSpace(request.Reason) ? "Revoked by admin" : request.Reason.Trim();
        SaveRegistry(registryPath, licenseRegistry);

        return Results.Ok(new
        {
            Message = "Child key revoked.",
            existing.LicenseKey,
            existing.RevokedAtUtc,
            existing.RevokedReason
        });
    }
});

app.MapPost("/api/v1/activate", ([FromBody] LicenseActivationRequest request) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.LicenseKey))
        {
            return Results.BadRequest(new LicenseActivationResponse(
                Success: false,
                Error: "License key is required.",
                Proof: null,
                Message: null
            ));
        }

        var inputKey = request.LicenseKey.Trim();
        ChildLicenseRecord? childKey;

        lock (stateLock)
        {
            childKey = licenseRegistry.Keys.FirstOrDefault(x =>
                x.LicenseKey.Equals(inputKey, StringComparison.OrdinalIgnoreCase));
        }

        if (childKey == null)
        {
            if (!allowUnknownKeys)
            {
                return Results.Json(new LicenseActivationResponse(
                    Success: false,
                    Error: "License key is not registered on master server.",
                    Proof: null,
                    Message: null
                ), statusCode: 401);
            }

            var fallbackTier = DetermineTierFromKey(inputKey);
            childKey = new ChildLicenseRecord
            {
                LicenseKey = inputKey,
                LicenseId = $"lic_{Guid.NewGuid().ToString("N")[..12]}",
                OrganizationName = ExtractOrganizationFromKey(inputKey),
                Tier = fallbackTier,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddYears(1),
                IssuedBy = "legacy-fallback"
            };
        }

        if (!childKey.IsActive)
        {
            return Results.Json(new LicenseActivationResponse(
                Success: false,
                Error: "License key is revoked.",
                Proof: null,
                Message: null
            ), statusCode: 403);
        }

        if (IsExpired(childKey))
        {
            return Results.Json(new LicenseActivationResponse(
                Success: false,
                Error: "License key is expired.",
                Proof: null,
                Message: null
            ), statusCode: 403);
        }

        var proof = new LicenseActivationProof
        {
            ProofId = Guid.NewGuid().ToString(),
            LicenseId = childKey.LicenseId,
            OrganizationName = childKey.OrganizationName,
            Tier = childKey.Tier,
            ActivatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = childKey.ExpiresAtUtc ?? DateTimeOffset.UtcNow.AddYears(1),
            ActivatedEnvironment = request.Environment ?? "Unknown",
            MaxSeats = childKey.Tier == "Enterprise" ? 1000 : childKey.Tier == "Licensed" ? 100 : 10,
            Features = GetFeaturesForTier(childKey.Tier),
            MachineFingerprint = request.MachineFingerprint,
            ProductVersion = request.ProductVersion
        };

        proof.Signature = SignProofResponse(rsa, proof.GetSigningData());

        return Results.Ok(new LicenseActivationResponse(
            Success: true,
            Error: null,
            Proof: proof,
            Message: $"License activated successfully. Valid until {proof.ExpiresAt:yyyy-MM-dd}."
        ));
    }
    catch (Exception ex)
    {
        return Results.Json(new LicenseActivationResponse(
            Success: false,
            Error: ex.Message,
            Proof: null,
            Message: null
        ), statusCode: 500);
    }
});

app.MapPost("/api/v1/chain/submit", ([FromBody] ChainSubmissionRequest request) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.LicenseId))
        {
            return Results.BadRequest(new ChainSubmissionResponse(
                Accepted: false,
                NewNonce: null,
                Error: "LicenseId is required."
            ));
        }

        if (request.Entries == null || request.Entries.Count == 0)
        {
            return Results.BadRequest(new ChainSubmissionResponse(
                Accepted: false,
                NewNonce: null,
                Error: "No entries to submit."
            ));
        }

        var normalizedTenant = NormalizeTenantId(request.TenantId, request.Entries);
        var nonceKey = BuildNonceKey(request.LicenseId, normalizedTenant);

        lock (stateLock)
        {
            chainAuditLog.Add(new ChainAuditEntry
            {
                LicenseId = request.LicenseId,
                TenantId = normalizedTenant,
                ReceivedAt = DateTime.UtcNow,
                EntryCount = request.Entries.Count,
                Entries = request.Entries
            });
        }

        var newNonce = Guid.NewGuid().ToString("N");
        lock (stateLock)
        {
            nonceStore[nonceKey] = newNonce;
        }

        var signature = SignResponse(rsa, true, newNonce, request.LicenseId);
        return Results.Ok(new ChainSubmissionResponse(
            Accepted: true,
            NewNonce: newNonce,
            Error: null,
            Signature: signature
        ));
    }
    catch (Exception ex)
    {
        return Results.Json(new ChainSubmissionResponse(
            Accepted: false,
            NewNonce: null,
            Error: ex.Message
        ), statusCode: 500);
    }
});

app.MapGet("/api/v1/audit", (string? tenantId) =>
{
    lock (stateLock)
    {
        var normalizedTenant = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
        var filtered = chainAuditLog.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(normalizedTenant))
        {
            filtered = filtered.Where(x =>
                string.Equals(x.TenantId, normalizedTenant, StringComparison.OrdinalIgnoreCase));
        }

        var materialized = filtered.OrderByDescending(x => x.ReceivedAt).Take(20).ToList();
        return Results.Ok(new
        {
            TotalSubmissions = materialized.Count,
            TenantId = normalizedTenant,
            Submissions = materialized
        });
    }
});

app.MapGet("/api/v1/nonce/{licenseId}", (string licenseId, string? tenantId) =>
{
    lock (stateLock)
    {
        var normalizedTenant = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
        var nonceKey = BuildNonceKey(licenseId, normalizedTenant);
        if (nonceStore.TryGetValue(nonceKey, out var nonce))
        {
            return Results.Ok(new { LicenseId = licenseId, TenantId = normalizedTenant, Nonce = nonce });
        }
    }

    return Results.NotFound(new { Error = "License not found." });
});

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       MUONROI MOCK LICENSE SERVER (MASTER/CHILD MODE)           ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine($"  Master Key Id : {licenseRegistry.MasterKeyId}");
Console.WriteLine($"  Registry Path : {registryPath}");
Console.WriteLine($"  Child Keys    : {licenseRegistry.Keys.Count}");
Console.WriteLine();
Console.WriteLine("  Endpoints:");
Console.WriteLine("    GET  /health");
Console.WriteLine("    GET  /api/v1/master");
Console.WriteLine("    GET  /api/v1/master/public-key");
Console.WriteLine("    GET  /api/v1/keys");
Console.WriteLine("    POST /api/v1/keys/generate");
Console.WriteLine("    POST /api/v1/keys/import");
Console.WriteLine("    POST /api/v1/keys/revoke");
Console.WriteLine("    POST /api/v1/activate");
Console.WriteLine("    POST /api/v1/chain/submit");
Console.WriteLine("    GET  /api/v1/audit");
Console.WriteLine("    GET  /api/v1/nonce/{id}");
Console.WriteLine();

app.Run();

static ChildLicenseRecord CreateChildKeyRecord(
    string tier,
    string organization,
    string masterKeyId,
    int validDays,
    string? notes)
{
    return new ChildLicenseRecord
    {
        LicenseKey = GenerateLicenseKey(tier, organization),
        LicenseId = $"lic_{Guid.NewGuid().ToString("N")[..12]}",
        OrganizationName = organization,
        Tier = tier,
        IsActive = true,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(validDays),
        IssuedBy = masterKeyId,
        Notes = notes
    };
}

static string GenerateLicenseKey(string tier, string organization)
{
    var prefix = tier == "Enterprise" ? "ENT" : tier == "Licensed" ? "LIC" : "FREE";
    var dateToken = DateTime.UtcNow.ToString("yyyyMMdd");
    return $"{prefix}-{dateToken}-{NewRandomSegment(4)}-{NewRandomSegment(4)}|{organization}";
}

static string NewRandomSegment(int length)
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    var buffer = new char[length];

    for (int i = 0; i < length; i++)
    {
        var index = RandomNumberGenerator.GetInt32(chars.Length);
        buffer[i] = chars[index];
    }

    return new string(buffer);
}

static bool IsExpired(ChildLicenseRecord record)
{
    return record.ExpiresAtUtc.HasValue && record.ExpiresAtUtc.Value < DateTimeOffset.UtcNow;
}

static LicenseRegistry LoadOrCreateRegistry(string path, string fallbackMasterKeyId)
{
    if (File.Exists(path))
    {
        try
        {
            var json = File.ReadAllText(path);
            var existing = JsonSerializer.Deserialize<LicenseRegistry>(json);
            if (existing != null)
            {
                existing.Keys ??= new List<ChildLicenseRecord>();
                if (string.IsNullOrWhiteSpace(existing.MasterKeyId))
                {
                    existing.MasterKeyId = fallbackMasterKeyId;
                }
                if (existing.CreatedAtUtc == default)
                {
                    existing.CreatedAtUtc = DateTimeOffset.UtcNow;
                }
                if (existing.UpdatedAtUtc == default)
                {
                    existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
                }
                return existing;
            }
        }
        catch
        {
            // ignored - will initialize a new registry
        }
    }

    var registry = new LicenseRegistry
    {
        MasterKeyId = fallbackMasterKeyId,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow,
        Keys = new List<ChildLicenseRecord>()
    };

    SaveRegistry(path, registry);
    return registry;
}

static void SaveRegistry(string path, LicenseRegistry registry)
{
    registry.UpdatedAtUtc = DateTimeOffset.UtcNow;
    var folder = Path.GetDirectoryName(path);
    if (!string.IsNullOrWhiteSpace(folder))
    {
        Directory.CreateDirectory(folder);
    }

    var json = JsonSerializer.Serialize(registry, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(path, json);
}

static string ResolvePath(string rootPath, string path)
{
    if (Path.IsPathRooted(path))
    {
        return path;
    }
    return Path.GetFullPath(Path.Combine(rootPath, path));
}

static RSA LoadOrGenerateRsaKey()
{
    var privateKeyPath = "server_private_key.pem";
    var publicKeyPath = "server_public_key.pem";

    if (File.Exists(privateKeyPath))
    {
        try
        {
            var pemContent = File.ReadAllText(privateKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(pemContent.ToCharArray());
            return rsa;
        }
        catch
        {
            // ignored - will generate new key
        }
    }

    var newRsa = RSA.Create(2048);
    File.WriteAllText(privateKeyPath, newRsa.ExportRSAPrivateKeyPem());
    File.WriteAllText(publicKeyPath, newRsa.ExportRSAPublicKeyPem());
    return newRsa;
}

static string SignResponse(RSA rsa, bool accepted, string? newNonce, string licenseId)
{
    var signingData = $"{accepted}|{newNonce ?? ""}|{licenseId}";
    var dataBytes = Encoding.UTF8.GetBytes(signingData);
    var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return Convert.ToBase64String(signature);
}

static string SignProofResponse(RSA rsa, string signingData)
{
    var dataBytes = Encoding.UTF8.GetBytes(signingData);
    var signature = rsa.SignData(dataBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    return Convert.ToBase64String(signature);
}

static string NormalizeTier(string? tier)
{
    if (string.IsNullOrWhiteSpace(tier))
    {
        return "Licensed";
    }

    return tier.Trim().ToLowerInvariant() switch
    {
        "free" => "Free",
        "paid" => "Licensed",
        "licensed" => "Licensed",
        "pro" => "Licensed",
        "enterprise" => "Enterprise",
        "ent" => "Enterprise",
        _ => throw new ArgumentException($"Unsupported tier '{tier}'. Supported values: paid, licensed, enterprise.")
    };
}

static string DetermineTierFromKey(string licenseKey)
{
    if (licenseKey.StartsWith("ENT-", StringComparison.OrdinalIgnoreCase) ||
        licenseKey.Contains("ENTERPRISE", StringComparison.OrdinalIgnoreCase))
    {
        return "Enterprise";
    }

    if (licenseKey.StartsWith("LIC-", StringComparison.OrdinalIgnoreCase) ||
        licenseKey.Contains("LICENSE", StringComparison.OrdinalIgnoreCase))
    {
        return "Licensed";
    }

    return "Free";
}

static string ExtractOrganizationFromKey(string licenseKey)
{
    if (licenseKey.Contains("|", StringComparison.Ordinal))
    {
        var parts = licenseKey.Split('|');
        if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            return parts[1].Trim();
        }
    }

    return "Unknown Organization";
}

static string NormalizeTenantId(string? requestTenantId, IEnumerable<ChainEntry> entries)
{
    if (!string.IsNullOrWhiteSpace(requestTenantId))
    {
        return requestTenantId.Trim();
    }

    return entries
               .Select(x => x.TenantId)
               .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
               ?.Trim() ?? "__host__";
}

static string BuildNonceKey(string licenseId, string? tenantId)
{
    var normalizedTenant = string.IsNullOrWhiteSpace(tenantId) ? "__host__" : tenantId.Trim();
    return $"{licenseId}:{normalizedTenant}";
}

static string[] GetFeaturesForTier(string tier)
{
    return tier switch
    {
        "Enterprise" => new[] { "*" },
        "Licensed" => new[]
        {
            "multi-tenant",
            "advanced-auth",
            "rule-engine",
            "grpc",
            "message-bus",
            "distributed-cache",
            "audit-trail",
            "anti-tampering"
        },
        _ => new[] { "db.query", "db.save", "http.request" }
    };
}

record GenerateChildKeyRequest(
    string? Tier,
    string? OrganizationName,
    int? ValidDays,
    string? Notes
);

record ImportChildKeysRequest(
    List<ImportChildKeyRequest>? Keys,
    bool ReplaceExisting
);

record ImportChildKeyRequest(
    string? LicenseKey,
    string? LicenseId,
    string? OrganizationName,
    string? Tier,
    bool IsActive,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    string? IssuedBy,
    string? Notes,
    DateTimeOffset? RevokedAtUtc,
    string? RevokedReason
);

record RevokeChildKeyRequest(
    string? LicenseKey,
    string? Reason
);

record LicenseRegistry
{
    public string MasterKeyId { get; set; } = "mock-master-local";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<ChildLicenseRecord> Keys { get; set; } = new();
}

record ChildLicenseRecord
{
    public string LicenseKey { get; set; } = "";
    public string LicenseId { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string Tier { get; set; } = "Licensed";
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public string IssuedBy { get; set; } = "mock-master-local";
    public string? Notes { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
}

record ChainSubmissionRequest(
    string LicenseId,
    string? TenantId,
    List<ChainEntry> Entries,
    DateTime SubmittedAt
);

record ChainEntry(
    long Sequence,
    string? TenantId,
    string ActionType,
    string? ActionName,
    string Signature,
    DateTime Timestamp
);

record ChainSubmissionResponse(
    bool Accepted,
    string? NewNonce,
    string? Error,
    string? Signature = null
);

record ChainAuditEntry
{
    public string LicenseId { get; init; } = "";
    public string? TenantId { get; init; }
    public DateTime ReceivedAt { get; init; }
    public int EntryCount { get; init; }
    public List<ChainEntry> Entries { get; init; } = new();
}

record LicenseActivationRequest(
    string? LicenseKey,
    string? MachineFingerprint,
    string? ProductVersion,
    DateTimeOffset ActivationTime,
    string? Environment
);

record LicenseActivationResponse(
    bool Success,
    string? Error,
    LicenseActivationProof? Proof,
    string? Message
);

record LicenseActivationProof
{
    public string ProofId { get; set; } = "";
    public string LicenseId { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string Tier { get; set; } = "";
    public DateTimeOffset ActivatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string ActivatedEnvironment { get; set; } = "";
    public int MaxSeats { get; set; }
    public string[] Features { get; set; } = Array.Empty<string>();
    public string? MachineFingerprint { get; set; }
    public string? ProductVersion { get; set; }
    public string Signature { get; set; } = "";

    public string GetSigningData()
    {
        return $"{ProofId}|{LicenseId}|{OrganizationName}|{Tier}|{ActivatedAt:O}|{ExpiresAt:O}|{ActivatedEnvironment}|{MaxSeats}|{string.Join(",", Features ?? Array.Empty<string>())}";
    }
}
