using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation;

public interface IScenarioExecutor
{
    Task<ScenarioResult> ExecuteAsync(NeuronScenario scenario, CancellationToken ct = default);
}
