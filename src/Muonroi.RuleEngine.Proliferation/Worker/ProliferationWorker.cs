using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Proliferation.Brain;
using Muonroi.RuleEngine.Proliferation.Models;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Muonroi.RuleEngine.Proliferation.Worker;

/// <summary>
/// Background worker that generates and executes proliferation scenarios.
/// </summary>
public sealed class ProliferationWorker(
    IServiceScopeFactory scopeFactory,
    ProliferationOptions options,
    IMLog<ProliferationWorker>? logger = null) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("Muonroi.RuleEngine.Proliferation");
    private static readonly Meter Meter = new("Muonroi.RuleEngine.Proliferation");
    private static readonly Counter<long> ScenariosGenerated = Meter.CreateCounter<long>("proliferation.scenarios_generated");
    private static readonly Counter<long> ScenariosExecuted = Meter.CreateCounter<long>("proliferation.scenarios_executed");
    private static readonly Counter<long> ScenariosPassed = Meter.CreateCounter<long>("proliferation.scenarios_passed");
    private static readonly Counter<long> ScenariosFailed = Meter.CreateCounter<long>("proliferation.scenarios_failed");
    private static readonly Counter<long> FeedbackGenerated = Meter.CreateCounter<long>("proliferation.feedback_generated");

    private int _consecutiveErrors;

    /// <summary>Runs the worker loop until cancellation.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger?.Info("[Proliferation] Worker started. Interval={Interval}s, MaxDepth={MaxDepth}, MaxTotal={MaxTotal}",
            options.WorkerIntervalSeconds, options.MaxGenerationDepth, options.MaxTotalScenarios);

        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
                _consecutiveErrors = 0;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveErrors++;
                logger?.Error(ex, "[Proliferation] Cycle error (consecutive: {Count})", _consecutiveErrors);

                if (_consecutiveErrors >= 3)
                {
                    logger?.Warn("[Proliferation] Backing off after {Count} consecutive errors. Waiting 60s.", _consecutiveErrors);
                    await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(options.WorkerIntervalSeconds), stoppingToken);
        }

        logger?.Info("[Proliferation] Worker stopped.");
    }

    /// <summary>Runs a single proliferation cycle.</summary>
    internal async Task RunCycleAsync(CancellationToken ct)
    {
        using Activity? activity = ActivitySource.StartActivity("ProliferationCycle");
        using IServiceScope scope = scopeFactory.CreateScope();
        IProliferationStore store = scope.ServiceProvider.GetRequiredService<IProliferationStore>();
        IScenarioExecutor executor = scope.ServiceProvider.GetRequiredService<IScenarioExecutor>();
        IRuleProliferationBrain brain = scope.ServiceProvider.GetRequiredService<IRuleProliferationBrain>();
        IProliferationChangeNotifier? notifier = scope.ServiceProvider.GetService<IProliferationChangeNotifier>();
        IFailureAnalyzer? failureAnalyzer = scope.ServiceProvider.GetService<IFailureAnalyzer>();
        ICoverageTracker? coverageTracker = scope.ServiceProvider.GetService<ICoverageTracker>();
        IBudgetAllocator? budgetAllocator = scope.ServiceProvider.GetService<IBudgetAllocator>();

        ProliferationStats stats = await store.GetStatsAsync(ct: ct);
        if (stats.TotalScenarios >= options.MaxTotalScenarios)
        {
            logger?.Info("[Proliferation] Max total scenarios ({Max}) reached. Skipping cycle.", options.MaxTotalScenarios);
            return;
        }

        IReadOnlyList<NeuronScenario> pending = await store.GetPendingScenariosAsync(limit: 10, ct: ct);
        if (pending.Count == 0)
        {
            logger?.Debug("[Proliferation] No pending scenarios.");
            return;
        }

        logger?.Info("[Proliferation] Processing {Count} pending scenarios.", pending.Count);

        foreach (NeuronScenario scenario in pending)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            string ruleSetJson = scenario.GeneratedRuleFlowGraph ?? "{}";
            RuleSetKind kind = RuleSetKindDetector.Detect(ruleSetJson);
            RuleSetSchema ruleSchema = RuleSetSchemaExtractor.Extract(ruleSetJson, kind);

            if (ruleSchema.InputFields.Count > 0)
            {
                ValidationResult validation = ScenarioInputValidator.Validate(scenario, ruleSchema);
                if (!validation.IsValid)
                {
                    logger?.Info("[Proliferation] Scenario {Id} skipped: {Errors}",
                        scenario.Id, string.Join("; ", validation.Errors));
                    await store.UpdateStatusAsync(scenario.Id, ScenarioStatus.Skipped, ct);
                    continue;
                }
            }

            await store.UpdateStatusAsync(scenario.Id, ScenarioStatus.Running, ct);

            try
            {
                ScenarioResult result = await executor.ExecuteAsync(scenario, ct);
                ScenariosExecuted.Add(1);

                await store.SaveResultAsync(result, ct);

                ScenarioStatus finalStatus = result.IsSuccess ? ScenarioStatus.Passed : ScenarioStatus.Failed;
                await store.UpdateStatusAsync(scenario.Id, finalStatus, ct);

                if (result.IsSuccess)
                {
                    ScenariosPassed.Add(1);
                }
                else
                {
                    ScenariosFailed.Add(1);
                }

                logger?.Debug("[Proliferation] Scenario {Id} -> {Status} (matches: {Matches})",
                    scenario.Id, finalStatus, result.MatchesExpectation);

                if (notifier is not null)
                {
                    try
                    {
                        await notifier.NotifyScenarioCompletedAsync(scenario.Id, result.IsSuccess, ct);
                    }
                    catch (Exception ex)
                    {
                        logger?.Warn("[Proliferation] Notifier error on scenario completion: {Error}", ex.Message);
                    }
                }

                if (result.IsSuccess
                    && scenario.GenerationDepth < options.MaxGenerationDepth
                    && stats.TotalScenarios < options.MaxTotalScenarios)
                {
                    await GenerateNextGenerationAsync(scenario, result, stats, store, brain, notifier, scope.ServiceProvider.GetService<IScenarioDeduplicator>(), coverageTracker, budgetAllocator, ct);
                }

                if (!result.IsSuccess
                    && scenario.GenerationDepth < options.MaxGenerationDepth
                    && stats.TotalScenarios < options.MaxTotalScenarios
                    && failureAnalyzer is not null)
                {
                    await AnalyzeFailureAndSaveAsync(scenario, result, stats, store, failureAnalyzer, scope.ServiceProvider.GetService<IScenarioDeduplicator>(), ct);
                }
            }
            catch (Exception ex)
            {
                logger?.Warn("[Proliferation] Error executing scenario {Id}: {Error}", scenario.Id, ex.Message);
                await store.UpdateStatusAsync(scenario.Id, ScenarioStatus.Error, ct);
                _consecutiveErrors++;

                if (_consecutiveErrors >= 3)
                {
                    break;
                }
            }
        }
    }

    /// <summary>Generates follow-up scenarios after a successful run.</summary>
    private async Task GenerateNextGenerationAsync(
        NeuronScenario parentScenario,
        ScenarioResult result,
        ProliferationStats currentStats,
        IProliferationStore store,
        IRuleProliferationBrain brain,
        IProliferationChangeNotifier? notifier,
        IScenarioDeduplicator? deduplication,
        ICoverageTracker? coverageTracker,
        IBudgetAllocator? budgetAllocator,
        CancellationToken ct)
    {
        int remainingBudget = options.MaxTotalScenarios - currentStats.TotalScenarios;
        if (remainingBudget <= 0)
        {
            return;
        }

        int perRuleBudget = options.MaxScenariosPerRule;
        if (options.EnableSmartBudget && budgetAllocator is not null)
        {
            double coveragePct = 0;
            double failureRate = 0;
            try
            {
                string nextRuleSetJsonForBudget = parentScenario.GeneratedRuleFlowGraph ?? "{}";
                RuleSetKind kindForBudget = RuleSetKindDetector.Detect(nextRuleSetJsonForBudget);
                RuleSetSchema schemaForBudget = RuleSetSchemaExtractor.Extract(nextRuleSetJsonForBudget, kindForBudget);
                if (coverageTracker is not null && schemaForBudget.InputFields.Count > 0)
                {
                    IReadOnlyList<NeuronScenario> siblingsForBudget = await store.GetScenariosBySeedAsync(parentScenario.SeedRuleCode, ct);
                    CoverageReport coverageForBudget = coverageTracker.GetCoverage(parentScenario.SeedRuleCode, siblingsForBudget, schemaForBudget);
                    coveragePct = coverageForBudget.CoveragePercentage;
                    int totalForBudget = siblingsForBudget.Count;
                    int failedForBudget = siblingsForBudget.Count(s => s.Status == ScenarioStatus.Failed);
                    failureRate = totalForBudget > 0 ? (double)failedForBudget / totalForBudget * 100 : 0;
                }
            }
            catch
            {
            }

            perRuleBudget = budgetAllocator.AllocateBudget(parentScenario.SeedRuleCode, coveragePct, failureRate, perRuleBudget);
        }

        try
        {
            IReadOnlyList<string>? coverageFocus = null;
            string nextRuleSetJson = parentScenario.GeneratedRuleFlowGraph ?? "{}";
            if (coverageTracker is not null)
            {
                RuleSetKind nextKind = RuleSetKindDetector.Detect(nextRuleSetJson);
                RuleSetSchema nextSchema = RuleSetSchemaExtractor.Extract(nextRuleSetJson, nextKind);
                if (nextSchema.InputFields.Count > 0)
                {
                    IReadOnlyList<NeuronScenario> siblings = await store.GetScenariosBySeedAsync(parentScenario.SeedRuleCode, ct);
                    CoverageReport coverage = coverageTracker.GetCoverage(parentScenario.SeedRuleCode, siblings, nextSchema);
                    if (coverage.UncoveredFields.Count > 0)
                    {
                        coverageFocus = coverage.UncoveredFields;
                        logger?.Info("[Proliferation] Coverage {Pct}% - focusing on uncovered fields: {Fields}",
                            coverage.CoveragePercentage, string.Join(", ", coverage.UncoveredFields));
                    }
                }
            }

            ProliferationPlan plan = await brain.AnalyzeAsync(
                parentScenario.SeedRuleCode,
                ruleSetJson: nextRuleSetJson,
                executionResult: result.OutputFacts,
                factBagSnapshot: result.OutputFacts,
                new ProliferationContext
                {
                    Scope = parentScenario.Scope,
                    CurrentDepth = parentScenario.GenerationDepth + 1,
                    RemainingBudget = Math.Min(remainingBudget, perRuleBudget),
                    TenantId = parentScenario.TenantId,
                    FocusAreas = coverageFocus
                },
                ct);

            if (plan.Scenarios.Count > 0)
            {
                List<NeuronScenario> children = [.. plan.Scenarios.Select(s => s with
                {
                    ParentScenarioId = parentScenario.Id,
                    GenerationDepth = parentScenario.GenerationDepth + 1
                })];

                if (deduplication is not null)
                {
                    IReadOnlyList<NeuronScenario> existing = await store.GetScenariosBySeedAsync(parentScenario.SeedRuleCode, ct);
                    children = [.. deduplication.Deduplicate(children, existing)];
                }

                if (children.Count > 0)
                {
                    await store.SaveScenariosAsync(children, ct);
                    ScenariosGenerated.Add(children.Count);

                    logger?.Info("[Proliferation] Generated {Count} children from scenario {ParentId} (depth {Depth})",
                        children.Count, parentScenario.Id, parentScenario.GenerationDepth + 1);

                    if (notifier is not null)
                    {
                        try
                        {
                            await notifier.NotifyProliferationTriggeredAsync(
                                parentScenario.SeedRuleCode, children.Count, ct);
                        }
                        catch (Exception ex)
                        {
                            logger?.Warn("[Proliferation] Notifier error on trigger: {Error}", ex.Message);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.Warn("[Proliferation] Failed to generate next generation from {ParentId}: {Error}",
                parentScenario.Id, ex.Message);
        }
    }

    /// <summary>Analyzes a failed scenario and persists follow-up scenarios.</summary>
    private async Task AnalyzeFailureAndSaveAsync(
        NeuronScenario failedScenario,
        ScenarioResult failureResult,
        ProliferationStats currentStats,
        IProliferationStore store,
        IFailureAnalyzer failureAnalyzer,
        IScenarioDeduplicator? deduplicator,
        CancellationToken ct)
    {
        int remainingBudget = options.MaxTotalScenarios - currentStats.TotalScenarios;
        if (remainingBudget <= 0)
        {
            return;
        }

        try
        {
            string ruleSetJson = failedScenario.GeneratedRuleFlowGraph ?? "{}";
            ProliferationContext context = new()
            {
                Scope = failedScenario.Scope,
                CurrentDepth = failedScenario.GenerationDepth + 1,
                RemainingBudget = Math.Min(remainingBudget, options.MaxScenariosPerFailure),
                TenantId = failedScenario.TenantId
            };

            IReadOnlyList<NeuronScenario> children = await failureAnalyzer.AnalyzeFailureAsync(
                failedScenario, failureResult, ruleSetJson, context, ct);

            if (children.Count == 0)
            {
                return;
            }

            List<NeuronScenario> toSave = [.. children];
            if (deduplicator is not null)
            {
                IReadOnlyList<NeuronScenario> existing = await store.GetScenariosBySeedAsync(failedScenario.SeedRuleCode, ct);
                toSave = [.. deduplicator.Deduplicate(toSave, existing)];
            }

            if (toSave.Count > 0)
            {
                await store.SaveScenariosAsync(toSave, ct);
                FeedbackGenerated.Add(toSave.Count);
                ScenariosGenerated.Add(toSave.Count);

                logger?.Info("[Proliferation] Failure feedback generated {Count} scenarios from failed scenario {ParentId}",
                    toSave.Count, failedScenario.Id);
            }
        }
        catch (Exception ex)
        {
            logger?.Warn("[Proliferation] Failure analysis error for scenario {Id}: {Error}",
                failedScenario.Id, ex.Message);
        }
    }
}
