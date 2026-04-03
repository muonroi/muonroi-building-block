namespace Muonroi.RuleEngine.DecisionTable.Validators;

/// <summary>
/// Result of validating a decision table.
/// </summary>
/// <param name="IsValid">True when no errors were detected.</param>
/// <param name="Errors">Validation errors.</param>
/// <param name="Warnings">Optional validation warnings.</param>
public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string>? Warnings = null);
