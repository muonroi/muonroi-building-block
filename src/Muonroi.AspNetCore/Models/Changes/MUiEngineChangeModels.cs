namespace Muonroi.AspNetCore.Models.Changes;

/// <summary>
/// Represents a request to change the order of rules for a specific endpoint.
/// </summary>
public sealed class RuleOrderChangeRequest
{
    /// <summary>
    /// Gets or sets the endpoint route the rules are associated with.
    /// </summary>
    public string EndpointRoute { get; set; } = "/";

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the ordered list of rule codes.
    /// </summary>
    public List<string> OrderedRuleCodes { get; set; } = [];
}

/// <summary>
/// Represents the result of validating a rule order change request.
/// </summary>
public sealed class RuleOrderChangeValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the change is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Represents a historical record of a rule change.
/// </summary>
public sealed class RuleChangeRecord
{
    /// <summary>
    /// Gets or sets the unique identifier for the change.
    /// </summary>
    public Guid ChangeId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint route.
    /// </summary>
    public string EndpointRoute { get; set; } = "/";

    /// <summary>
    /// Gets or sets the rule order before the change.
    /// </summary>
    public List<string> PreviousOrder { get; set; } = [];

    /// <summary>
    /// Gets or sets the rule order after the change.
    /// </summary>
    public List<string> NewOrder { get; set; } = [];

    /// <summary>
    /// Gets or sets the date and time when the change was applied (UTC).
    /// </summary>
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary

    /// <summary>
    /// Gets or sets the user or system that applied the change.
    /// </summary>
    public string AppliedBy { get; set; } = "system";
}

/// <summary>
/// Represents a request to rollback a rule order change.
/// </summary>
public sealed class RuleOrderRollbackRequest
{
    /// <summary>
    /// Gets or sets the endpoint route.
    /// </summary>
    public string EndpointRoute { get; set; } = "/";

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string? TenantId { get; set; }
}

/// <summary>
/// Defines the status of a rule change proposal.
/// </summary>
public enum ProposalStatus
{
    /// <summary>
    /// The proposal is pending review.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The proposal has been approved.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// The proposal has been rejected.
    /// </summary>
    Rejected = 2
}

/// <summary>
/// Represents a proposal for a rule change.
/// </summary>
public sealed class RuleChangeProposal
{
    /// <summary>
    /// Gets or sets the unique identifier for the proposal.
    /// </summary>
    public Guid ProposalId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the endpoint route.
    /// </summary>
    public string EndpointRoute { get; set; } = "/";

    /// <summary>
    /// Gets or sets the proposed ordered rule codes.
    /// </summary>
    public List<string> OrderedRuleCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets the user who proposed the change.
    /// </summary>
    public string ProposedBy { get; set; } = "system";

    /// <summary>
    /// Gets or sets the date and time when the change was proposed (UTC).
    /// </summary>
    public DateTime ProposedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary

    /// <summary>
    /// Gets or sets the current status of the proposal.
    /// </summary>
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    /// <summary>
    /// Gets or sets the user who reviewed the proposal.
    /// </summary>
    public string? ReviewedBy { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the proposal was reviewed (UTC).
    /// </summary>
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets an optional note from the reviewer.
    /// </summary>
    public string? ReviewNote { get; set; }
}

/// <summary>
/// Represents a request to create a new rule change proposal.
/// </summary>
public sealed class ProposeRuleChangeRequest
{
    /// <summary>
    /// Gets or sets the endpoint route.
    /// </summary>
    public string EndpointRoute { get; set; } = "/";

    /// <summary>
    /// Gets or sets the tenant identifier.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the proposed ordered rule codes.
    /// </summary>
    public List<string> OrderedRuleCodes { get; set; } = [];

    /// <summary>
    /// Gets or sets an optional note for the proposal.
    /// </summary>
    public string? Note { get; set; }
}

/// <summary>
/// Represents a request to review a rule change proposal.
/// </summary>
public sealed class ReviewProposalRequest
{
    /// <summary>
    /// Gets or sets the reviewer's note.
    /// </summary>
    public string? ReviewNote { get; set; }
}
