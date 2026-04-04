using System.Text;
using System.Text.Json;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Analyzes failed scenarios by sending failure context to the AI brain
/// and generating follow-up scenarios that probe the failure boundary.
/// </summary>
public sealed class DefaultFailureAnalyzer(
    IRuleProliferationBrain brain,
    ProliferationOptions options,
    IMLog<DefaultFailureAnalyzer>? logger = null) : IFailureAnalyzer
{
    /// <summary>Analyzes a failed scenario and generates follow-up scenarios.</summary>
    public async Task<IReadOnlyList<NeuronScenario>> AnalyzeFailureAsync(
        NeuronScenario failedScenario,
        ScenarioResult failureResult,
        string ruleSetJson,
        ProliferationContext context,
        CancellationToken ct = default)
    {
        if (!options.EnableFailureFeedback)
            return [];

        int budget = Math.Min(options.MaxScenariosPerFailure, context.RemainingBudget);
        if (budget <= 0) return [];

        try
        {
            // Build a focused context that includes failure information
            ProliferationContext failureContext = new()
            {
                Scope = context.Scope,
                CurrentDepth = failedScenario.GenerationDepth + 1,
                RemainingBudget = budget,
                FocusAreas = BuildFailureFocusAreas(failedScenario, failureResult),
                TenantId = context.TenantId
            };

            // Pass failure details as execution result so the brain sees what went wrong
            JsonElement? failureInfo = BuildFailureInfoElement(failedScenario, failureResult);

            ProliferationPlan plan = await brain.AnalyzeAsync(
                seedRuleCode: failedScenario.SeedRuleCode,
                ruleSetJson: ruleSetJson,
                executionResult: failureInfo,
                factBagSnapshot: null,
                context: failureContext,
                ct: ct);

            if (plan.Scenarios.Count == 0)
                return [];

            // Set parent-child relationship and cap to budget
            List<NeuronScenario> children = [.. plan.Scenarios
                .Take(budget)
                .Select(s => s with
                {
                    ParentScenarioId = failedScenario.Id,
                    GenerationDepth = failedScenario.GenerationDepth + 1
                })];

            logger?.Info("[Proliferation] Failure analysis generated {Count} follow-up scenarios from failed scenario {ParentId}",
                children.Count, failedScenario.Id);

            return children;
        }
        catch (Exception ex)
        {
            logger?.Warn("[Proliferation] Failure analysis failed for scenario {Id}: {Error}",
                failedScenario.Id, ex.Message);
            return [];
        }
    }

    private static List<string> BuildFailureFocusAreas(NeuronScenario failedScenario, ScenarioResult failureResult)
    {
        List<string> areas =
        [
            "failure-boundary-probing",
            $"failed-scenario: {failedScenario.ScenarioName}"
        ];

        if (failureResult.Errors.Count > 0)
        {
            areas.Add($"error-context: {failureResult.Errors[0]}");
        }

        if (!string.IsNullOrWhiteSpace(failureResult.ActualBehavior))
        {
            areas.Add($"actual-behavior: {failureResult.ActualBehavior}");
        }

        return areas;
    }

    private static JsonElement? BuildFailureInfoElement(NeuronScenario failedScenario, ScenarioResult failureResult)
    {
        try
        {
            var failureInfo = new
            {
                failedScenarioName = failedScenario.ScenarioName,
                failedInputFacts = failedScenario.InputFacts.ToString(),
                expectedBehavior = failedScenario.ExpectedBehavior,
                actualBehavior = failureResult.ActualBehavior,
                errors = failureResult.Errors,
                isSuccess = failureResult.IsSuccess,
                matchesExpectation = failureResult.MatchesExpectation
            };

            string json = JsonSerializer.Serialize(failureInfo);
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}
