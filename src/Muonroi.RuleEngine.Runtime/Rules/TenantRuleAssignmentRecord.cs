namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Records a ruleset assignment for a tenant.
/// </summary>
public sealed class TenantRuleAssignmentRecord
{
    /// <summary>Record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant identifier that owns the assignment.</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Target tenant identifier for the assignment.</summary>
    public string TargetTenantId { get; set; } = string.Empty;

    /// <summary>Workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Ruleset version assigned.</summary>
    public int Version { get; set; }

    /// <summary>Identifier of the user who assigned the rule.</summary>
    public string AssignedBy { get; set; } = "system";

    /// <summary>Timestamp of assignment.</summary>
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
}

