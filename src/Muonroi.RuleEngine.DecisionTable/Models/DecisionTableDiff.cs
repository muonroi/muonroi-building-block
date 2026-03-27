namespace Muonroi.RuleEngine.DecisionTable.Models;

/// <summary>
/// Represents changes between two table versions.
/// </summary>
public sealed class DecisionTableDiff
{
    /// <summary>Source version number.</summary>
    public int FromVersion { get; init; }
    /// <summary>Target version number.</summary>
    public int ToVersion { get; init; }
    /// <summary>Column-level changes.</summary>
    public IReadOnlyList<ColumnChange> ColumnChanges { get; init; } = [];
    /// <summary>Row-level changes.</summary>
    public IReadOnlyList<RowDiff> RowDiffs { get; init; } = [];
    /// <summary>True when any changes are present.</summary>
    public bool HasChanges => ColumnChanges.Any() || RowDiffs.Any();
}

/// <summary>
/// Describes changes to a single row.
/// </summary>
public sealed class RowDiff
{
    /// <summary>Diff kind for the row.</summary>
    public DiffKind Kind { get; init; }
    /// <summary>Row order index, if available.</summary>
    public int? RowOrder { get; init; }
    /// <summary>Row identifier, if available.</summary>
    public string? RowId { get; init; }
    /// <summary>Cell-level differences.</summary>
    public IReadOnlyList<CellDiff> CellDiffs { get; init; } = [];
}

/// <summary>
/// Describes changes to a single cell.
/// </summary>
public sealed class CellDiff
{
    /// <summary>Column name associated with the cell.</summary>
    public string ColumnName { get; init; } = string.Empty;
    /// <summary>Previous value.</summary>
    public string? OldValue { get; init; }
    /// <summary>New value.</summary>
    public string? NewValue { get; init; }
}

/// <summary>
/// Describes changes to a column definition.
/// </summary>
public sealed class ColumnChange
{
    /// <summary>Diff kind for the column.</summary>
    public DiffKind Kind { get; init; }
    /// <summary>Column name.</summary>
    public string ColumnName { get; init; } = string.Empty;
    /// <summary>Previous label.</summary>
    public string? OldLabel { get; init; }
    /// <summary>New label.</summary>
    public string? NewLabel { get; init; }
}

/// <summary>
/// Kind of change recorded in a diff.
/// </summary>
public enum DiffKind
{
    /// <summary>Item was added.</summary>
    Added,
    /// <summary>Item was removed.</summary>
    Removed,
    /// <summary>Item was modified.</summary>
    Modified
}
