namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Filters out duplicate or near-duplicate scenarios based on input facts
/// to avoid wasting execution budget on redundant test cases.
/// </summary>
public interface IScenarioDeduplicator
{
    /// <summary>Removes duplicate or near-duplicate scenarios.</summary>
    IReadOnlyList<NeuronScenario> Deduplicate(
        IReadOnlyList<NeuronScenario> candidates,
        IReadOnlyList<NeuronScenario> existingScenarios);
}
