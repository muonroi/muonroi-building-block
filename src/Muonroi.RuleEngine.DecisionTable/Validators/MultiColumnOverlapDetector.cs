using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Validators;

public sealed class MultiColumnOverlapDetector
{
    public IReadOnlyList<ConflictReport> Detect(DecisionTableModel table)
    {
        if (table.HitPolicy != HitPolicy.Unique)
        {
            return [];
        }

        List<ConflictReport> conflicts = [];
        DecisionTableRow[] rows = [.. table.Rows
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)];

        for (int i = 0; i < rows.Length; i++)
        {
            for (int j = i + 1; j < rows.Length; j++)
            {
                List<string> overlappingColumns = [];
                int count = Math.Min(table.InputColumns.Count, Math.Min(rows[i].InputCells.Count, rows[j].InputCells.Count));
                bool hasOverlap = true;

                for (int c = 0; c < count; c++)
                {
                    CellExpression left = DecisionTableExpressionEvaluator.ParseCellExpression(rows[i].InputCells[c].Expression);
                    CellExpression right = DecisionTableExpressionEvaluator.ParseCellExpression(rows[j].InputCells[c].Expression);
                    if (!CellExpressionsCanOverlap(left, right))
                    {
                        hasOverlap = false;
                        break;
                    }

                    overlappingColumns.Add(table.InputColumns[c].Name);
                }

                if (!hasOverlap)
                {
                    continue;
                }

                string columns = overlappingColumns.Count == 0
                    ? "all input columns"
                    : string.Join(", ", overlappingColumns);
                conflicts.Add(new ConflictReport
                {
                    Row1Index = rows[i].Order,
                    Row2Index = rows[j].Order,
                    Description = $"Rows '{rows[i].Id}' and '{rows[j].Id}' overlap on {columns}."
                });
            }
        }

        return conflicts;
    }

    private static bool CellExpressionsCanOverlap(CellExpression a, CellExpression b)
    {
        if (a.IsWildcard || b.IsWildcard)
        {
            return true;
        }

        if (a.IsRange && b.IsRange)
        {
            double aMin = a.Min ?? double.MinValue;
            double aMax = a.Max ?? double.MaxValue;
            double bMin = b.Min ?? double.MinValue;
            double bMax = b.Max ?? double.MaxValue;
            return aMin <= bMax && bMin <= aMax;
        }

        if (a.Values.Count > 0 && b.Values.Count > 0)
        {
            return a.Values.Any(x => b.Values.Contains(x, StringComparer.OrdinalIgnoreCase));
        }

        if (a.IsRange && b.Values.Count > 0)
        {
            return b.Values.Any(v => InRange(v, a));
        }

        if (b.IsRange && a.Values.Count > 0)
        {
            return a.Values.Any(v => InRange(v, b));
        }

        return string.Equals(a.Raw, b.Raw, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InRange(string rawValue, CellExpression range)
    {
        if (!double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
        {
            return false;
        }

        if (range.Min.HasValue)
        {
            bool minPass = range.IncludeMin ? num >= range.Min.Value : num > range.Min.Value;
            if (!minPass)
            {
                return false;
            }
        }

        if (!range.Max.HasValue)
        {
            return true;
        }

        return range.IncludeMax ? num <= range.Max.Value : num < range.Max.Value;
    }
}

public sealed class ConflictReport
{
    public int Row1Index { get; init; }
    public int Row2Index { get; init; }
    public string Description { get; init; } = string.Empty;
}
