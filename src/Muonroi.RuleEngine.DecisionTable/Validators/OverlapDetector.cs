using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Validators;

public sealed class OverlapDetector
{
    public IReadOnlyList<string> Detect(DecisionTableModel table)
    {
        List<string> overlaps = [];
        DecisionTableRow[] rows = [.. table.Rows
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)];

        for (int i = 0; i < rows.Length; i++)
        {
            for (int j = i + 1; j < rows.Length; j++)
            {
                if (RowsCanOverlap(table, rows[i], rows[j]))
                {
                    overlaps.Add($"{rows[i].Id} overlaps {rows[j].Id}");
                }
            }
        }

        return overlaps;
    }

    private static bool RowsCanOverlap(DecisionTableModel table, DecisionTableRow left, DecisionTableRow right)
    {
        int count = Math.Min(table.InputColumns.Count, Math.Min(left.InputCells.Count, right.InputCells.Count));
        for (int i = 0; i < count; i++)
        {
            CellExpression a = DecisionTableExpressionEvaluator.ParseCellExpression(left.InputCells[i].Expression);
            CellExpression b = DecisionTableExpressionEvaluator.ParseCellExpression(right.InputCells[i].Expression);
            if (!CellExpressionsCanOverlap(a, b))
            {
                return false;
            }
        }

        return true;
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
