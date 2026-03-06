namespace Muonroi.AspNetCore.Models.Changes;

public sealed class RuleOrderChangeRequest
{
    public string EndpointRoute { get; set; } = "/";
    public string? TenantId { get; set; }
    public List<string> OrderedRuleCodes { get; set; } = [];
}

public sealed class RuleOrderChangeValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

public sealed class RuleChangeRecord
{
    public Guid ChangeId { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string EndpointRoute { get; set; } = "/";
    public List<string> PreviousOrder { get; set; } = [];
    public List<string> NewOrder { get; set; } = [];
    public DateTime AppliedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    public string AppliedBy { get; set; } = "system";
}

public sealed class RuleOrderRollbackRequest
{
    public string EndpointRoute { get; set; } = "/";
    public string? TenantId { get; set; }
}

public enum ProposalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public sealed class RuleChangeProposal
{
    public Guid ProposalId { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string EndpointRoute { get; set; } = "/";
    public List<string> OrderedRuleCodes { get; set; } = [];
    public string ProposedBy { get; set; } = "system";
    public DateTime ProposedAtUtc { get; set; } = DateTime.UtcNow; // MBB001-exempt: static-class boundary
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}

public sealed class ProposeRuleChangeRequest
{
    public string EndpointRoute { get; set; } = "/";
    public string? TenantId { get; set; }
    public List<string> OrderedRuleCodes { get; set; } = [];
    public string? Note { get; set; }
}

public sealed class ReviewProposalRequest
{
    public string? ReviewNote { get; set; }
}
