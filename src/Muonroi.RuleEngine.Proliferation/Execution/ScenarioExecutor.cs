using System.Diagnostics;
using System.Text.Json;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Proliferation.Models;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.Tenancy.Core;

namespace Muonroi.RuleEngine.Proliferation.Execution;

public sealed class ScenarioExecutor(
    RulesEngineService rulesEngineService,
    IRuleSetStore ruleSetStore,
    ISystemExecutionContextAccessor executionContextAccessor,
    ProliferationOptions options,
    IMLog<ScenarioExecutor>? logger = null) : IScenarioExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Default context type for DryRunAsync — matches RuleDryRunService behavior
    private static readonly string DefaultContextType = typeof(Dictionary<string, object?>).AssemblyQualifiedName!;

    public async Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        ISystemExecutionContext previousContext = executionContextAccessor.Get();
        string? previousTenantId = TenantContext.CurrentTenantId;

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.ScenarioExecutionTimeoutSeconds));

        try
        {
            // Set tenant context if scenario specifies one
            if (!string.IsNullOrWhiteSpace(scenario.TenantId))
            {
                TenantContext.CurrentTenantId = scenario.TenantId;
            }

            OrchestratorResult result;

            if (!string.IsNullOrWhiteSpace(scenario.GeneratedRuleFlowGraph))
            {
                // Dry-run with modified rule flow graph
                FactBag factBag = await rulesEngineService.DryRunAsync(
                    scenario.SeedRuleCode,
                    scenario.GeneratedRuleFlowGraph,
                    scenario.InputFacts,
                    contextType: DefaultContextType,
                    cancellationToken: timeoutCts.Token);

                // Wrap FactBag into a synthetic OrchestratorResult
                result = OrchestratorResult.Success(
                    ExecutionMode.BestEffort,
                    factBag,
                    new Dictionary<string, RuleResult>());
            }
            else
            {
                // Execute existing rule with provided input facts
                FactBag factBag = await rulesEngineService.DryRunAsync(
                    scenario.SeedRuleCode,
                    await GetRuleSetJsonAsync(scenario.SeedRuleCode, timeoutCts.Token),
                    scenario.InputFacts,
                    contextType: DefaultContextType,
                    cancellationToken: timeoutCts.Token);

                result = OrchestratorResult.Success(
                    ExecutionMode.BestEffort,
                    factBag,
                    new Dictionary<string, RuleResult>());
            }

            sw.Stop();

            string actualBehavior = result.IsSuccess ? "passed" : $"failed: {string.Join("; ", result.Errors)}";
            bool matchesExpectation = EvaluateExpectation(scenario.ExpectedBehavior, result);

            JsonElement outputFacts = JsonSerializer.SerializeToElement(
                result.Facts.AsReadOnly(), JsonOptions);

            return new ScenarioResult
            {
                ScenarioId = scenario.Id,
                IsSuccess = result.IsSuccess,
                MatchesExpectation = matchesExpectation,
                ActualBehavior = actualBehavior,
                OutputFacts = outputFacts,
                Errors = result.Errors,
                Duration = sw.Elapsed,
                ExecutedAt = DateTimeOffset.UtcNow
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            logger?.Warn("Scenario {ScenarioId} timed out after {Timeout}s",
                scenario.Id, options.ScenarioExecutionTimeoutSeconds);
            return new ScenarioResult
            {
                ScenarioId = scenario.Id,
                IsSuccess = false,
                MatchesExpectation = false,
                ActualBehavior = "timeout",
                Errors = [$"Execution timed out after {options.ScenarioExecutionTimeoutSeconds}s"],
                Duration = sw.Elapsed,
                ExecutedAt = DateTimeOffset.UtcNow
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger?.Error(ex, "Scenario {ScenarioId} execution error", scenario.Id);
            return new ScenarioResult
            {
                ScenarioId = scenario.Id,
                IsSuccess = false,
                MatchesExpectation = false,
                ActualBehavior = $"error: {ex.Message}",
                Errors = [ex.Message],
                Duration = sw.Elapsed,
                ExecutedAt = DateTimeOffset.UtcNow
            };
        }
        finally
        {
            // Restore previous context
            executionContextAccessor.Set(previousContext);
            TenantContext.CurrentTenantId = previousTenantId;
        }
    }

    private async Task<string> GetRuleSetJsonAsync(string workflowName, CancellationToken ct)
    {
        string? json = await ruleSetStore.GetAsync(workflowName, version: null, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                $"No active ruleset found for workflow '{workflowName}'. Ensure the workflow exists and has an active version.");
        }

        return json;
    }

    internal static bool EvaluateExpectation(string? expectedBehavior, OrchestratorResult result)
    {
        if (string.IsNullOrWhiteSpace(expectedBehavior)) return true; // No expectation = always matches

        string expected = expectedBehavior.ToLowerInvariant().Trim();

        if (expected.StartsWith("should pass") || expected.StartsWith("should succeed"))
            return result.IsSuccess;

        if (expected.StartsWith("should fail"))
            return !result.IsSuccess;

        // Default: if there's any expectation text and the result has errors, mismatch
        return result.IsSuccess;
    }
}
