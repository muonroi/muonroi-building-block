namespace Muonroi.Rules.Feel;

/// <summary>
/// Thin wrapper to parse FEEL expressions into raw values using FeelEvaluator runtime.
/// </summary>
public sealed class FeelParser
{
    public static object? Parse(string expression, Dictionary<string, object> variables)
    {
        return FeelEvaluator.EvaluateValue(expression, variables);
    }
}
