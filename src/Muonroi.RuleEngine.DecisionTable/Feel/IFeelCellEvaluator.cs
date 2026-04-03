namespace Muonroi.RuleEngine.DecisionTable.Feel;

/// <summary>
/// Evaluates and validates decision-table cell expressions.
/// </summary>
public interface IFeelCellEvaluator
{
    /// <summary>
    /// Evaluates a single input-cell expression against an input value.
    /// </summary>
    bool Evaluate(string expression, object? inputValue, string? columnDataType = null);

    /// <summary>
    /// Validates expression syntax. Returns null when valid; otherwise an error message.
    /// </summary>
    string? Validate(string expression, string? columnDataType = null);
}
