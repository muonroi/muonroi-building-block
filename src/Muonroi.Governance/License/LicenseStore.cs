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
