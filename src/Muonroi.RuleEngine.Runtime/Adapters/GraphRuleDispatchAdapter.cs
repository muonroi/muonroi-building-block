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

        // Snapshot ALL FactBag keys BEFORE rule evaluation (needed to compute outputSnapshot later).
        // keysBefore intentionally includes internal keys for accurate change detection.
        HashSet<string> keysBefore = new(facts.Keys, StringComparer.OrdinalIgnoreCase);

        // Capture values of non-internal keys for output-change detection (replaces inputSnapshot for this purpose).
        Dictionary<string, object?> factValuesBefore = new(StringComparer.OrdinalIgnoreCase);
        foreach (string key in facts.Keys)
        {
            if (!key.StartsWith("__graph.", StringComparison.OrdinalIgnoreCase) &&
                !key.StartsWith("__trace.", StringComparison.OrdinalIgnoreCase))
            {
                factValuesBefore[key] = facts[key];
            }
        }

        // Build edge-scoped inputSnapshot — ONLY predecessor output facts (not full FactBag).
        Dictionary<string, object?> inputSnapshot;

        if (_entry.IncomingEdges.Count == 0)
        {
            // Start node: check if __trace.initial.input was already written by an earlier start node.
            // If YES, reuse it; otherwise build from non-internal FactBag keys and store it.
            Dictionary<string, object?>? existingInitial = facts.Get<Dictionary<string, object?>>("__trace.initial.input");
            if (existingInitial is not null)
            {
                inputSnapshot = existingInitial;
            }
            else
            {
                inputSnapshot = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in facts.Keys)
                {
                    if (!key.StartsWith("__graph.", StringComparison.OrdinalIgnoreCase) &&
                        !key.StartsWith("__trace.", StringComparison.OrdinalIgnoreCase))
                    {
                        inputSnapshot[key] = facts[key];
                    }
                }

                facts.Set("__trace.initial.input", inputSnapshot);
            }
        }
        else
        {
            // Non-start node: inputSnapshot = union of ONLY active predecessors' __trace.node.{id}.output.
            inputSnapshot = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (RuleGraphIncomingEdge edge in _entry.IncomingEdges)
            {
                bool sourceExecuted = facts.Get<bool?>(MExecutionKey(edge.SourceNodeId)) == true;
                if (!sourceExecuted)
                {
                    continue;
                }

                bool sourcePassed = facts.Get<bool?>(MPassedKey(edge.SourceNodeId)) == true;
                bool sourceErrored = facts.Get<bool?>(MErroredKey(edge.SourceNodeId)) == true;

                bool edgeActive = string.Equals(edge.EdgeType, "always", StringComparison.OrdinalIgnoreCase)
                    || (string.Equals(edge.EdgeType, "on-true", StringComparison.OrdinalIgnoreCase) && sourcePassed)
                    || (string.Equals(edge.EdgeType, "on-false", StringComparison.OrdinalIgnoreCase) && !sourcePassed && !sourceErrored)
                    || (string.Equals(edge.EdgeType, "on-error", StringComparison.OrdinalIgnoreCase) && sourceErrored);

                if (!edgeActive)
                {
                    continue;
                }

                Dictionary<string, object?>? predecessorOutput =
                    facts.Get<Dictionary<string, object?>>($"__trace.node.{edge.SourceNodeId}.output");
                if (predecessorOutput is not null)
                {
                    foreach (KeyValuePair<string, object?> kvp in predecessorOutput)
                    {
                        inputSnapshot[kvp.Key] = kvp.Value;
                    }
                }
            }
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

            // Compute output = keys that were added or changed after evaluation
            Dictionary<string, object?> outputSnapshot = new(StringComparer.OrdinalIgnoreCase);
            foreach (string key in facts.Keys)
            {
                if (key.StartsWith("__graph.", StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith("__trace.", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!keysBefore.Contains(key))
                {
                    // New key added by this rule
                    outputSnapshot[key] = facts[key];
                }
                else if (!Equals(facts[key], factValuesBefore.GetValueOrDefault(key)))
                {
                    // Existing key modified by this rule
                    outputSnapshot[key] = facts[key];
                }
            }

            // Store per-node trace in FactBag for dry-run response mapping
            facts.Set($"__trace.node.{_entry.NodeId}.input", inputSnapshot);
            facts.Set($"__trace.node.{_entry.NodeId}.output", outputSnapshot);

            _pendingExecution.Value = result.IsSuccess
                ? PendingExecutionState.ExecuteInner
                : PendingExecutionState.Skip;

            if (handledFailure)
            {
                MAppendRoutedFailure(facts, _entry.NodeId, result);
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

    /// <summary>
    /// Appends a routed failure to <c>__graph.routedFailures</c> in the FactBag so that
    /// callers can detect nodes that failed but were routed through (always/on-false edges).
    /// </summary>
    private static void MAppendRoutedFailure(FactBag facts, string nodeId, RuleResult result)
    {
        const string key = "__graph.routedFailures";
        List<Dictionary<string, object?>> failures =
            facts.Get<List<Dictionary<string, object?>>>(key) ?? [];
        failures.Add(new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["nodeId"] = nodeId,
            ["errors"] = result.Errors.ToList(),
            ["isPass"] = false
        });
        facts.Set(key, failures);
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
