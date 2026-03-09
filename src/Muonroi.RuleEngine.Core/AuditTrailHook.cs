using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Emits audit events for rule execution and allows data minimization
/// through an optional context projector.
/// </summary>
/// <typeparam name="TContext">Type of execution context.</typeparam>
public sealed class AuditTrailHook<TContext>(
    IMLog<AuditTrailHook<TContext>> logger,
    Func<TContext, object?>? projector = null) : IHookHandler<TContext>
{
    /// <inheritdoc/>
    public Task HandleAsync(
        HookPoint point,
        IRule<TContext> rule,
        RuleResult result,
        FactBag facts,
        TContext context,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        object? minimal = projector?.Invoke(context);
        object durationValue = duration?.TotalMilliseconds ?? 0d;
        object contextValue = minimal ?? "<null>";
        object[] logArgs =
        [
            point,
            rule.Name ?? string.Empty,
            result.IsSuccess,
            durationValue,
            contextValue,
            facts
        ];
        logger?.Info(
            "Audit {Point} {Rule} Success:{Success} Duration:{Duration} Context:{@Context} Facts:{@Facts}",
            logArgs);
        return Task.CompletedTask;
    }
}
