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
        logger?.Info(
            "Audit {Point} {Rule} Success:{Success} Duration:{Duration} Context:{@Context} Facts:{@Facts}",
            point, rule.Name, result.IsSuccess, duration?.TotalMilliseconds, minimal, facts);
        return Task.CompletedTask;
    }
}