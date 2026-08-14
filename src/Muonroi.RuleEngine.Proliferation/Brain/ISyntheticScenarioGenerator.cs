namespace Muonroi.RuleEngine.Proliferation.Brain;

/// <summary>
/// Generates deterministic boundary scenarios from a ruleset schema without AI.
/// Used as fallback when AI fails to return any scenarios after all retries.
/// </summary>
public interface ISyntheticScenarioGenerator
{
    /// <summary>
    /// Generate synthetic boundary scenarios from the given schema.
    /// Always returns at least 1 scenario, even if schema has no fields.
    /// </summary>
    IReadOnlyList<NeuronScenario> Generate(
        string seedRuleCode,
        RuleSetSchema schema,
        ProliferationContext context);
}
