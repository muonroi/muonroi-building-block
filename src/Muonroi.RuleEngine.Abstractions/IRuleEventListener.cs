namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Listens to rule engine events for observability and auditing.
/// </summary>
/// <typeparam name="TContext">Type of the rule context.</typeparam>
public interface IRuleEventListener<TContext>
{
    /// <summary>
    /// Called when a rule is about to be evaluated.
    /// </summary>
    Task OnRuleMatchedAsync(IRule<TContext> rule, TContext context, FactBag facts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a rule has finished executing.
    /// </summary>
    /// <param name="rule">The rule that executed.</param>
    /// <param name="result">Result returned by the rule.</param>
    /// <param name="facts">Facts after the rule ran.</param>
    /// <param name="changes">Collection of fact changes produced by the rule.</param>
    /// <param name="context">Execution context.</param>
    /// <param name="duration">Execution duration.</param>
    Task OnRuleFiredAsync(
        IRule<TContext> rule,
        RuleResult result,
        FactBag facts,
        IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes,
        TContext context,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
