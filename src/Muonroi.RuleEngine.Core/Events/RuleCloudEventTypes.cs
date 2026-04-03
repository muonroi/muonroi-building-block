namespace Muonroi.RuleEngine.Core.Events;

/// <summary>
/// Defines CloudEvents type constants for rule engine lifecycle events.
/// All types follow the reverse-DNS naming convention: muonroi.{domain}.{action}
/// </summary>
public static class RuleCloudEventTypes
{
    /// <summary>
    /// Emitted when a ruleset version is saved (may not yet be active).
    /// </summary>
    public const string RuleSetChanged = "muonroi.ruleset.changed";

    /// <summary>
    /// Emitted when a ruleset version is activated and becomes live.
    /// </summary>
    public const string RuleSetActivated = "muonroi.ruleset.activated";

    /// <summary>
    /// Emitted after a single rule is successfully evaluated and executed.
    /// </summary>
    public const string RuleExecuted = "muonroi.rule.executed";

    /// <summary>
    /// Emitted when a rule fails during evaluation or execution.
    /// </summary>
    public const string RuleFailed = "muonroi.rule.failed";
}
