namespace Muonroi.RuleEngine.DecisionTable.Models;

public sealed class DecisionTableDiff
{
    public int FromVersion { get; init; }
    public int ToVersion { get; init; }
    public IReadOnlyList<ColumnChange> ColumnChanges { get; init; } = [];
    public IReadOnlyList<RowDiff> RowDiffs { get; init; } = [];
    public bool HasChanges => ColumnChanges.Any() || RowDiffs.Any();
}

public sealed class RowDiff
{
    public DiffKind Kind { get; init; }
    public int? RowOrder { get; init; }
    public string? RowId { get; init; }
    public IReadOnlyList<CellDiff> CellDiffs { get; init; } = [];
}

public sealed class CellDiff
{
    public string ColumnName { get; init; } = string.Empty;
    public string? OldValue { get; init; }
    public string? NewValue { get; init; }
}

public sealed class ColumnChange
{
    public DiffKind Kind { get; init; }
    public string ColumnName { get; init; } = string.Empty;
    public string? OldLabel { get; init; }
    public string? NewLabel { get; init; }
}

public enum DiffKind
{
    Added,
    Removed,
    Modified
}
