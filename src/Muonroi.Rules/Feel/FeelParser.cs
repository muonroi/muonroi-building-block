namespace Muonroi.Rules.Feel;

/// <summary>
/// Thin wrapper to parse FEEL expressions into raw values using FeelEvaluator runtime.
/// </summary>
public sealed class FeelParser
{
    /// <summary>
    /// Parses and evaluates a FEEL expression.
    /// </summary>
    /// <param name="expression">The FEEL expression to parse.</param>
    /// <param name="variables">The context variables for evaluation.</param>
    /// <returns>The result of the evaluation.</returns>
    public static object? Parse(string expression, Dictionary<string, object> variables)
    {
        return FeelEvaluator.EvaluateValue(expression, variables);
    }
}
