namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Applies flow-graph routing semantics on top of an inner rule so the existing
/// orchestrator can keep running sequentially while edges decide whether a node
/// should run and whether failures branch or halt the workflow.
/// </summary>
internal sealed class GraphRuleDispatchAdapter<TContext> : IRule<TContext>
{
    private readonly IRule<TContext> _inner;
    private readonly RuleGraphEntry _entry;
    private readonly AsyncLocal<PendingExecutionState?> _pendingExecution = new();

    public string Code => _inner.Code;
    public int Order => _entry.Order;
    public IReadOnlyList<string> DependsOn => _entry.DependsOn;
    public HookPoint HookPoint => _inner.HookPoint;
    public RuleType Type => _inner.Type;
    public string Name => _inner.Name;
    public IEnumerable<Type> Dependencies => _inner.Dependencies;

    public GraphRuleDispatchAdapter(IRule<TContext> inner, RuleGraphEntry entry)
    {
        _inner = inner;
        _entry = entry;
    }

    public async Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        if (!MShouldExecute(facts))
        {
            MWriteExecutionState(facts, executed: false, passed: false, isError: false, message: "SKIPPED");
            _pendingExecution.Value = PendingExecutionState.Skip;
            return new RuleResult(true, ["SKIPPED: edge condition not met"]);
        }

        try
        {
            RuleResult result = await _inner.EvaluateAsync(ctx, facts, ct);
            bool handledFailure = !result.IsSuccess && (_entry.HasAlwaysEdge || _entry.HasOnFalseEdge);
            MWriteExecutionState(
                facts,
                executed: true,
                passed: result.IsSuccess,
                isError: false,
                message: result.Errors.Count > 0 ? string.Join("; ", result.Errors) : null);

            _pendingExecution.Value = result.IsSuccess
                ? PendingExecutionState.ExecuteInner
                : PendingExecutionState.Skip;

            if (handledFailure)
            {
                return RuleResult.Passed();
            }

            return result;
        }
        catch (Exception ex)
        {
            MWriteExecutionState(facts, executed: true, passed: false, isError: true, message: ex.Message);
            _pendingExecution.Value = PendingExecutionState.Skip;

            if (_entry.HasAlwaysEdge || _entry.HasOnErrorEdge)
            {
                return RuleResult.Passed();
            }

            throw;
        }
    }

    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        PendingExecutionState state = _pendingExecution.Value ?? PendingExecutionState.Skip;
        _pendingExecution.Value = null;
        return state == PendingExecutionState.ExecuteInner
            ? _inner.ExecuteAsync(context, cancellationToken)
            : Task.CompletedTask;
    }

    private bool MShouldExecute(FactBag facts)
    {
        if (_entry.IncomingEdges.Count == 0)
        {
            return true;
        }

        foreach (RuleGraphIncomingEdge edge in _entry.IncomingEdges)
        {
            bool sourceExecuted = facts.Get<bool?>(MExecutionKey(edge.SourceNodeId)) == true;
            if (!sourceExecuted)
            {
                continue;
            }

            bool sourcePassed = facts.Get<bool?>(MPassedKey(edge.SourceNodeId)) == true;
            bool sourceErrored = facts.Get<bool?>(MErroredKey(edge.SourceNodeId)) == true;

            if (string.Equals(edge.EdgeType, "always", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(edge.EdgeType, "on-true", StringComparison.OrdinalIgnoreCase) && sourcePassed)
            {
                return true;
            }

            if (string.Equals(edge.EdgeType, "on-false", StringComparison.OrdinalIgnoreCase) && !sourcePassed && !sourceErrored)
            {
                return true;
            }

            if (string.Equals(edge.EdgeType, "on-error", StringComparison.OrdinalIgnoreCase) && sourceErrored)
            {
                return true;
            }
        }

        return false;
    }

    private void MWriteExecutionState(
        FactBag facts,
        bool executed,
        bool passed,
        bool isError,
        string? message)
    {
        facts.Set(MExecutionKey(_entry.NodeId), executed);
        facts.Set(MPassedKey(_entry.NodeId), passed);
        facts.Set(MErroredKey(_entry.NodeId), isError);

        Dictionary<string, object?> resultPayload = new(StringComparer.OrdinalIgnoreCase)
        {
            ["isPass"] = passed,
            ["message"] = message,
            ["errorCode"] = isError ? "runtime.error" : null
        };
        facts.Set($"__graph.node.{_entry.NodeId}.result", resultPayload);
        facts.Set("result", resultPayload);
    }

    private static string MExecutionKey(string nodeId) => $"__graph.node.{nodeId}.executed";
    private static string MPassedKey(string nodeId) => $"__graph.node.{nodeId}.passed";
    private static string MErroredKey(string nodeId) => $"__graph.node.{nodeId}.errored";

    private enum PendingExecutionState
    {
        Skip = 0,
        ExecuteInner = 1
    }
}
