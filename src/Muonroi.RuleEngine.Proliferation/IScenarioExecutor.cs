using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Executes a single proliferation scenario.
/// </summary>
public interface IScenarioExecutor
{
    /// <summary>Runs the supplied scenario.</summary>
    Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, CancellationToken ct = default);
}
