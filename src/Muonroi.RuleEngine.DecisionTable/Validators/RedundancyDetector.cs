using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Validators;

/// <summary>
/// Detects redundant rows that are shadowed by earlier rows.
/// </summary>
public sealed class RedundancyDetector
{
    /// <summary>
    /// Detects redundant rows when hit policy is First.
    /// </summary>
    /// <param name="table">Decision table to inspect.</param>
    /// <returns>List of redundancy reports.</returns>
    public IReadOnlyList<RedundancyReport> Detect(DecisionTableModel table)
    {
        if (table.HitPolicy != HitPolicy.First)
        {
            return [];
        }

        List<RedundancyReport> redundancies = [];
        DecisionTableRow[] rows = [.. table.Rows
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)];

        for (int i = 1; i < rows.Length; i++)
        {
            DecisionTableRow current = rows[i];
            for (int j = 0; j < i; j++)
            {
                DecisionTableRow previous = rows[j];
                if (!IsCoveredBy(previous, current, table))
                {
                    continue;
                }

                redundancies.Add(new RedundancyReport
                {
                    RowIndex = current.Order,
                    Description = $"Row '{current.Id}' is shadowed by row '{previous.Id}'."
                });
                break;
            }
        }

        return redundancies;
    }

    private static bool IsCoveredBy(DecisionTableRow covering, DecisionTableRow candidate, DecisionTableModel table)
    {
        int count = Math.Min(table.InputColumns.Count, Math.Min(covering.InputCells.Count, candidate.InputCells.Count));
        for (int i = 0; i < count; i++)
        {
            CellExpression coveringExpression = DecisionTableExpressionEvaluator.ParseCellExpression(covering.InputCells[i].Expression);
            CellExpression candidateExpression = DecisionTableExpressionEvaluator.ParseCellExpression(candidate.InputCells[i].Expression);
            if (!Contains(coveringExpression, candidateExpression))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Contains(CellExpression container, CellExpression target)
    {
        if (container.IsWildcard)
        {
            return true;
        }

        if (target.IsWildcard)
        {
            return container.IsWildcard;
        }

        if (container.IsRange && target.IsRange)
        {
            double cMin = container.Min ?? double.MinValue;
            double cMax = container.Max ?? double.MaxValue;
            double tMin = target.Min ?? double.MinValue;
            double tMax = target.Max ?? double.MaxValue;
            return cMin <= tMin && cMax >= tMax;
        }

        if (container.Values.Count > 0 && target.Values.Count > 0)
        {
            return target.Values.All(x => container.Values.Contains(x, StringComparer.OrdinalIgnoreCase));
        }

        if (container.IsRange && target.Values.Count > 0)
        {
            return target.Values.All(v => InRange(v, container));
        }

        return string.Equals(container.Raw, target.Raw, StringComparison.OrdinalIgnoreCase);
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

/// <summary>
/// Describes a redundant row in a decision table.
/// </summary>
public sealed class RedundancyReport
{
    /// <summary>Order index of the redundant row.</summary>
    public int RowIndex { get; init; }
    /// <summary>Human-readable description of the redundancy.</summary>
    public string Description { get; init; } = string.Empty;
}
