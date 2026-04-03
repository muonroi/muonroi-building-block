namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Defines an input or output column within a decision table.
/// </summary>
public sealed class DecisionTableColumn
{
    /// <summary>Unique identifier for the column.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>Column name used as a key.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Display label for the column.</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Declared data type for the column.</summary>
    public string DataType { get; set; } = "string";
    /// <summary>Indicates whether the column must have a value.</summary>
    public bool IsRequired { get; set; } = true;
}
