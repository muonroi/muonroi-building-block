namespace Muonroi.Governance.License;

public sealed class NoopFingerprintChainStore : IFingerprintChainStore
{
    public void Append(FingerprintChainEntry entry) { }
    public string? GetLastSignature(string? tenantId = null) => null;
    public long GetLastSequence(string? tenantId = null) => 0;
    public IEnumerable<FingerprintChainEntry> GetRecentEntries(int count, long? afterSequence = null, string? tenantId = null) => [];
    public IEnumerable<string> GetTenantPartitions() => [];
}
