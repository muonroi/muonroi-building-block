namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Handles telemetry or side effects at specific <see cref="HookPoint"/>s.
/// </summary>
public interface IHookHandler<TContext>
{
    /// <summary>
    /// Invoked when a rule execution reaches a hook point.
    /// </summary>
    /// <param name="point">The hook point being reached.</param>
    /// <param name="rule">The rule being executed.</param>
    /// <param name="result">The result of the rule execution.</param>
    /// <param name="facts">The current fact bag.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="duration">The duration of the rule execution, if applicable.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleAsync(
        HookPoint point,
        IRule<TContext> rule,
        RuleResult result,
        FactBag facts,
        TContext context,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default);
}
