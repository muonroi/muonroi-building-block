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
                return jsonSerializeService.Deserialize<LicensePayload>(content);
            }
            catch { }
        }

        // 2. Fallback: try ActivationProofPath and extract SignedLicensePayload
        string? proofPath = ResolvePath(configs.ActivationProofPath, environment);
        if (string.IsNullOrWhiteSpace(proofPath) || !File.Exists(proofPath))
        {
            return null;
        }

        try
        {
            string proofContent = File.ReadAllText(proofPath);
            using JsonDocument doc = JsonDocument.Parse(proofContent);
            // Try camelCase "signedLicensePayload" first, then PascalCase
            if (doc.RootElement.TryGetProperty("signedLicensePayload", out JsonElement payloadEl) ||
                doc.RootElement.TryGetProperty("SignedLicensePayload", out payloadEl))
            {
                string payloadJson = payloadEl.GetRawText();
                // MBB002-exempt: activation proof boundary — format conversion from camelCase activation proof to LicensePayload
                return JsonSerializer.Deserialize<LicensePayload>(payloadJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
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
