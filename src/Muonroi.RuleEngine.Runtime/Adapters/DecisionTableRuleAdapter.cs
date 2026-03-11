using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions.Adapters;
using Muonroi.RuleEngine.DecisionTable;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Stores;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Executes a Decision Table as an <see cref="IRule{TContext}"/>.
/// Loads the table from <see cref="IDecisionTableStore"/>, builds input facts from
/// context projection + FactBag, then writes output column values back to the FactBag.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public sealed class DecisionTableRuleAdapter<TContext> : IRule<TContext>
{
    private readonly string _code;
    private readonly string _tableId;
    private readonly IDecisionTableStore _store;
    private readonly IDecisionTableExecutor _executor;
    private readonly IContextProjector<TContext> _projector;
    private readonly IMLog<DecisionTableRuleAdapter<TContext>> _log;
    private readonly bool _failOnNoMatch;

    public string Code => _code;
    public int Order { get; init; }
    public string[] DependsOn { get; init; } = [];
    public HookPoint HookPoint => HookPoint.BeforeRule;
    public RuleType Type => RuleType.Business;
    public string Name => $"DT:{_code}";
    public IEnumerable<Type> Dependencies => [];

    public DecisionTableRuleAdapter(
        string code,
        string tableId,
        IDecisionTableStore store,
        IDecisionTableExecutor executor,
        IContextProjector<TContext> projector,
        IMLog<DecisionTableRuleAdapter<TContext>> log,
        bool failOnNoMatch = true)
    {
        _code         = code;
        _tableId      = tableId;
        _store        = store;
        _executor     = executor;
        _projector    = projector;
        _log          = log;
        _failOnNoMatch = failOnNoMatch;
    }

    public async Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        // Load decision table
        DecisionTableModel? table = await _store.GetByIdAsync(_tableId, ct);
        if (table is null)
        {
            return RuleResult.Failure($"Decision table '{_tableId}' not found.");
        }

        // Build input facts from context projection + FactBag (FactBag has higher priority)
        IReadOnlyDictionary<string, object?> inputs = BuildInputs(ctx, facts, table);

        // Execute
        DecisionTableExecutionResult result = await _executor.ExecuteAsync(table, inputs, ct);

        if (!result.Matched)
        {
            _log.Info("DT '{Code}': no row matched (HitPolicy={Policy})",
                _code, result.HitPolicy);

            return _failOnNoMatch
                ? RuleResult.Failure($"No decision table row matched for '{_code}'")
                : RuleResult.Passed();
        }

        // Write all output column values to FactBag
        foreach (DecisionTableOutputRow outputRow in result.Outputs)
        {
            foreach (KeyValuePair<string, object?> kv in outputRow.Outputs)
            {
                facts.Set(kv.Key, kv.Value);
            }
        }

        _log.Info("DT '{Code}': matched {Count} row(s)", _code, result.MatchedRowIds.Count);
        return RuleResult.Passed();
    }

    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private IReadOnlyDictionary<string, object?> BuildInputs(
        TContext ctx, FactBag facts, DecisionTableModel table)
    {
        IReadOnlyDictionary<string, object?> contextProps = _projector.Project(ctx);
        Dictionary<string, object?> inputs = new(
            table.InputColumns.Count, StringComparer.OrdinalIgnoreCase);

        foreach (DecisionTableColumn col in table.InputColumns)
        {
            // FactBag takes priority over context projection
            if (facts.TryGet<object>(col.Name, out object? factVal))
            {
                inputs[col.Name] = factVal;
            }
            else if (contextProps.TryGetValue(col.Name, out object? ctxVal))
            {
                inputs[col.Name] = ctxVal;
            }
            else
            {
                inputs[col.Name] = null;
            }
        }

        return inputs;
    }
}
