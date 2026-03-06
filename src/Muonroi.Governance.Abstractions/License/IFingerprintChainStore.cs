namespace Muonroi.Governance.License;

public interface IFingerprintChainStore
{
    void Append(FingerprintChainEntry entry);
    string? GetLastSignature(string? tenantId = null);
    long GetLastSequence(string? tenantId = null);
    
    /// <summary>
    /// Retrieves recent chain entries for server submission.
    /// </summary>
    /// <param name="count">Maximum number of entries to retrieve.</param>
    /// <param name="afterSequence">Only retrieve entries with sequence greater than this value.</param>
    IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null, string? tenantId = null);

    /// <summary>
    /// Gets all tenant partitions currently persisted in the chain store.
    /// </summary>
    IEnumerable<string> GetTenantPartitions();
}
