namespace Muonroi.RuleEngine.DecisionTable.Feel;

internal sealed class SimplifiedFeelCellEvaluator : IFeelCellEvaluator
{
    public bool Evaluate(string expression, object? inputValue, string? columnDataType = null)
    {
        _ = columnDataType;
        return Converters.DecisionTableExpressionEvaluator.Evaluate(inputValue, expression);
    }

    public string? Validate(string expression, string? columnDataType = null)
    {
        _ = columnDataType;
        return Converters.DecisionTableExpressionEvaluator.IsExpressionValid(expression)
            ? null
            : "Invalid expression.";
    }
}
