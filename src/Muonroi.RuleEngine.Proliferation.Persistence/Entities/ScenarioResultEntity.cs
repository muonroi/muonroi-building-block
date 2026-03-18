namespace Muonroi.RuleEngine.Proliferation.Persistence.Entities;

public sealed class ScenarioResultEntity
{
    public string ScenarioId { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public bool MatchesExpectation { get; set; }
    public string? ActualBehavior { get; set; }
    public string? OutputFactsJson { get; set; }
    public string ErrorsJson { get; set; } = "[]";
    public long DurationMs { get; set; }
    public DateTimeOffset ExecutedAt { get; set; }
}
