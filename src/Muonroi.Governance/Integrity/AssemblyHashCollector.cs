namespace Muonroi.Governance.License;

internal sealed class AssemblyHashCollector : IAssemblyHashCollector
{
    private readonly object _sync = new();
    private IReadOnlyList<AssemblyManifestEntry>? _cache;

    public IReadOnlyList<AssemblyManifestEntry> Collect()
    {
        if (_cache != null)
        {
            return _cache;
        }

        lock (_sync)
        {
            if (_cache != null)
            {
                return _cache;
            }

            _cache = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.StartsWith("Muonroi.", StringComparison.Ordinal) == true)
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(BuildEntry)
                .OrderBy(x => x.AssemblyName, StringComparer.Ordinal)
                .ThenBy(x => x.Version, StringComparer.Ordinal)
                .ToList();

            return _cache;
        }
    }

    private static AssemblyManifestEntry BuildEntry(Assembly assembly)
    {
        AssemblyName name = assembly.GetName();
        byte[] bytes = File.ReadAllBytes(assembly.Location);
        string hash = Convert.ToBase64String(SHA256.HashData(bytes));
        byte[]? tokenBytes = name.GetPublicKeyToken();
        string? publicKeyToken = tokenBytes is { Length: > 0 }
            ? Convert.ToHexString(tokenBytes).ToLowerInvariant()
            : null;

        return new AssemblyManifestEntry
        {
            AssemblyName = name.Name ?? assembly.GetName().Name ?? "unknown",
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? name.Version?.ToString()
                ?? "0.0.0",
            Sha256Hash = hash,
            PublicKeyToken = publicKeyToken
        };
    }
}
