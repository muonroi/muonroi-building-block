namespace Muonroi.RuleEngine.Proliferation;

/// <summary>
/// Analyzes a rule set and proposes proliferation scenarios.
/// </summary>
public interface IRuleProliferationBrain
{
    /// <summary>Analyzes the provided rule set and context.</summary>
    Task<ProliferationPlan> AnalyzeAsync(
        string seedRuleCode,
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        ProliferationContext context,
        CancellationToken ct = default);
}
