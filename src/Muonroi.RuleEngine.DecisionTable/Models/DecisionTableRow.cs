namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Represents a single decision table row.
/// </summary>
public sealed class DecisionTableRow
{
    /// <summary>Unique identifier for the row.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Ordering index used for evaluation priority.</summary>
    public int Order { get; set; }
    /// <summary>Optional row description.</summary>
    public string? Description { get; set; }
    /// <summary>Input cells for the row.</summary>
    public List<DecisionTableCell> InputCells { get; set; } = [];
    /// <summary>Output cells for the row.</summary>
    public List<DecisionTableCell> OutputCells { get; set; } = [];
    /// <summary>Indicates whether the row is active.</summary>
    public bool IsEnabled { get; set; } = true;
}
