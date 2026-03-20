namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Describes a validation problem found in a ruleset payload.
/// </summary>
public sealed record RuleSetValidationIssue(
    /// <summary>Machine-readable validation code.</summary>
    string Code,
    /// <summary>Human-readable validation message.</summary>
    string Message,
    /// <summary>Optional JSON path for the issue.</summary>
    string? Path = null,
    /// <summary>Severity of the issue.</summary>
    string Severity = "Error");
