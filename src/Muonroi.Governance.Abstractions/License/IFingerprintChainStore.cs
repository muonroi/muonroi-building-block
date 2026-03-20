namespace Muonroi.Governance.License;

/// <summary>
/// Represents the IFingerprint Chain Store.
/// </summary>
public interface IFingerprintChainStore
{
    /// <summary>
    /// Executes the Append operation.
    /// </summary>
    void Append(FingerprintChainEntry entry);
    /// <summary>
    /// Executes the Get Last Signature operation.
    /// </summary>
    string? GetLastSignature(string? tenantId = null);
    /// <summary>
    /// Executes the Get Last Sequence operation.
    /// </summary>
    long GetLastSequence(string? tenantId = null);
    
    /// <summary>
    /// Retrieves recent chain entries for server submission.
    /// </summary>
    /// <param name="count">Maximum number of entries to retrieve.</param>
    /// <param name="afterSequence">Only retrieve entries with sequence greater than this value.</param>
    /// <param name="tenantId">Tenant partition to read from.</param>
    IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null, string? tenantId = null);

    /// <summary>
    /// Gets all tenant partitions currently persisted in the chain store.
    /// </summary>
    IEnumerable<string> GetTenantPartitions();
}
