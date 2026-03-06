namespace Muonroi.RuleEngine.Runtime.Rules;

public sealed record RuleSetValidationIssue(
    string Code,
    string Message,
    string? Path = null,
    string Severity = "Error");
