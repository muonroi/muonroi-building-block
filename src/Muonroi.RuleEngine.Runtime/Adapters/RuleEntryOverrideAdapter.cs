namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Wraps a compiled rule so flow-graph order and dependency metadata can override
/// the generated/default values without changing the inner rule implementation.
/// </summary>
public sealed class RuleEntryOverrideAdapter<TContext> : IRule<TContext>
{
    private readonly IRule<TContext> _inner;

    /// <inheritdoc />
    public string Code => _inner.Code;

    /// <inheritdoc />
    public int Order { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> DependsOn { get; }

    /// <inheritdoc />
    public HookPoint HookPoint => _inner.HookPoint;

    /// <inheritdoc />
    public RuleType Type => _inner.Type;

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => _inner.Dependencies;

    /// <summary>
    /// Initializes a new instance of the <see cref="RuleEntryOverrideAdapter{TContext}"/> class.
    /// </summary>
    /// <param name="inner">The inner rule to execute.</param>
    /// <param name="order">Override order for the flow graph.</param>
    /// <param name="dependsOn">Optional dependency overrides.</param>
    public RuleEntryOverrideAdapter(IRule<TContext> inner, int order, IReadOnlyList<string>? dependsOn = null)
    {
        _inner = inner;
        Order = order;
        DependsOn = dependsOn is null ? [] : [.. dependsOn];
    }

    /// <inheritdoc />
    public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
        => _inner.EvaluateAsync(ctx, facts, ct);

    /// <inheritdoc />
    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => _inner.ExecuteAsync(context, cancellationToken);
}
