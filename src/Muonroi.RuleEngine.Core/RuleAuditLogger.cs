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
        string? corrId = Activity.Current?.TraceId.ToString();
        logger?.Info(
            "Rule matched {Rule} with facts {@Facts} (corrId: {CorrId})",
            rule.Name,
            facts.AsReadOnly(),
            corrId);
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
        string? corrId = Activity.Current?.TraceId.ToString();
        logger?.Info(
            "Rule fired {Rule} in {Duration} ms with changes {@Changes} (Success: {Success}, corrId: {CorrId})",
            rule.Name,
            duration.TotalMilliseconds,
            diff,
            result.IsSuccess,
            corrId);
        return Task.CompletedTask;
    }
}