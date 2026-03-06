namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableAuditEntry
{
    public long Id { get; init; }
    public string? TableId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Actor { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? PayloadJson { get; init; }
}
