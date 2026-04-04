namespace Muonroi.RuleEngine.Runtime.Web.ViewModels;

/// <summary>Request payload for saving a ruleset.</summary>
public sealed class SaveRuleSetRequest
{
    /// <summary>Ruleset definition to save.</summary>
    public JsonElement RuleSet { get; init; }
    /// <summary>Whether to activate the ruleset after saving.</summary>
    public bool ActivateAfterSave { get; init; } = true;
    /// <summary>Optional actor identity for audit logging.</summary>
    public string? Actor { get; init; }
    /// <summary>Optional detail string for audit logging.</summary>
    public string? Detail { get; init; }
}

/// <summary>Request payload for activating a ruleset.</summary>
public sealed class ActivateRuleSetRequest
{
    /// <summary>Optional actor identity for audit logging.</summary>
    public string? Actor { get; init; }
    /// <summary>Optional detail string for audit logging.</summary>
    public string? Detail { get; init; }
}

/// <summary>Request payload for validating a ruleset.</summary>
public sealed class ValidateRuleSetRequest
{
    /// <summary>Ruleset definition to validate.</summary>
    public JsonElement RuleSet { get; init; }
}

/// <summary>Request payload for a dry-run execution of a ruleset.</summary>
public sealed class DryRunRuleSetRequest
{
    /// <summary>Ruleset definition to execute.</summary>
    public JsonElement RuleSet { get; init; }
    /// <summary>Execution context for the dry run.</summary>
    public JsonElement Context { get; init; }
    /// <summary>Optional context type hint.</summary>
    public string? ContextType { get; init; }
}
