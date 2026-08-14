namespace Muonroi.RuleEngine.DecisionTable.Validators;

/// <summary>
/// Validates decision table structure and expressions.
/// </summary>
/// <param name="feelEvaluator">Optional FEEL evaluator for expression validation.</param>
/// <param name="overlapDetector">Optional detector for overlapping rows.</param>
/// <param name="gapDetector">Optional detector for range gaps.</param>
/// <param name="multiColumnOverlapDetector">Optional detector for multi-column overlaps.</param>
/// <param name="redundancyDetector">Optional detector for redundant rows.</param>
public sealed class DecisionTableValidator(
    IFeelCellEvaluator? feelEvaluator = null,
    OverlapDetector? overlapDetector = null,
    GapDetector? gapDetector = null,
    MultiColumnOverlapDetector? multiColumnOverlapDetector = null,
    RedundancyDetector? redundancyDetector = null)
{
    private readonly IFeelCellEvaluator _feelEvaluator =
        feelEvaluator ?? new FullFeelCellEvaluator(new SimplifiedFeelCellEvaluator());
    private readonly OverlapDetector _overlapDetector = overlapDetector ?? new OverlapDetector();
    private readonly GapDetector _gapDetector = gapDetector ?? new GapDetector();
    private readonly MultiColumnOverlapDetector _multiColumnOverlapDetector =
        multiColumnOverlapDetector ?? new MultiColumnOverlapDetector();
    private readonly RedundancyDetector _redundancyDetector = redundancyDetector ?? new RedundancyDetector();

    /// <summary>
    /// Validates the specified table and returns errors and warnings.
    /// </summary>
    /// <param name="table">Decision table to validate.</param>
    /// <returns>Validation result.</returns>
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

            for (int i = 0; i < row.InputCells.Count; i++)
            {
                DecisionTableCell cell = row.InputCells[i];
                string? dataType = i < table.InputColumns.Count ? table.InputColumns[i].DataType : null;
                string? validationError = _feelEvaluator.Validate(cell.Expression, dataType);
                if (!string.IsNullOrWhiteSpace(validationError))
                {
                    errors.Add($"Row {index}: Invalid input expression '{cell.Expression}'. {validationError}");
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
            IReadOnlyList<string> overlaps = _overlapDetector.Detect(table);
            errors.AddRange(overlaps.Select(x => $"Overlap detected: {x}"));

            IReadOnlyList<ConflictReport> conflicts = _multiColumnOverlapDetector.Detect(table);
            errors.AddRange(conflicts.Select(x => $"Conflict: {x.Description}"));
        }

        if (table.HitPolicy == HitPolicy.First)
        {
            IReadOnlyList<RedundancyReport> redundancies = _redundancyDetector.Detect(table);
            warnings.AddRange(redundancies.Select(x => $"Unreachable row {x.RowIndex}: {x.Description}"));
        }

        warnings.AddRange(_gapDetector.Detect(table));

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }
}
