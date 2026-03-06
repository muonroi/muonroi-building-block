namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class TenantRuleAssignmentRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = "default";

    public string TargetTenantId { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public int Version { get; set; }

    public string AssignedBy { get; set; } = "system";

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

