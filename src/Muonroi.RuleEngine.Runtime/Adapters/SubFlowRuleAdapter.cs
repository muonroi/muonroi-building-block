namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Delegates execution to a child workflow via <see cref="RulesEngineService.ExecuteSubFlowAsync"/>.
/// Maps parent scope facts to child input FactBag (via input mappings),
/// then merges selected child output facts back to the parent FactBag (via output mappings).
/// Detects circular sub-flow references via <see cref="SubFlowCallStack"/>.
/// </summary>
/// <typeparam name="TContext">The parent rule execution context type.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SubFlowRuleAdapter{TContext}"/> class.
/// </remarks>
/// <param name="code">Rule code for this node.</param>
/// <param name="childFlowCode">Child workflow code.</param>
/// <param name="inputMappings">Input mappings from parent to child.</param>
/// <param name="outputMappings">Output mappings from child to parent.</param>
/// <param name="engine">Rules engine service used to execute the sub-flow.</param>
/// <param name="projector">Context projector for variables.</param>
/// <param name="log">Logger instance.</param>
public sealed class SubFlowRuleAdapter<TContext>(
    string code,
    string childFlowCode,
    IReadOnlyList<SubFlowInputMapping> inputMappings,
    IReadOnlyList<SubFlowOutputMapping> outputMappings,
    RulesEngineService engine,
    IContextProjector<TContext> projector,
    IMLog<SubFlowRuleAdapter<TContext>> log) : IRule<TContext>
{
    private readonly string _code = code;
    private readonly string _childFlowCode = childFlowCode;
    private readonly IReadOnlyList<SubFlowInputMapping> _inputMappings = inputMappings;
    private readonly IReadOnlyList<SubFlowOutputMapping> _outputMappings = outputMappings;
    private readonly RulesEngineService _engine = engine;
    private readonly IContextProjector<TContext> _projector = projector;
    private readonly IMLog<SubFlowRuleAdapter<TContext>> _log = log;

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
    public string Name => $"SubFlow:{_childFlowCode}";

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => [];

    /// <inheritdoc />
    public async Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
    {
        FactBag childFacts = BuildChildFacts(ctx, facts);

        _log.Info("SubFlow '{Code}' → executing child flow '{Child}'", _code, _childFlowCode);

        SubFlowExecutionResult childResult;
        try
        {
            childResult = await _engine.ExecuteSubFlowAsync(_childFlowCode, childFacts, ct);
        }
        catch (SubFlowCycleException)
        {
            throw; // rethrow cycle exceptions without wrapping
        }
        catch (Exception ex)
        {
            _log.Error(ex, "SubFlow '{Code}' child flow '{Child}' threw", _code, _childFlowCode);
            return RuleResult.Failure(
                $"SubFlow '{_childFlowCode}' execution failed: {ex.Message}");
        }

        if (!childResult.IsSuccess)
        {
            _log.Warn("SubFlow '{Code}' child flow failed: {Errors}",
                _code, string.Join(", ", childResult.Errors));
            return RuleResult.Failure([.. childResult.Errors]);
        }

        // Merge child output facts into parent FactBag (only ExposeToParent = true)
        foreach (SubFlowOutputMapping mapping in _outputMappings.Where(m => m.ExposeToParent))
        {
            if (childResult.OutputFacts.TryGet(mapping.ChildPath, out object? value))
            {
                string parentKey = string.IsNullOrEmpty(mapping.ParentPath)
                    ? mapping.ChildPath
                    : mapping.ParentPath;
                facts.Set(parentKey, value);
                _log.Debug("SubFlow '{Code}' merged fact '{Child}' → '{Parent}'",
                    _code, mapping.ChildPath, parentKey);
            }
        }

        return RuleResult.Passed();
    }

    /// <inheritdoc />
    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    private FactBag BuildChildFacts(TContext ctx, FactBag parentFacts)
    {
        FactBag childFacts = new();
        IReadOnlyDictionary<string, object?> contextDict = _projector.Project(ctx);

        foreach (SubFlowInputMapping mapping in _inputMappings)
        {
            object? value = null;

            // FactBag takes priority over context projection
            if (!parentFacts.TryGet(mapping.SourcePath, out value))
            {
                contextDict.TryGetValue(mapping.SourcePath, out value);
            }

            // Apply optional simple FEEL-like transform
            if (value is not null && !string.IsNullOrEmpty(mapping.TransformExpression))
            {
                value = ApplyTransform(mapping.TransformExpression, value);
            }

            childFacts.Set(mapping.TargetPath, value);
        }

        return childFacts;
    }

    private static object? ApplyTransform(string feelExpr, object? value)
    {
        // Phase A: named type-cast transforms only
        // Phase B: full FEEL expression evaluation via FeelEvaluator
        if (feelExpr.StartsWith("string(", StringComparison.OrdinalIgnoreCase))
        {
            return value?.ToString();
        }

        if (feelExpr.StartsWith("number(", StringComparison.OrdinalIgnoreCase))
        {
            return decimal.TryParse(value?.ToString(),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal d) ? d : value;
        }

        if (feelExpr.StartsWith("boolean(", StringComparison.OrdinalIgnoreCase))
        {
            return bool.TryParse(value?.ToString(), out bool b) ? b : value;
        }

        return value; // passthrough for unrecognized transforms
    }
}
