namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the MFile Control Plane Store.
/// </summary>
public sealed class MFileControlPlaneStore(string path, IMJsonSerializeService jsonSerializeService) : IMControlPlaneStore
{
    private readonly string _path = ResolvePath(path);

    /// <summary>
    /// Executes the Load operation.
    /// </summary>
    public MControlPlaneRegistry Load()
    {
        if (!File.Exists(_path))
        {
            return new MControlPlaneRegistry();
        }

        try
        {
            string json = File.ReadAllText(_path);
            MControlPlaneRegistry? registry = jsonSerializeService.Deserialize<MControlPlaneRegistry>(json);
            return registry ?? new MControlPlaneRegistry();
        }
        catch
        {
            return new MControlPlaneRegistry();
        }
    }

    /// <summary>
    /// Executes the Save operation.
    /// </summary>
    public void Save(MControlPlaneRegistry registry)
    {
        registry.UpdatedAtUtc = DateTimeOffset.UtcNow;
        string? folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string json = JsonSerializer.Serialize(registry, new JsonSerializerOptions
        {
            WriteIndented = true
        }); // MBB002-exempt: requires WriteIndented formatting for file-based control plane store not available in wrapper
        File.WriteAllText(_path, json);
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Control-plane registry path is required.", nameof(path));
        }

        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}


