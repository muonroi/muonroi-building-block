using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// A single regression entry — a scenario that passed before but fails now.
/// </summary>
public sealed record RegressionEntry(
    string ScenarioId,
    string ScenarioName,
    string PreviousStatus,
    string CurrentStatus,
    IReadOnlyList<string> Errors);

/// <summary>
/// Regression report after re-running existing scenarios against a new version.
/// </summary>
public sealed record RegressionReport(
    int ScenariosRun,
    int Passed,
    int Failed,
    int NewFailures,
    IReadOnlyList<RegressionEntry> Regressions);

/// <summary>
/// Re-runs previously passed scenarios against a new ruleset version to detect regressions.
/// </summary>
public interface IRegressionRunner
{
    /// <summary>Runs regression checks for the given workflow version.</summary>
    Task<RegressionReport> RunRegressionAsync(
        string workflowName,
        int newVersion,
        CancellationToken ct);
}

/// <summary>
/// Default regression runner implementation.
/// Loads all PASSED scenarios for a workflow, re-executes against new version,
/// and reports any new failures as regressions.
/// </summary>
public sealed class DefaultRegressionRunner(
    IProliferationStore store,
    IScenarioExecutor executor) : IRegressionRunner
{
    /// <summary>Re-runs passed scenarios to detect new failures.</summary>
    public async Task<RegressionReport> RunRegressionAsync(
        string workflowName,
        int newVersion,
        CancellationToken ct)
    {
        // Load all scenarios for this workflow
        IReadOnlyList<NeuronScenario> allScenarios = await store.GetScenariosBySeedAsync(workflowName, ct);

        // Filter only previously passed scenarios
        List<NeuronScenario> passedScenarios = [.. allScenarios.Where(s => s.Status == ScenarioStatus.Passed)];

        if (passedScenarios.Count == 0)
        {
            return new RegressionReport(0, 0, 0, 0, []);
        }

        int passed = 0;
        int failed = 0;
        List<RegressionEntry> regressions = [];

        foreach (NeuronScenario scenario in passedScenarios)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                ScenarioResult result = await executor.ExecuteAsync(scenario, ct);

                if (result.IsSuccess)
                {
                    passed++;
                }
                else
                {
                    failed++;
                    regressions.Add(new RegressionEntry(
                        scenario.Id,
                        scenario.ScenarioName,
                        PreviousStatus: "Passed",
                        CurrentStatus: "Failed",
                        Errors: result.Errors));
                }
            }
            catch (Exception ex)
            {
                failed++;
                regressions.Add(new RegressionEntry(
                    scenario.Id,
                    scenario.ScenarioName,
                    PreviousStatus: "Passed",
                    CurrentStatus: "Error",
                    Errors: [ex.Message]));
            }
        }

        return new RegressionReport(
            ScenariosRun: passedScenarios.Count,
            Passed: passed,
            Failed: failed,
            NewFailures: regressions.Count,
            Regressions: regressions);
    }
}
