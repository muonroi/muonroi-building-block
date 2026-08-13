namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Result of a bulk decision table operation.
/// </summary>
public sealed class DecisionTableBulkResult
{
    /// <summary>Number of processed items.</summary>
    public int ProcessedCount { get; init; }
    /// <summary>Identifiers of processed tables.</summary>
    public IReadOnlyList<string> Ids { get; init; } = [];
}
