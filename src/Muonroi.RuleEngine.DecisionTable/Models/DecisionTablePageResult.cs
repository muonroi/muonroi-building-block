namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Paged result for decision table queries.
/// </summary>
public sealed class DecisionTablePageResult
{
    /// <summary>Current page items.</summary>
    public IReadOnlyList<DecisionTableModel> Items { get; init; } = [];
    /// <summary>Current page number.</summary>
    public int Page { get; init; } = 1;
    /// <summary>Page size.</summary>
    public int PageSize { get; init; } = 20;
    /// <summary>Total number of items.</summary>
    public int Total { get; init; }
}
