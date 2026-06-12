namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Provides ruleset lifecycle change type constants.
/// </summary>
public static class RuleSetChangeTypes
{
    /// <summary>Ruleset was saved.</summary>
    public const string Saved = "saved";
    /// <summary>Ruleset was activated.</summary>
    public const string Activated = "activated";
    /// <summary>Ruleset was submitted for approval.</summary>
    public const string SubmittedForApproval = "submitted_for_approval";
    /// <summary>Ruleset was approved.</summary>
    public const string Approved = "approved";
    /// <summary>Ruleset was rejected.</summary>
    public const string Rejected = "rejected";
    /// <summary>Canary rollout was started.</summary>
    public const string CanaryStarted = "canary_started";
    /// <summary>Canary rollout was promoted.</summary>
    public const string CanaryPromoted = "canary_promoted";
    /// <summary>Canary rollout was rolled back.</summary>
    public const string CanaryRolledBack = "canary_rolled_back";
}

/// <summary>
/// Describes a ruleset lifecycle change event.
/// </summary>
public sealed record RuleSetChangeEvent(
    string TenantId,
    string WorkflowName,
    string ChangeType,
    int? Version,
    DateTimeOffset OccurredAtUtc);
