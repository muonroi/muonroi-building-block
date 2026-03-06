namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableVersionSnapshot
{
    public string TableId { get; init; } = string.Empty;
    public int Version { get; init; }
    public string ChangeType { get; init; } = string.Empty;
    public string? Actor { get; init; }
    public string? Reason { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public required DecisionTableModel Table { get; init; }
}
