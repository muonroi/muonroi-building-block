namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Executes a Decision Table as an <see cref="IRule{TContext}"/>.
/// Loads the table from <see cref="IDecisionTableStore"/>, builds input facts from
/// context projection + FactBag, then writes output column values back to the FactBag.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="DecisionTableRuleAdapter{TContext}"/> class.
/// </remarks>
/// <param name="code">Rule code for this node.</param>
/// <param name="tableId">Decision table identifier.</param>
/// <param name="store">Decision table store.</param>
/// <param name="executor">Decision table executor.</param>
/// <param name="projector">Context projector for inputs.</param>
/// <param name="log">Logger instance.</param>
/// <param name="failOnNoMatch">Whether to fail when no row matches.</param>
public sealed class DecisionTableRuleAdapter<TContext>(
    string code,
    string tableId,
    IDecisionTableStore store,
    IDecisionTableExecutor executor,
    IContextProjector<TContext> projector,
    IMLog<DecisionTableRuleAdapter<TContext>> log,
    bool failOnNoMatch = true) : IRule<TContext>
{
    private readonly string _code = code;
    private readonly string _tableId = tableId;
    private readonly IDecisionTableStore _store = store;
    private readonly IDecisionTableExecutor _executor = executor;
    private readonly IContextProjector<TContext> _projector = projector;
    private readonly IMLog<DecisionTableRuleAdapter<TContext>> _log = log;
    private readonly bool _failOnNoMatch = failOnNoMatch;

    /// <inheritdoc />
    public string Code => _code;

    /// <inheritdoc />
    public int Order { get; init; }

    /// <inheritdoc />
    public string[] DependsOn { get; init; } = [];

    /// <inheritdoc />
    public HookPoint HookPoint => HookPoint.BeforeRule;

    /// <inheritdoc />
    public RuleType Type => RuleType.Business;

    /// <inheritdoc />
    public string Name => $"DT:{_code}";

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => [];

    /// <inheritdoc />
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

    /// <inheritdoc />
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
            if (facts.TryGet(col.Name, out object? factVal))
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
