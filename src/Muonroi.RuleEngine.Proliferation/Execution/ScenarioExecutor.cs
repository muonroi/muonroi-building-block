namespace Muonroi.RuleEngine.Proliferation.Execution;

/// <summary>
/// Executes a proliferation scenario against the rules engine.
/// </summary>
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

    private static readonly string DefaultContextType = MGuard.NotNull(typeof(Dictionary<string, object?>).AssemblyQualifiedName);

    /// <summary>Executes the scenario and records the outcome.</summary>
    public async Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, CancellationToken ct = default)
    {
        Stopwatch sw = Stopwatch.StartNew();
        ISystemExecutionContext previousContext = executionContextAccessor.Get();
        string? previousTenantId = TenantContext.CurrentTenantId;

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.ScenarioExecutionTimeoutSeconds));

        try
        {
            if (!string.IsNullOrWhiteSpace(scenario.TenantId))
            {
                TenantContext.CurrentTenantId = scenario.TenantId;
            }

            OrchestratorResult result;

            if (!string.IsNullOrWhiteSpace(scenario.GeneratedRuleFlowGraph))
            {
                FactBag factBag = await rulesEngineService.DryRunAsync(
                    scenario.SeedRuleCode,
                    scenario.GeneratedRuleFlowGraph,
                    scenario.InputFacts,
                    contextType: DefaultContextType,
                    cancellationToken: timeoutCts.Token);

                result = OrchestratorResult.Success(
                    ExecutionMode.BestEffort,
                    factBag,
                    new Dictionary<string, RuleResult>());
            }
            else
            {
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
            executionContextAccessor.Set(previousContext);
            TenantContext.CurrentTenantId = previousTenantId;
        }
    }

    /// <summary>Loads the active ruleset JSON for a workflow.</summary>
    private async Task<string> GetRuleSetJsonAsync(string workflowName, CancellationToken ct)
    {
        string? json = await ruleSetStore.GetAsync(workflowName, version: null, ct);
        return MGuard.Found(string.IsNullOrWhiteSpace(json) ? null : json, "Ruleset", workflowName);
    }

    /// <summary>Evaluates whether the result matched the expected behavior.</summary>
    internal static bool EvaluateExpectation(string? expectedBehavior, OrchestratorResult result)
    {
        if (string.IsNullOrWhiteSpace(expectedBehavior)) return true;

        string expected = expectedBehavior.ToLowerInvariant().Trim();

        if (expected.StartsWith("should pass") || expected.StartsWith("should succeed"))
            return result.IsSuccess;

        if (expected.StartsWith("should fail"))
            return !result.IsSuccess;

        return result.IsSuccess;
    }
}
