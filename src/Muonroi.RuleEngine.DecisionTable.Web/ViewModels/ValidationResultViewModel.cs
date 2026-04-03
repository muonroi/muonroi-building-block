namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

/// <summary>
/// View model for decision table validation results.
/// </summary>
public sealed class ValidationResultViewModel
{
    /// <summary>
    /// Validation result payload.
    /// </summary>
    public required ValidationResult Result { get; init; }
    /// <summary>
    /// Number of validation errors.
    /// </summary>
    public int ErrorCount => Result.Errors.Count;
    /// <summary>
    /// Number of validation warnings.
    /// </summary>
    public int WarningCount => Result.Warnings?.Count ?? 0;
}
