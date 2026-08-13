using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Models;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.RuleEngine.DecisionTable.Validators;

/// <summary>
/// Detects gaps in range-based input columns.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "LINQ projections on decision table data produce values guaranteed non-null by prior filtering; MGuard.NotNull would be redundant.")]
public sealed class GapDetector
{
    /// <summary>
    /// Detects gaps in the first input column ranges.
    /// </summary>
    /// <param name="table">Decision table to inspect.</param>
    /// <returns>List of gap descriptions.</returns>
    public IReadOnlyList<string> Detect(DecisionTableModel table)
    {
        List<string> gaps = [];
        if (table.InputColumns.Count != 1)
        {
            return gaps;
        }

        CellExpression[] ranges = [.. table.Rows
            .Where(x => x.IsEnabled && x.InputCells.Count > 0)
            .Select(x => DecisionTableExpressionEvaluator.ParseCellExpression(x.InputCells[0].Expression))
            .Where(x => x.IsRange && x.Min.HasValue && x.Max.HasValue)
            .OrderBy(x => x.Min!.Value)];

        if (ranges.Length < 2)
        {
            return gaps;
        }

        for (int i = 0; i < ranges.Length - 1; i++)
        {
            CellExpression current = ranges[i];
            CellExpression next = ranges[i + 1];
            if (current.Max!.Value < next.Min!.Value)
            {
                gaps.Add($"Gap detected between {current.Raw} and {next.Raw}.");
            }
        }

        return gaps;
    }
}
