namespace Muonroi.Governance.Policy;

/// <summary>
/// Represents the File Policy Store.
/// </summary>
public sealed class FilePolicyStore(LicenseConfigs configs, IHostEnvironment? environment, IMJsonSerializeService jsonSerializeService) : IPolicyStore
{
    /// <summary>
    /// Executes the Load operation.
    /// </summary>
    public LicensePolicy? Load()
    {
        string? path = ResolvePath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

        try
        {
            string json = File.ReadAllText(path);
            return jsonSerializeService.Deserialize<LicensePolicy>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executes the Save operation.
    /// </summary>
    public void Save(LicensePolicy policy)
    {
        string? path = ResolvePath();
        if (string.IsNullOrWhiteSpace(path)) return;

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        string json = jsonSerializeService.Serialize(policy);
        File.WriteAllText(path, json);
    }

    private string? ResolvePath()
    {
        string? path = configs.PolicyFilePath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (Path.IsPathRooted(path)) return path;

        string root = !string.IsNullOrWhiteSpace(environment?.ContentRootPath)
            ? environment.ContentRootPath
            : AppContext.BaseDirectory;

        return Path.GetFullPath(Path.Combine(root, path));
    }
}
