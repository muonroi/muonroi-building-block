namespace Muonroi.Governance.Abstractions.Integrity;

/// <summary>
/// Collects hashes for loaded Muonroi assemblies.
/// </summary>
public interface IAssemblyHashCollector
{
    /// <summary>
    /// Executes the Collect operation.
    /// </summary>
    IReadOnlyList<AssemblyManifestEntry> Collect();
}
