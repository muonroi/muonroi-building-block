namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Entity stored for a ruleset version.
/// </summary>
public sealed class RuleSetRecord
{
    /// <summary>Gets or sets the record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the tenant identifier.</summary>
    public string TenantId { get; set; } = "default";

    /// <summary>Gets or sets the workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the version number.</summary>
    public int Version { get; set; }

    /// <summary>Gets or sets the serialized ruleset JSON.</summary>
    public string Json { get; set; } = string.Empty;

    /// <summary>Gets or sets the lifecycle status.</summary>
    public RuleSetStatus Status { get; set; } = RuleSetStatus.Draft;

    /// <summary>Gets or sets a value indicating whether this version is active.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the creator identifier.</summary>
    public string CreatedBy { get; set; } = "system";

    /// <summary>Gets or sets the submitter identifier.</summary>
    public string? SubmittedBy { get; set; }

    /// <summary>Gets or sets the submission timestamp.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Gets or sets the approver identifier.</summary>
    public string? ApprovedBy { get; set; }

    /// <summary>Gets or sets the approval timestamp.</summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>Gets or sets the rejector identifier.</summary>
    public string? RejectedBy { get; set; }

    /// <summary>Gets or sets the rejection reason.</summary>
    public string? RejectedReason { get; set; }

    /// <summary>Gets or sets the creation timestamp.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the last update timestamp.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
