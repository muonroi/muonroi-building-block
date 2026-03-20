namespace Muonroi.Rules.Rules;

/// <summary>
/// Defines the types of changes that can occur to a ruleset.
/// </summary>
public static class RuleSetChangeTypes
{
    /// <summary>
    /// The ruleset was saved or updated.
    /// </summary>
    public const string Saved = "saved";

    /// <summary>
    /// A specific version of the ruleset was activated.
    /// </summary>
    public const string Activated = "activated";
}

/// <summary>
/// Represents an event that occurs when a ruleset is changed.
/// </summary>
/// <param name="TenantId">The ID of the tenant the ruleset belongs to.</param>
/// <param name="WorkflowName">The name of the workflow/ruleset.</param>
/// <param name="ChangeType">The type of change (e.g., saved, activated).</param>
/// <param name="Version">The version of the ruleset, if applicable.</param>
/// <param name="OccurredAtUtc">The timestamp when the change occurred.</param>
public sealed record RuleSetChangeEvent(
    string TenantId,
    string WorkflowName,
    string ChangeType,
    int? Version,
    DateTimeOffset OccurredAtUtc);
