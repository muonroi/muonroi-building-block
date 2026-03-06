namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Result returned by rule execution.
/// </summary>
/// <param name="IsSuccess">Indicates whether the rule passed.</param>
/// <param name="Errors">Collection of error messages.</param>
public sealed record RuleResult(bool IsSuccess, IReadOnlyList<string> Errors)
{
    public static RuleResult Passed()
    {
        return new RuleResult(true, []);
    }

    public static RuleResult Success()
    {
        return new RuleResult(true, []);
    }

    public static RuleResult Failure(params string[] errors)
    {
        return new RuleResult(false, errors);
    }
}