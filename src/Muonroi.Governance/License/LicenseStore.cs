using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Governance.License;

public sealed class LicenseStore(
    IHostEnvironment? environment,
    LicenseConfigs configs,
    IMJsonSerializeService jsonSerializeService) : ILicenseStore
{
    private static readonly JsonSerializerOptions _cachedJsonOptions = new() { WriteIndented = true };

    public LicensePayload? Load()
    {
        // 1. Try primary LicenseFilePath
        string? path = ResolvePath(configs.LicenseFilePath, environment);
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                string content = File.ReadAllText(path);
                LicensePayload? payload = jsonSerializeService.Deserialize<LicensePayload>(content);
                // Skip if file is raw key format ({"LicenseKey":"MRR-..."}) — not a signed payload
                if (payload != null && !string.IsNullOrWhiteSpace(payload.LicenseId))
                {
                    return payload;
                }
            }
            catch { }
        }

        // 2. Fallback to activation proof
        ActivationProof? proof = LoadActivationProof();
        return proof?.SignedLicensePayload;
    }

    public ActivationProof? LoadActivationProof()
    {
        string? proofPath = ResolvePath(configs.ActivationProofPath, environment);
        if (string.IsNullOrWhiteSpace(proofPath) || !File.Exists(proofPath))
        {
            return null;
        }

        try
        {
            string proofContent = File.ReadAllText(proofPath);
            // MBB002-exempt: activation proof boundary — tolerant parsing for old/new proof casing
            return JsonSerializer.Deserialize<ActivationProof>(proofContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { }

        return null;
    }

    public void Save(LicensePayload payload)
    {
        string? path = ResolvePath(configs.LicenseFilePath, environment);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = jsonSerializeService.Serialize(payload);

        File.WriteAllText(path, json);
    }

    public void SaveActivationProof(ActivationProof proof)
    {
        string? path = ResolvePath(configs.ActivationProofPath, environment);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = jsonSerializeService.Serialize(proof);
        File.WriteAllText(path, json);
    }

    public string? LoadActivationJwt()
    {
        string? jwtPath = ResolvePath(configs.ActivationJwtPath, environment);
        if (string.IsNullOrWhiteSpace(jwtPath) || !File.Exists(jwtPath))
        {
            return null;
        }

        try
        {
            return File.ReadAllText(jwtPath).Trim();
        }
        catch
        {
            return null;
        }
    }

    public void SaveActivationJwt(string jwt)
    {
        string? path = ResolvePath(configs.ActivationJwtPath, environment);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, jwt);
    }

    public string? LoadPublicKeyPem()
    {
        // Look for public key alongside the JWT file: licenses/dev_license_public.pem or public_key.pem
        string? jwtPath = ResolvePath(configs.ActivationJwtPath, environment);
        if (string.IsNullOrWhiteSpace(jwtPath))
        {
            return null;
        }

        string? dir = Path.GetDirectoryName(jwtPath);
        if (string.IsNullOrWhiteSpace(dir))
        {
            return null;
        }

        // Try dev key first, then standard key
        foreach (string keyFile in new[] { "dev_license_public.pem", "public_key.pem" })
        {
            string keyPath = Path.Combine(dir, keyFile);
            if (File.Exists(keyPath))
            {
                try
                {
                    return File.ReadAllText(keyPath).Trim();
                }
                catch
                {
                    // Continue to next candidate
                }
            }
        }

        return null;
    }

    private static string? ResolvePath(string? path, IHostEnvironment? environment)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string root = !string.IsNullOrWhiteSpace(environment?.ContentRootPath)
            ? environment.ContentRootPath
            : AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, path));
    }
}
