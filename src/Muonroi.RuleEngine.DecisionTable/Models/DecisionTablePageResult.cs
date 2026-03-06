namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTablePageResult
{
    public IReadOnlyList<DecisionTableModel> Items { get; init; } = [];
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int Total { get; init; }
}
