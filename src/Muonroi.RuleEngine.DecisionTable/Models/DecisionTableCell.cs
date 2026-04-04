namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Represents a single cell expression in a decision table row.
/// </summary>
public sealed class DecisionTableCell
{
    /// <summary>Identifier of the column this cell belongs to.</summary>
    public string ColumnId { get; set; } = string.Empty;
    /// <summary>Expression or literal value stored in the cell.</summary>
    public string Expression { get; set; } = string.Empty;
    /// <summary>Optional comment for the cell.</summary>
    public string? Comment { get; set; }
}
