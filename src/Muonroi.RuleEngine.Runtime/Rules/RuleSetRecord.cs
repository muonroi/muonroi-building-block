namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed class RuleSetRecord
{
    public Guid Id { get; set; }

    public string TenantId { get; set; } = "default";

    public string WorkflowName { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Json { get; set; } = string.Empty;

    public RuleSetStatus Status { get; set; } = RuleSetStatus.Draft;

    public bool IsActive { get; set; }

    public string CreatedBy { get; set; } = "system";

    public string? SubmittedBy { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public string? RejectedBy { get; set; }

    public string? RejectedReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

