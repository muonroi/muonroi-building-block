using Muonroi.RuleEngine.Proliferation.Models;

namespace Muonroi.RuleEngine.Proliferation.Persistence.Entities;

public sealed class NeuronScenarioEntity
{
    public string Id { get; set; } = string.Empty;
    public string SeedRuleCode { get; set; } = string.Empty;
    public string ScenarioName { get; set; } = string.Empty;
    public ScenarioType Type { get; set; }
    public ProliferationScope Scope { get; set; }
    public string? ParentScenarioId { get; set; }
    public int GenerationDepth { get; set; }
    public string ProliferationReason { get; set; } = string.Empty;
    public string InputFactsJson { get; set; } = "{}";
    public string? ExpectedBehavior { get; set; }
    public string? GeneratedRuleFlowGraph { get; set; }
    public ScenarioStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? TenantId { get; set; }
}
