using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Stores proliferation scenarios, results, and lineage data.
/// </summary>
public interface IProliferationStore
{
    /// <summary>Persists generated scenarios.</summary>
    Task SaveScenariosAsync(IReadOnlyList<NeuronScenario> scenarios, CancellationToken ct = default);
    /// <summary>Persists a completed scenario result.</summary>
    Task SaveResultAsync(ScenarioResult result, CancellationToken ct = default);
    /// <summary>Gets scenarios that are still pending execution.</summary>
    Task<IReadOnlyList<NeuronScenario>> GetPendingScenariosAsync(int limit = 10, CancellationToken ct = default);
    /// <summary>Gets scenarios generated from the specified seed rule.</summary>
    Task<IReadOnlyList<NeuronScenario>> GetScenariosBySeedAsync(string seedRuleCode, CancellationToken ct = default);
    /// <summary>Updates a scenario's execution status.</summary>
    Task UpdateStatusAsync(string scenarioId, ScenarioStatus status, CancellationToken ct = default);
    /// <summary>Gets the result for a specific scenario.</summary>
    Task<ScenarioResult?> GetResultAsync(string scenarioId, CancellationToken ct = default);
    /// <summary>Gets all results associated with a seed rule.</summary>
    Task<IReadOnlyList<ScenarioResult>> GetResultsByWorkflowAsync(string seedRuleCode, CancellationToken ct = default);
    /// <summary>Gets lineage records for a seed rule.</summary>
    Task<IReadOnlyList<RuleLineage>> GetLineageAsync(string seedRuleCode, CancellationToken ct = default);
    /// <summary>Gets aggregate proliferation statistics.</summary>
    Task<ProliferationStats> GetStatsAsync(string? seedRuleCode = null, CancellationToken ct = default);
}
