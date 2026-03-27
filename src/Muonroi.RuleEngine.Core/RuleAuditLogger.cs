using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Default implementation of <see cref="IRuleEventListener{TContext}"/> that logs rule execution details.
/// </summary>
/// <typeparam name="TContext">Type of the rule context.</typeparam>
public sealed class RuleAuditLogger<TContext>(IMLog<RuleAuditLogger<TContext>> logger) : IRuleEventListener<TContext>
{
    /// <inheritdoc/>
    public Task OnRuleMatchedAsync(IRule<TContext> rule, TContext context, FactBag facts,
        CancellationToken cancellationToken = default)
    {
        object correlationId = Activity.Current?.TraceId.ToString() ?? string.Empty;
        object[] matchedArgs =
        [
            rule.Name ?? string.Empty,
            facts.AsReadOnly(),
            correlationId
        ];
        logger?.Info(
            "Rule matched {Rule} with facts {@Facts} (corrId: {CorrId})",
            matchedArgs);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task OnRuleFiredAsync(
        IRule<TContext> rule,
        RuleResult result,
        FactBag facts,
        IReadOnlyDictionary<string, (object? OldValue, object? NewValue)> changes,
        TContext context,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, object?> diff = changes.ToDictionary(
            kvp => kvp.Key,
            kvp => (object?)new { kvp.Value.OldValue, kvp.Value.NewValue });
        object correlationId = Activity.Current?.TraceId.ToString() ?? string.Empty;
        object[] firedArgs =
        [
            rule.Name ?? string.Empty,
            duration.TotalMilliseconds,
            diff,
            result.IsSuccess,
            correlationId
        ];
        logger?.Info(
            "Rule fired {Rule} in {Duration} ms with changes {@Changes} (Success: {Success}, corrId: {CorrId})",
            firedArgs);
        return Task.CompletedTask;
    }
}
