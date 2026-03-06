namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

public sealed class DecisionTableBulkUpsertRequest
{
    public IReadOnlyList<DecisionTableModel> Tables { get; init; } = [];
    public string? Actor { get; init; }
    public string? Reason { get; init; }
}

public sealed class DecisionTableBulkDeleteRequest
{
    public IReadOnlyList<string> Ids { get; init; } = [];
    public string? Actor { get; init; }
    public string? Reason { get; init; }
}

public sealed class DecisionTableRowReorderRequest
{
    public IReadOnlyList<string> RowIds { get; init; } = [];
    public string? Actor { get; init; }
    public string? Reason { get; init; }
}
