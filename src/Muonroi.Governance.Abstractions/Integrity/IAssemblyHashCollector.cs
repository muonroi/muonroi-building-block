namespace Muonroi.Governance.Abstractions.Integrity;

/// <summary>
/// Collects hashes for loaded Muonroi assemblies.
/// </summary>
public interface IAssemblyHashCollector
{
    IReadOnlyList<AssemblyManifestEntry> Collect();
}
