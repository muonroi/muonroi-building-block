using System.Collections.Concurrent;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Store;

public sealed class InMemoryProliferationStore : IProliferationStore
{
    private readonly ConcurrentDictionary<string, NeuronScenario> _scenarios = new();
    private readonly ConcurrentDictionary<string, ScenarioResult> _results = new();

    public Task SaveScenariosAsync(IReadOnlyList<NeuronScenario> scenarios, CancellationToken ct = default)
    {
        foreach (NeuronScenario scenario in scenarios)
        {
            _scenarios[scenario.Id] = scenario;
        }
        return Task.CompletedTask;
    }

    public Task SaveResultAsync(ScenarioResult result, CancellationToken ct = default)
    {
        _results[result.ScenarioId] = result;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NeuronScenario>> GetPendingScenariosAsync(int limit = 10, CancellationToken ct = default)
    {
        IReadOnlyList<NeuronScenario> pending = _scenarios.Values
            .Where(s => s.Status == ScenarioStatus.Pending)
            .OrderBy(s => s.CreatedAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task<IReadOnlyList<NeuronScenario>> GetScenariosBySeedAsync(string seedRuleCode, CancellationToken ct = default)
    {
        IReadOnlyList<NeuronScenario> result = _scenarios.Values
            .Where(s => s.SeedRuleCode == seedRuleCode)
            .OrderBy(s => s.CreatedAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task UpdateStatusAsync(string scenarioId, ScenarioStatus status, CancellationToken ct = default)
    {
        if (_scenarios.TryGetValue(scenarioId, out NeuronScenario? existing))
        {
            _scenarios[scenarioId] = existing with { Status = status };
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RuleLineage>> GetLineageAsync(string seedRuleCode, CancellationToken ct = default)
    {
        IReadOnlyList<RuleLineage> lineage = _scenarios.Values
            .Where(s => s.SeedRuleCode == seedRuleCode)
            .Select(s => new RuleLineage
            {
                ScenarioId = s.Id,
                SeedRuleCode = s.SeedRuleCode,
                ParentScenarioId = s.ParentScenarioId,
                Depth = s.GenerationDepth,
                Reason = s.ProliferationReason,
                CreatedAt = s.CreatedAt
            })
            .OrderBy(l => l.Depth)
            .ThenBy(l => l.CreatedAt)
            .ToList();
        return Task.FromResult(lineage);
    }

    public Task<ProliferationStats> GetStatsAsync(string? seedRuleCode = null, CancellationToken ct = default)
    {
        IEnumerable<NeuronScenario> query = _scenarios.Values;
        if (seedRuleCode is not null)
            query = query.Where(s => s.SeedRuleCode == seedRuleCode);

        List<NeuronScenario> all = query.ToList();
        ProliferationStats stats = new()
        {
            TotalScenarios = all.Count,
            Passed = all.Count(s => s.Status == ScenarioStatus.Passed),
            Failed = all.Count(s => s.Status == ScenarioStatus.Failed),
            Pending = all.Count(s => s.Status == ScenarioStatus.Pending),
            MaxDepthReached = all.Count > 0 ? all.Max(s => s.GenerationDepth) : 0,
            BySeedRule = all.GroupBy(s => s.SeedRuleCode)
                .ToDictionary(g => g.Key, g => g.Count())
        };
        return Task.FromResult(stats);
    }
}
