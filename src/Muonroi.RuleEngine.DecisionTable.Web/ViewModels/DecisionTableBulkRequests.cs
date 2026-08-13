namespace Muonroi.RuleEngine.DecisionTable.Web.ViewModels;

/// <summary>
/// Bulk create or update request for decision tables.
/// </summary>
public sealed class DecisionTableBulkUpsertRequest
{
    /// <summary>
    /// Decision tables to upsert.
    /// </summary>
    public IReadOnlyList<DecisionTableModel> Tables { get; init; } = [];
    /// <summary>
    /// Optional actor identity.
    /// </summary>
    public string? Actor { get; init; }
    /// <summary>
    /// Optional reason for the change.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Bulk delete request for decision tables.
/// </summary>
public sealed class DecisionTableBulkDeleteRequest
{
    /// <summary>
    /// Identifiers to delete.
    /// </summary>
    public IReadOnlyList<string> Ids { get; init; } = [];
    /// <summary>
    /// Optional actor identity.
    /// </summary>
    public string? Actor { get; init; }
    /// <summary>
    /// Optional reason for the change.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Row reorder request for a decision table.
/// </summary>
public sealed class DecisionTableRowReorderRequest
{
    /// <summary>
    /// Row identifiers in the desired order.
    /// </summary>
    public IReadOnlyList<string> RowIds { get; init; } = [];
    /// <summary>
    /// Optional actor identity.
    /// </summary>
    public string? Actor { get; init; }
    /// <summary>
    /// Optional reason for the change.
    /// </summary>
    public string? Reason { get; init; }
}
