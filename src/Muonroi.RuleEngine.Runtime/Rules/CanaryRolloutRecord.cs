namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class CanaryRolloutRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = "default";

    public string WorkflowName { get; set; } = string.Empty;

    public int Version { get; set; }

    public string[] TargetTenantIds { get; set; } = [];

    public int? TargetPercentage { get; set; }

    public CanaryStatus Status { get; set; } = CanaryStatus.Active;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public string StartedBy { get; set; } = "system";

    public string? PromotedBy { get; set; }

    public string? RolledBackBy { get; set; }

    public string? RollbackReason { get; set; }
}

