namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Handles telemetry or side effects at specific <see cref="HookPoint"/>s.
/// </summary>
public interface IHookHandler<TContext>
{
    Task HandleAsync(
        HookPoint point,
        IRule<TContext> rule,
        RuleResult result,
        FactBag facts,
        TContext context,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default);
}
