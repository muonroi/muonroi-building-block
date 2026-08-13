using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging.Abstractions;

namespace Muonroi.RuleEngine.Core;

/// <summary>
/// Default runtime router for traditional/rule/hybrid/shadow execution.
/// </summary>
public sealed class MRuleExecutionRouter<TContext>(
    RuleOrchestrator<TContext> orchestrator,
    Microsoft.Extensions.Options.IOptions<MRuleEngineOptions>? options,
    IMLog<MRuleExecutionRouter<TContext>>? logger = null) : IMRuleExecutionRouter<TContext>
{
    private readonly MRuleEngineOptions _options = options?.Value ?? new MRuleEngineOptions();

    /// <inheritdoc/>
    public async Task<FactBag> ExecuteAsync(
        TContext context,
        Func<CancellationToken, Task>? traditionalPath = null,
        RuleExecutionMode? modeOverride = null,
        CancellationToken cancellationToken = default)
    {
        RuleExecutionMode mode = modeOverride ?? _options.ExecutionMode;
        return mode switch
        {
            RuleExecutionMode.Traditional => await MRuleExecutionRouter<TContext>.ExecuteTraditionalAsync(traditionalPath, cancellationToken),
            RuleExecutionMode.Rules => await orchestrator.ExecuteAsync(context, cancellationToken: cancellationToken),
            RuleExecutionMode.Hybrid => await ExecuteHybridAsync(context, traditionalPath, cancellationToken),
            RuleExecutionMode.Shadow => await ExecuteShadowAsync(context, traditionalPath, cancellationToken),
            _ => await orchestrator.ExecuteAsync(context, cancellationToken: cancellationToken)
        };
    }

    private static async Task<FactBag> ExecuteTraditionalAsync(
        Func<CancellationToken, Task>? traditionalPath,
        CancellationToken cancellationToken)
    {
        if (traditionalPath is null)
        {
            return MGuard.Fail<FactBag>("Traditional path delegate is required for Traditional mode.");
        }

        await traditionalPath(cancellationToken);
        return new FactBag();
    }

    private async Task<FactBag> ExecuteHybridAsync(
        TContext context,
        Func<CancellationToken, Task>? traditionalPath,
        CancellationToken cancellationToken)
    {
        double roll = Random.Shared.NextDouble();
        if (traditionalPath is not null && roll <= _options.NormalizedTraditionalWeight)
        {
            logger?.Info("MRuleExecutionRouter selected Traditional path in Hybrid mode.");
            await traditionalPath(cancellationToken);
            return new FactBag();
        }

        logger?.Info("MRuleExecutionRouter selected Rules path in Hybrid mode.");
        return await orchestrator.ExecuteAsync(context, cancellationToken: cancellationToken);
    }

    private async Task<FactBag> ExecuteShadowAsync(
        TContext context,
        Func<CancellationToken, Task>? traditionalPath,
        CancellationToken cancellationToken)
    {
        FactBag primaryFacts;
        if (traditionalPath is not null)
        {
            await traditionalPath(cancellationToken);
            primaryFacts = new FactBag();
            FactBag shadowFacts = await orchestrator.ExecuteAsync(context, cancellationToken: cancellationToken);
            if (_options.LogDifferences)
            {
                LogShadowDifference(primaryFacts.AsReadOnly(), shadowFacts.AsReadOnly());
            }
        }
        else
        {
            primaryFacts = await orchestrator.ExecuteAsync(context, cancellationToken: cancellationToken);
        }

        return primaryFacts;
    }

    private void LogShadowDifference(IReadOnlyDictionary<string, object?> baseline, IReadOnlyDictionary<string, object?> shadow)
    {
        HashSet<string> baselineKeys = baseline.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> shadowKeys = shadow.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        string[] missingInShadow = [.. baselineKeys.Except(shadowKeys, StringComparer.OrdinalIgnoreCase)];
        string[] extraInShadow = [.. shadowKeys.Except(baselineKeys, StringComparer.OrdinalIgnoreCase)];

        if (missingInShadow.Length == 0 && extraInShadow.Length == 0)
        {
            logger?.Info("Shadow execution completed with no key-level fact differences.");
            return;
        }

        logger?.Warn(
            "Shadow execution fact differences. MissingInShadow={Missing}, ExtraInShadow={Extra}",
            string.Join(", ", missingInShadow),
            string.Join(", ", extraInShadow));
    }
}
