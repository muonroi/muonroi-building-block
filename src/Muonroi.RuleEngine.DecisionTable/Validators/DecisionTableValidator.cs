using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Validators;

public sealed class DecisionTableValidator(
    OverlapDetector? overlapDetector = null,
    GapDetector? gapDetector = null)
{
    private readonly OverlapDetector _overlapDetector = overlapDetector ?? new OverlapDetector();
    private readonly GapDetector _gapDetector = gapDetector ?? new GapDetector();

    public ValidationResult Validate(DecisionTableModel table)
    {
        List<string> errors = [];
        List<string> warnings = [];

        if (string.IsNullOrWhiteSpace(table.Name))
        {
            errors.Add("Decision table name is required.");
        }

        if (table.InputColumns.Count == 0)
        {
            errors.Add("At least one input column is required.");
        }

        if (table.OutputColumns.Count == 0)
        {
            errors.Add("At least one output column is required.");
        }

        IEnumerable<string> duplicates = table.InputColumns
            .Concat(table.OutputColumns)
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key);

        foreach (string? duplicate in duplicates)
        {
            errors.Add($"Duplicate column name '{duplicate}'.");
        }

        foreach ((DecisionTableRow row, int index) in table.Rows.Select((x, idx) => (x, idx + 1)))
        {
            if (row.InputCells.Count != table.InputColumns.Count)
            {
                errors.Add($"Row {index}: Input cell count mismatch.");
            }

            if (row.OutputCells.Count != table.OutputColumns.Count)
            {
                errors.Add($"Row {index}: Output cell count mismatch.");
            }

            foreach (DecisionTableCell cell in row.InputCells)
            {
                if (!DecisionTableExpressionEvaluator.IsExpressionValid(cell.Expression))
                {
                    errors.Add($"Row {index}: Invalid input expression '{cell.Expression}'.");
                }
            }

            foreach (DecisionTableCell cell in row.OutputCells)
            {
                if (string.IsNullOrWhiteSpace(cell.Expression))
                {
                    errors.Add($"Row {index}: Empty output expression.");
                }
            }
        }

        if (table.HitPolicy == HitPolicy.Unique)
        {
            IReadOnlyList<string> overlaps = OverlapDetector.Detect(table);
            errors.AddRange(overlaps.Select(x => $"Overlap detected: {x}"));
        }

        warnings.AddRange(GapDetector.Detect(table));

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }
}
