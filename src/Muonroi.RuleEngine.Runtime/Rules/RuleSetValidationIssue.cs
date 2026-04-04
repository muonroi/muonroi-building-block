namespace Muonroi.RuleEngine.Runtime.Rules;

/// <summary>
/// Describes a validation problem found in a ruleset payload.
/// </summary>
public sealed record RuleSetValidationIssue(
    string Code,
    string Message,
    string? Path = null,
    string Severity = "Error");
