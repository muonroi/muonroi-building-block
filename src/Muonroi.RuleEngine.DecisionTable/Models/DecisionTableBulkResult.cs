namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableBulkResult
{
    public int ProcessedCount { get; init; }
    public IReadOnlyList<string> Ids { get; init; } = [];
}
