using Muonroi.Logging.Abstractions;

namespace Muonroi.Rules.Flags;

/// <summary>
/// Evaluates feature flags to decide whether a new rule-set should be executed.
/// When a flag is disabled the evaluator runs the new rule-set in "shadow" mode
/// and logs any differences in the output without affecting production.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public sealed class FeatureFlagEvaluator(IFeatureFlagClient client, IMLog<FeatureFlagEvaluator> logger)
{
    /// <summary>
    /// Executes the provided rule sets based on the feature flag.
    /// </summary>
    /// <typeparam name="T">Type of the rule result.</typeparam>
    /// <param name="flag">Feature flag name.</param>
    /// <param name="context">Tenant/segment context for evaluation.</param>
    /// <param name="currentRules">Current production rule-set.</param>
    /// <param name="newRules">New rule-set to roll out.</param>
    /// <returns>The result of the active rule-set.</returns>
    public T Evaluate<T>(string flag, FeatureContext context, Func<T> currentRules, Func<T> newRules)
    {
        if (client.IsEnabled(flag, context))
        {
            // Feature enabled: execute new rule-set.
            return newRules();
        }

        // Feature disabled: run new rule-set in shadow mode.
        T? currentResult = currentRules();
        T? shadowResult = newRules();

        if (!EqualityComparer<T>.Default.Equals(currentResult, shadowResult))
        {
            object currentValue = currentResult is null ? "<null>" : currentResult;
            object shadowValue = shadowResult is null ? "<null>" : shadowResult;
            logger?.Info(
                "Shadow diff for {Flag} tenant {Tenant} segment {Segment}: {Current} vs {Shadow}",
                flag, context.TenantId, context.Segment ?? string.Empty, currentValue, shadowValue);
        }

        return currentResult;
    }
}

/// <summary>
/// Context used for feature flag evaluation (tenant/segment).
/// </summary>
/// <param name="TenantId">Tenant identifier.</param>
/// <param name="Segment">Optional user segment.</param>
public readonly record struct FeatureContext(string TenantId, string? Segment = null);

/// <summary>
/// Abstraction of a feature flag client (e.g. Unleash/OpenFeature).
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
public interface IFeatureFlagClient
{
    /// <summary>
    /// Determines whether the specified feature flag is enabled for the given context.
    /// </summary>
    /// <param name="flag">The feature flag name.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns><c>true</c> if the feature flag is enabled; otherwise, <c>false</c>.</returns>
    bool IsEnabled(string flag, FeatureContext context);
}
