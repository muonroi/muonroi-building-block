using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Activates rules for a percentage of executions to support canary rollouts.
/// The supplied <paramref name="percentageProvider"/> returns the rollout percentage     
/// (0-100) for the given rule and context, allowing per-tenant or per-group control.     
/// </summary>
/// <typeparam name="T">Type of the context passed to the rule.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="PercentageRuleActivationStrategy{T}"/> class.
/// </remarks>
/// <param name="percentageProvider">A function that provides the activation percentage.</param>
/// <param name="random">Optional random number generator.</param>
public sealed class PercentageRuleActivationStrategy<T>(
    Func<IRule<T>, T, double> percentageProvider,
    Random? random = null) : IRuleActivationStrategy<T>
{
    private readonly Func<IRule<T>, T, double> _percentageProvider =
        percentageProvider ?? throw new ArgumentNullException(nameof(percentageProvider));

    private readonly Random _random = random ?? Random.Shared;

    /// <summary>
    /// Determines whether the rule should be active based on the configured percentage.
    /// </summary>
    /// <param name="rule">The rule to evaluate.</param>
    /// <param name="context">The context for evaluation.</param>
    /// <returns>True if the rule should be active; otherwise, false.</returns>
    public bool IsActive(IRule<T> rule, T context)
    {
        double percentage = _percentageProvider(rule, context);
        return _random.NextDouble() < percentage / 100d;
    }
}
