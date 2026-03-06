using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Rules.Rules;

/// <summary>
/// Activates rules for a percentage of executions to support canary rollouts.
/// The supplied <paramref name="percentageProvider"/> returns the rollout percentage
/// (0-100) for the given rule and context, allowing per-tenant or per-group control.
/// </summary>
/// <typeparam name="T">Type of the context passed to the rule.</typeparam>
public sealed class PercentageRuleActivationStrategy<T>(
    Func<IRule<T>, T, double> percentageProvider,
    Random? random = null) : IRuleActivationStrategy<T>
{
    private readonly Func<IRule<T>, T, double> _percentageProvider =
        percentageProvider ?? throw new ArgumentNullException(nameof(percentageProvider));

    private readonly Random _random = random ?? Random.Shared;

    public bool IsActive(IRule<T> rule, T context)
    {
        double percentage = _percentageProvider(rule, context);
        return _random.NextDouble() < percentage / 100d;
    }
}
