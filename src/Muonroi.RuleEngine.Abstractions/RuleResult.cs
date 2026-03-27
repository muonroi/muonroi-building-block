namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Result returned by rule execution.
/// </summary>
/// <param name="IsSuccess">Indicates whether the rule passed.</param>
/// <param name="Errors">Collection of error messages.</param>
public sealed record RuleResult(bool IsSuccess, IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Creates a successful rule result.
    /// </summary>
    /// <returns>A successful <see cref="RuleResult"/>.</returns>
    public static RuleResult Passed()
    {
        return new RuleResult(true, []);
    }

    /// <summary>
    /// Creates a successful rule result.
    /// </summary>
    /// <returns>A successful <see cref="RuleResult"/>.</returns>
    public static RuleResult Success()
    {
        return new RuleResult(true, []);
    }

    /// <summary>
    /// Creates a failed rule result with the specified error messages.
    /// </summary>
    /// <param name="errors">The error messages.</param>
    /// <returns>A failed <see cref="RuleResult"/>.</returns>
    public static RuleResult Failure(params string[] errors)
    {
        return new RuleResult(false, errors);
    }
}