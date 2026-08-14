namespace Muonroi.RuleEngine.DecisionTable.Converters;

/// <summary>
/// Converts decision tables into executable rule instances.
/// </summary>
/// <param name="feelEvaluator">Optional FEEL evaluator for input expressions.</param>
public sealed class DecisionTableToRuleConverter(IFeelCellEvaluator? feelEvaluator = null)
{
    private readonly IFeelCellEvaluator _feelEvaluator =
        feelEvaluator ?? new FullFeelCellEvaluator(new SimplifiedFeelCellEvaluator());

    /// <summary>
    /// Converts the table into rules using the provided evaluator and context projector.
    /// </summary>
    /// <typeparam name="TContext">Rule context type.</typeparam>
    /// <param name="table">Decision table to convert.</param>
    /// <param name="contextProjector">Optional projector from context to fact dictionary.</param>
    /// <returns>Rule list derived from the table.</returns>
    public IReadOnlyList<IRule<TContext>> ConvertWithEvaluator<TContext>(
        DecisionTableModel table,
        Func<TContext, IDictionary<string, object?>>? contextProjector = null)
    {
        DecisionTableRow[] rows = [.. table.Rows
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)];

        return rows
            .Select(row => (IRule<TContext>)new DecisionTableRowRule<TContext>(table, row, contextProjector, _feelEvaluator))
            .ToArray();
    }

    /// <summary>
    /// Converts the table into rules using the default evaluator.
    /// </summary>
    /// <typeparam name="TContext">Rule context type.</typeparam>
    /// <param name="table">Decision table to convert.</param>
    /// <param name="contextProjector">Optional projector from context to fact dictionary.</param>
    /// <returns>Rule list derived from the table.</returns>
    public static IReadOnlyList<IRule<TContext>> Convert<TContext>(
        DecisionTableModel table,
        Func<TContext, IDictionary<string, object?>>? contextProjector = null)
    {
        return new DecisionTableToRuleConverter().ConvertWithEvaluator(table, contextProjector);
    }

    private sealed class DecisionTableRowRule<TContext>(
        DecisionTableModel table,
        DecisionTableRow row,
        Func<TContext, IDictionary<string, object?>>? projector,
        IFeelCellEvaluator feelEvaluator) : IRule<TContext>
    {
        public string Code => row.Id;
        public int Order => row.Order;
        public IReadOnlyList<string> DependsOn => [];
        public HookPoint HookPoint => HookPoint.BeforeRule;
        public RuleType Type => RuleType.Validation;
        public string Name => $"{table.Name}_{row.Id}";
        public IEnumerable<Type> Dependencies => [];

        public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
        {
            IReadOnlyDictionary<string, object?> values = projector?.Invoke(ctx) is { } projected
                ? new Dictionary<string, object?>(projected, StringComparer.OrdinalIgnoreCase)
                : ObjectToDictionary(ctx);
            bool pass = MatchesRow(values, table, row, feelEvaluator);
            return Task.FromResult(pass ? RuleResult.Passed() : RuleResult.Failure($"DecisionTable row '{row.Id}' mismatch."));
        }

        public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        {
            IDictionary<string, object?> values = projector?.Invoke(context) ?? ObjectToDictionary(context);

            for (int i = 0; i < row.OutputCells.Count && i < table.OutputColumns.Count; i++)
            {
                DecisionTableColumn column = table.OutputColumns[i];
                values[column.Name] = DecisionTableExpressionEvaluator.EvaluateOutput(
                    row.OutputCells[i].Expression,
                    values as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>(values),
                    column.DataType);
            }

            return Task.CompletedTask;
        }

        private static bool MatchesRow(
            IReadOnlyDictionary<string, object?> values,
            DecisionTableModel dt,
            DecisionTableRow ruleRow,
            IFeelCellEvaluator cellEvaluator)
        {
            for (int i = 0; i < ruleRow.InputCells.Count && i < dt.InputColumns.Count; i++)
            {
                DecisionTableColumn column = dt.InputColumns[i];
                values.TryGetValue(column.Name, out object? actual);
                DecisionTableCell cell = ruleRow.InputCells[i];
                if (!cellEvaluator.Evaluate(cell.Expression, actual, column.DataType))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, object?> ObjectToDictionary(TContext context)
        {
            Dictionary<string, object?> map = new(StringComparer.OrdinalIgnoreCase);
            foreach (System.Reflection.PropertyInfo property in typeof(TContext).GetProperties())
            {
                map[property.Name] = property.GetValue(context);
            }

            return map;
        }
    }
}
