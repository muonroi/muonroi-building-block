using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation;

public interface IProliferationStore
{
    Task SaveScenariosAsync(IReadOnlyList<NeuronScenario> scenarios, CancellationToken ct = default);
    Task SaveResultAsync(ScenarioResult result, CancellationToken ct = default);
    Task<IReadOnlyList<NeuronScenario>> GetPendingScenariosAsync(int limit = 10, CancellationToken ct = default);
    Task<IReadOnlyList<NeuronScenario>> GetScenariosBySeedAsync(string seedRuleCode, CancellationToken ct = default);
    Task UpdateStatusAsync(string scenarioId, ScenarioStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<RuleLineage>> GetLineageAsync(string seedRuleCode, CancellationToken ct = default);
    Task<ProliferationStats> GetStatsAsync(string? seedRuleCode = null, CancellationToken ct = default);
}
