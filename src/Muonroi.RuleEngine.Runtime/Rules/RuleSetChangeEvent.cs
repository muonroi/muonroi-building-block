namespace Muonroi.RuleEngine.Runtime.Rules;

public static class RuleSetChangeTypes
{
    public const string Saved = "saved";
    public const string Activated = "activated";
    public const string SubmittedForApproval = "submitted_for_approval";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string CanaryStarted = "canary_started";
    public const string CanaryPromoted = "canary_promoted";
    public const string CanaryRolledBack = "canary_rolled_back";
}

public sealed record RuleSetChangeEvent(
    string TenantId,
    string WorkflowName,
    string ChangeType,
    int? Version,
    DateTimeOffset OccurredAtUtc);
