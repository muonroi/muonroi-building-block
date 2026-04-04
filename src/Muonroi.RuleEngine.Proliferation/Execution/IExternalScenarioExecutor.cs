using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Execution;

/// <summary>
/// Executes a scenario against an external project's HTTP endpoint.
/// Separated from IScenarioExecutor to allow explicit external execution with config.
/// </summary>
public interface IExternalScenarioExecutor
{
    /// <summary>
    /// Executes the scenario against the external endpoint described in config.
    /// </summary>
    Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, ExternalProjectConfig config, CancellationToken ct = default);
}
