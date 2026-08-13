using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.RuleEngine.DecisionTable.Converters;
using Muonroi.RuleEngine.DecisionTable.Feel;
using Muonroi.RuleEngine.DecisionTable.Models;
using System.Diagnostics;

namespace Muonroi.RuleEngine.DecisionTable;

/// <summary>
/// Executes a decision table against a set of input facts.
/// </summary>
/// <param name="feelEvaluator">Optional FEEL evaluator for input expressions.</param>
public sealed class DecisionTableExecutor(IFeelCellEvaluator? feelEvaluator = null) : IDecisionTableExecutor
{
    private readonly IFeelCellEvaluator _feelEvaluator =
        feelEvaluator ?? new FullFeelCellEvaluator(new SimplifiedFeelCellEvaluator());

    /// <summary>
    /// Evaluates the decision table and returns matched rows and outputs.
    /// </summary>
    /// <param name="table">Decision table to execute.</param>
    /// <param name="inputFacts">Input facts keyed by column name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Execution result containing matches and outputs.</returns>
    public Task<DecisionTableExecutionResult> ExecuteAsync(
        DecisionTableModel table,
        IReadOnlyDictionary<string, object?> inputFacts,
        CancellationToken cancellationToken = default)
    {
        MGuard.NotNull(table);
        MGuard.NotNull(inputFacts);
        cancellationToken.ThrowIfCancellationRequested();

        Stopwatch stopwatch = Stopwatch.StartNew();
        List<(DecisionTableRow Row, IReadOnlyDictionary<string, object?> Outputs)> matchedRows = [];

        foreach (DecisionTableRow row in table.Rows
                     .Where(x => x.IsEnabled)
                     .OrderBy(x => x.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsMatch(table, row, inputFacts))
            {
                continue;
            }

            matchedRows.Add((row, BuildOutputs(table, row, inputFacts)));
        }

        IReadOnlyList<(DecisionTableRow Row, IReadOnlyDictionary<string, object?> Outputs)> selected =
            SelectByHitPolicy(table.HitPolicy, matchedRows);

        stopwatch.Stop();

        DecisionTableExecutionResult result = new()
        {
            Matched = selected.Count > 0,
            HitPolicy = table.HitPolicy,
            EvaluationTime = stopwatch.Elapsed,
            MatchedRowIds = [.. selected.Select(x => x.Row.Id)],
            Outputs = [.. selected.Select(x => new DecisionTableOutputRow
            {
                RowId = x.Row.Id,
                Outputs = x.Outputs
            })]
        };

        return Task.FromResult(result);
    }

    private bool IsMatch(
        DecisionTableModel table,
        DecisionTableRow row,
        IReadOnlyDictionary<string, object?> inputFacts)
    {
        int count = Math.Min(row.InputCells.Count, table.InputColumns.Count);
        for (int i = 0; i < count; i++)
        {
            DecisionTableColumn column = table.InputColumns[i];
            DecisionTableCell cell = row.InputCells[i];
            inputFacts.TryGetValue(column.Name, out object? actual);
            if (!_feelEvaluator.Evaluate(cell.Expression, actual, column.DataType))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyDictionary<string, object?> BuildOutputs(
        DecisionTableModel table,
        DecisionTableRow row,
        IReadOnlyDictionary<string, object?> inputFacts)
    {
        Dictionary<string, object?> variables = new(inputFacts, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, object?> outputs = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < row.OutputCells.Count && i < table.OutputColumns.Count; i++)
        {
            DecisionTableColumn column = table.OutputColumns[i];
            DecisionTableCell cell = row.OutputCells[i];
            object? value = DecisionTableExpressionEvaluator.EvaluateOutput(cell.Expression, variables, column.DataType);
            outputs[column.Name] = value;
            variables[column.Name] = value;
        }

        return outputs;
    }

    private static IReadOnlyList<(DecisionTableRow Row, IReadOnlyDictionary<string, object?> Outputs)> SelectByHitPolicy(
        HitPolicy hitPolicy,
        IReadOnlyList<(DecisionTableRow Row, IReadOnlyDictionary<string, object?> Outputs)> matches)
    {
        if (matches.Count == 0)
        {
            return [];
        }

        if (hitPolicy == HitPolicy.Unique)
        {
            MGuard.State(matches.Count <= 1, $"Hit policy '{HitPolicy.Unique}' requires a single match, but found {matches.Count}.");
        }

        return hitPolicy switch
        {
            HitPolicy.First => [matches[0]],
            HitPolicy.Unique => [matches[0]],
            HitPolicy.Collect => [.. matches],
            HitPolicy.Priority => [matches.OrderBy(x => x.Row.Order).First()],
            _ => [matches[0]]
        };
    }
}
