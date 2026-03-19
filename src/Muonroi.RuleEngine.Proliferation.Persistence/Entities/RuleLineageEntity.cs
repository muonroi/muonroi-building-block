namespace Muonroi.RuleEngine.Proliferation.Persistence.Entities;

public sealed class RuleLineageEntity
{
    public string ScenarioId { get; set; } = string.Empty;
    public string SeedRuleCode { get; set; } = string.Empty;
    public string? ParentScenarioId { get; set; }
    public int Depth { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
