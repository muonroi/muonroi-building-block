namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Represents a canary rollout record for a ruleset version.
/// </summary>
public sealed class CanaryRolloutRecord
{
    /// <summary>Unique identifier for the rollout.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant that owns the control plane for this rollout.</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Workflow name for the ruleset.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Ruleset version under rollout.</summary>
    public int Version { get; set; }

    /// <summary>Explicit target tenant identifiers.</summary>
    public string[] TargetTenantIds { get; set; } = [];

    /// <summary>Percentage target for tenants, if using percentage rollout.</summary>
    public int? TargetPercentage { get; set; }

    /// <summary>Current rollout status.</summary>
    public CanaryStatus Status { get; set; } = CanaryStatus.Active;

    /// <summary>Timestamp when the rollout started.</summary>
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp when the rollout completed, if any.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Actor who initiated the rollout.</summary>
    public string StartedBy { get; set; } = "system";

    /// <summary>Actor who promoted the rollout, if any.</summary>
    public string? PromotedBy { get; set; }

    /// <summary>Actor who rolled back the rollout, if any.</summary>
    public string? RolledBackBy { get; set; }

    /// <summary>Reason for rollback, if any.</summary>
    public string? RollbackReason { get; set; }
}

