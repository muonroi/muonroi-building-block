namespace Muonroi.Governance.License;

public sealed class FingerprintChainEntry
{
    public long Sequence { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? TenantId { get; set; }
    public string? ActionType { get; set; }
    public string? ActionName { get; set; }
    public string? PayloadHash { get; set; }
    public string? PreviousSignature { get; set; }
    public string? Signature { get; set; }
}
