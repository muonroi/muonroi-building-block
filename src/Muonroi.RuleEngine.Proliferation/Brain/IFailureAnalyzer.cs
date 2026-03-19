using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Analyzes failed scenarios and generates follow-up scenarios
/// that probe the failure boundary for smarter test coverage.
/// </summary>
public interface IFailureAnalyzer
{
    Task<IReadOnlyList<NeuronScenario>> AnalyzeFailureAsync(
        NeuronScenario failedScenario,
        ScenarioResult failureResult,
        string ruleSetJson,
        ProliferationContext context,
        CancellationToken ct = default);
}
