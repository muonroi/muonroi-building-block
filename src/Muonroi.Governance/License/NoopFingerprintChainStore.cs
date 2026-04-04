namespace Muonroi.Governance.License;

/// <summary>
/// Represents the Noop Fingerprint Chain Store.
/// </summary>
public sealed class NoopFingerprintChainStore : IFingerprintChainStore
{
    /// <summary>
    /// Executes the Append operation.
    /// </summary>
    public void Append(FingerprintChainEntry entry) { }
    /// <summary>
    /// Executes the Get Last Signature operation.
    /// </summary>
    public string? GetLastSignature(string? tenantId = null) => null;
    /// <summary>
    /// Executes the Get Last Sequence operation.
    /// </summary>
    public long GetLastSequence(string? tenantId = null) => 0;
    /// <summary>
    /// Executes the Get Recent Entries operation.
    /// </summary>
    public IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null, string? tenantId = null) => [];
    /// <summary>
    /// Executes the Get Tenant Partitions operation.
    /// </summary>
    public IEnumerable<string> GetTenantPartitions() => [];
}
