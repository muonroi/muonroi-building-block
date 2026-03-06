namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleSetAuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string TenantId { get; set; } = "default";
    public string WorkflowName { get; set; } = string.Empty;
    public string? TargetTenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public int? Version { get; set; }
    public string? Actor { get; set; }
    public string? Detail { get; set; }
    public string? ContentHash { get; set; }
    public string? SignatureAlgorithm { get; set; }
    public string? SignatureKeyId { get; set; }
    public string? Signature { get; set; }
}
