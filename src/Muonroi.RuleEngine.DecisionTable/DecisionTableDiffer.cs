using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable;

public sealed class DecisionTableDiffer
{
    public DecisionTableDiff Compute(DecisionTableModel from, DecisionTableModel to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        List<ColumnChange> columnChanges = ComputeColumnChanges(from, to);
        List<RowDiff> rowDiffs = ComputeRowDiffs(from, to);

        return new DecisionTableDiff
        {
            FromVersion = from.Version,
            ToVersion = to.Version,
            ColumnChanges = columnChanges,
            RowDiffs = rowDiffs
        };
    }

    private static List<ColumnChange> ComputeColumnChanges(DecisionTableModel from, DecisionTableModel to)
    {
        Dictionary<string, DecisionTableColumn> fromColumns = GetColumns(from);
        Dictionary<string, DecisionTableColumn> toColumns = GetColumns(to);

        List<ColumnChange> changes = [];
        foreach ((string name, DecisionTableColumn column) in fromColumns)
        {
            if (!toColumns.TryGetValue(name, out DecisionTableColumn? target))
            {
                changes.Add(new ColumnChange
                {
                    Kind = DiffKind.Removed,
                    ColumnName = name,
                    OldLabel = column.Label
                });
                continue;
            }

            if (!string.Equals(column.Label, target.Label, StringComparison.Ordinal) ||
                !string.Equals(column.DataType, target.DataType, StringComparison.OrdinalIgnoreCase))
            {
                changes.Add(new ColumnChange
                {
                    Kind = DiffKind.Modified,
                    ColumnName = name,
                    OldLabel = column.Label,
                    NewLabel = target.Label
                });
            }
        }

        foreach ((string name, DecisionTableColumn column) in toColumns)
        {
            if (fromColumns.ContainsKey(name))
            {
                continue;
            }

            changes.Add(new ColumnChange
            {
                Kind = DiffKind.Added,
                ColumnName = name,
                NewLabel = column.Label
            });
        }

        return [.. changes.OrderBy(x => x.ColumnName, StringComparer.OrdinalIgnoreCase)];
    }

    private static Dictionary<string, DecisionTableColumn> GetColumns(DecisionTableModel table)
    {
        return table.InputColumns
            .Concat(table.OutputColumns)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    private static List<RowDiff> ComputeRowDiffs(DecisionTableModel from, DecisionTableModel to)
    {
        Dictionary<string, DecisionTableRow> fromRows = from.Rows.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, DecisionTableRow> toRows = to.Rows.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        List<RowDiff> diffs = [];
        foreach ((string id, DecisionTableRow row) in fromRows)
        {
            if (!toRows.ContainsKey(id))
            {
                diffs.Add(new RowDiff
                {
                    Kind = DiffKind.Removed,
                    RowId = id,
                    RowOrder = row.Order
                });
            }
        }

        foreach ((string id, DecisionTableRow row) in toRows)
        {
            if (!fromRows.TryGetValue(id, out DecisionTableRow? source))
            {
                diffs.Add(new RowDiff
                {
                    Kind = DiffKind.Added,
                    RowId = id,
                    RowOrder = row.Order
                });
                continue;
            }

            List<CellDiff> cellDiffs = ComputeCellDiffs(from, to, source, row);
            if (cellDiffs.Count == 0)
            {
                continue;
            }

            diffs.Add(new RowDiff
            {
                Kind = DiffKind.Modified,
                RowId = id,
                RowOrder = row.Order,
                CellDiffs = cellDiffs
            });
        }

        return [.. diffs
            .OrderBy(x => x.RowOrder ?? int.MaxValue)
            .ThenBy(x => x.RowId, StringComparer.OrdinalIgnoreCase)];
    }

    private static List<CellDiff> ComputeCellDiffs(
        DecisionTableModel fromTable,
        DecisionTableModel toTable,
        DecisionTableRow fromRow,
        DecisionTableRow toRow)
    {
        Dictionary<string, string?> fromCells = BuildCellMap(fromTable, fromRow);
        Dictionary<string, string?> toCells = BuildCellMap(toTable, toRow);
        HashSet<string> keys = [.. fromCells.Keys.Concat(toCells.Keys)];

        List<CellDiff> diffs = [];
        foreach (string key in keys)
        {
            fromCells.TryGetValue(key, out string? oldValue);
            toCells.TryGetValue(key, out string? newValue);
            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                continue;
            }

            diffs.Add(new CellDiff
            {
                ColumnName = key,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        return [.. diffs.OrderBy(x => x.ColumnName, StringComparer.OrdinalIgnoreCase)];
    }

    private static Dictionary<string, string?> BuildCellMap(DecisionTableModel table, DecisionTableRow row)
    {
        Dictionary<string, string?> map = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < table.InputColumns.Count && i < row.InputCells.Count; i++)
        {
            map[table.InputColumns[i].Name] = row.InputCells[i].Expression;
        }

        for (int i = 0; i < table.OutputColumns.Count && i < row.OutputCells.Count; i++)
        {
            map[table.OutputColumns[i].Name] = row.OutputCells[i].Expression;
        }

        return map;
    }
}
