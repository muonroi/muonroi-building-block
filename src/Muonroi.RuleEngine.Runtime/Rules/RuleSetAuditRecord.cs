namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleSetAuditRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = "default";

    public string WorkflowName { get; set; } = string.Empty;

    public string? TargetTenantId { get; set; }

    public int? Version { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Actor { get; set; } = "system";

    public string? Detail { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public string ContentHash { get; set; } = string.Empty;

    public string SignatureAlgorithm { get; set; } = string.Empty;

    public string SignatureKeyId { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;
}
