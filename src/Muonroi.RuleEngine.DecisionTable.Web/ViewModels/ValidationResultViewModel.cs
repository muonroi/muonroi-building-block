namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

public sealed class ValidationResultViewModel
{
    public required ValidationResult Result { get; init; }
    public int ErrorCount => Result.Errors.Count;
    public int WarningCount => Result.Warnings?.Count ?? 0;
}
