namespace Muonroi.Governance.License;

public sealed class LicenseActionContext
{
    public string? ActionName { get; set; }
    public string? ActionType { get; set; }
    public string? PayloadHash { get; set; }
    public string? CorrelationId { get; set; }
    public string? TenantId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
