using System.Diagnostics;
using System.Text.Json;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

public enum CompositeMode
{
    /// <summary>Run all brains in parallel, merge + deduplicate results.</summary>
    Parallel = 0,
    /// <summary>Chain brains sequentially: output of brain N feeds as context to brain N+1.</summary>
    Sequential = 1
}

/// <summary>
/// Chains multiple IRuleProliferationBrain instances.
/// Parallel: run all, merge + dedup. Sequential: chain output as context.
/// </summary>
public sealed class CompositeProliferationBrain(
    IReadOnlyList<IRuleProliferationBrain> brains,
    CompositeMode mode,
    IScenarioDeduplicator? deduplicator = null,
    IMLog<CompositeProliferationBrain>? logger = null) : IRuleProliferationBrain
{
    public async Task<ProliferationPlan> AnalyzeAsync(
        string seedRuleCode,
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        ProliferationContext context,
        CancellationToken ct = default)
    {
        if (brains.Count == 0)
        {
            return new ProliferationPlan
            {
                SeedRuleCode = seedRuleCode,
                Scope = context.Scope,
                AiModelUsed = "none",
                Scenarios = [],
                GenerationDuration = TimeSpan.Zero
            };
        }

        if (brains.Count == 1)
        {
            return await brains[0].AnalyzeAsync(seedRuleCode, ruleSetJson, executionResult, factBagSnapshot, context, ct);
        }

        Stopwatch sw = Stopwatch.StartNew();

        List<NeuronScenario> allScenarios = mode switch
        {
            CompositeMode.Sequential => await RunSequentialAsync(seedRuleCode, ruleSetJson, executionResult, factBagSnapshot, context, ct),
            _ => await RunParallelAsync(seedRuleCode, ruleSetJson, executionResult, factBagSnapshot, context, ct)
        };

        sw.Stop();

        // Deduplicate merged results
        if (deduplicator is not null && allScenarios.Count > 0)
        {
            int beforeDedup = allScenarios.Count;
            allScenarios = deduplicator.Deduplicate(allScenarios, []).ToList();
            if (allScenarios.Count < beforeDedup)
            {
                logger?.Info("[CompositeBrain] Deduped {Before} → {After} scenarios",
                    beforeDedup, allScenarios.Count);
            }
        }

        string modelNames = string.Join(",", brains.Select((_, i) => $"brain-{i}"));

        return new ProliferationPlan
        {
            SeedRuleCode = seedRuleCode,
            Scope = context.Scope,
            AiModelUsed = $"composite({modelNames})",
            Scenarios = allScenarios,
            GenerationDuration = sw.Elapsed
        };
    }

    private async Task<List<NeuronScenario>> RunParallelAsync(
        string seedRuleCode, string ruleSetJson,
        JsonElement? executionResult, JsonElement? factBagSnapshot,
        ProliferationContext context, CancellationToken ct)
    {
        // Split budget evenly
        int budgetPerBrain = Math.Max(1, context.RemainingBudget / brains.Count);

        Task<ProliferationPlan>[] tasks = brains.Select(brain =>
        {
            ProliferationContext brainContext = context with { RemainingBudget = budgetPerBrain };
            return brain.AnalyzeAsync(seedRuleCode, ruleSetJson, executionResult, factBagSnapshot, brainContext, ct);
        }).ToArray();

        ProliferationPlan[] results = await Task.WhenAll(tasks);

        logger?.Info("[CompositeBrain] Parallel: {Count} brains returned {Total} scenarios total",
            brains.Count, results.Sum(r => r.Scenarios.Count));

        return results.SelectMany(r => r.Scenarios).ToList();
    }

    private async Task<List<NeuronScenario>> RunSequentialAsync(
        string seedRuleCode, string ruleSetJson,
        JsonElement? executionResult, JsonElement? factBagSnapshot,
        ProliferationContext context, CancellationToken ct)
    {
        List<NeuronScenario> accumulated = [];
        JsonElement? currentResult = executionResult;

        foreach (IRuleProliferationBrain brain in brains)
        {
            int remainingBudget = context.RemainingBudget - accumulated.Count;
            if (remainingBudget <= 0) break;

            ProliferationContext brainContext = context with { RemainingBudget = remainingBudget };

            ProliferationPlan plan = await brain.AnalyzeAsync(
                seedRuleCode, ruleSetJson, currentResult, factBagSnapshot, brainContext, ct);

            accumulated.AddRange(plan.Scenarios);

            // Pass output as context to next brain (if scenarios were generated)
            if (plan.Scenarios.Count > 0)
            {
                string scenarioSummary = string.Join("\n", plan.Scenarios.Select(s =>
                    $"- {s.ScenarioName}: {s.ExpectedBehavior}"));
                using JsonDocument summaryDoc = JsonDocument.Parse(
                    System.Text.Json.JsonSerializer.Serialize(new { previousScenarios = scenarioSummary }));
                currentResult = summaryDoc.RootElement.Clone();
            }
        }

        logger?.Info("[CompositeBrain] Sequential: {Count} brains produced {Total} scenarios",
            brains.Count, accumulated.Count);

        return accumulated;
    }
}
