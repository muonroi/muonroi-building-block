using Muonroi.RuleEngine.DecisionTable.Models;

namespace Muonroi.RuleEngine.DecisionTable.Converters;

public sealed class DecisionTableToRuleConverter
{
    public static IReadOnlyList<IRule<TContext>> Convert<TContext>(
        DecisionTableModel table,
        Func<TContext, IDictionary<string, object?>>? contextProjector = null)
    {
        DecisionTableRow[] rows = [.. table.Rows
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Order)];

        return rows
            .Select(row => (IRule<TContext>)new DecisionTableRowRule<TContext>(table, row, contextProjector))
            .ToArray();
    }

    private sealed class DecisionTableRowRule<TContext>(
        DecisionTableModel table,
        DecisionTableRow row,
        Func<TContext, IDictionary<string, object?>>? projector) : IRule<TContext>
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
            bool pass = MatchesRow(values, table, row);
            return Task.FromResult(pass ? RuleResult.Passed() : RuleResult.Failure($"DecisionTable row '{row.Id}' mismatch."));
        }

        public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        {
            IDictionary<string, object?> values = projector?.Invoke(context) ?? ObjectToDictionary(context);

            // Writes outputs to context dictionary when projector is used.
            for (int i = 0; i < row.OutputCells.Count && i < table.OutputColumns.Count; i++)
            {
                DecisionTableColumn column = table.OutputColumns[i];
                values[column.Name] = row.OutputCells[i].Expression;
            }

            return Task.CompletedTask;
        }

        private static bool MatchesRow(
            IReadOnlyDictionary<string, object?> values,
            DecisionTableModel dt,
            DecisionTableRow ruleRow)
        {
            for (int i = 0; i < ruleRow.InputCells.Count && i < dt.InputColumns.Count; i++)
            {
                DecisionTableColumn column = dt.InputColumns[i];
                values.TryGetValue(column.Name, out object? actual);
                DecisionTableCell cell = ruleRow.InputCells[i];
                if (!DecisionTableExpressionEvaluator.Evaluate(actual, cell.Expression))
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
