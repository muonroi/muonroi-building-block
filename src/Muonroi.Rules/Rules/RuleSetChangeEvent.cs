namespace Muonroi.Rules.Rules;

public static class RuleSetChangeTypes
{
    public const string Saved = "saved";
    public const string Activated = "activated";
}

public sealed record RuleSetChangeEvent(
    string TenantId,
    string WorkflowName,
    string ChangeType,
    int? Version,
    DateTimeOffset OccurredAtUtc);
