namespace Muonroi.Governance.License;

public sealed class LicenseStore(
    IHostEnvironment? environment,
    LicenseConfigs configs,
    IMJsonSerializeService jsonSerializeService) : ILicenseStore
{
    private static readonly JsonSerializerOptions _cachedJsonOptions = new() { WriteIndented = true };

    public LicensePayload? Load()
    {
        string? path = ResolvePath(configs.LicenseFilePath, environment);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            string content = File.ReadAllText(path);

            return jsonSerializeService.Deserialize<LicensePayload>(content);
        }
        catch
        {
            return null;
        }
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
