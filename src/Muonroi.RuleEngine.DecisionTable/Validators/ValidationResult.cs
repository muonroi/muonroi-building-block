namespace Muonroi.RuleEngine.DecisionTable.Validators;

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string>? Warnings = null);
