using System.Text.Json;
using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation;

public interface IRuleProliferationBrain
{
    Task<ProliferationPlan> AnalyzeAsync(
        string seedRuleCode,
        string ruleSetJson,
        JsonElement? executionResult,
        JsonElement? factBagSnapshot,
        ProliferationContext context,
        CancellationToken ct = default);
}
