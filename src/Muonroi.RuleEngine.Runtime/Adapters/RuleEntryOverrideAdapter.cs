namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Wraps a compiled rule so flow-graph order and dependency metadata can override
/// the generated/default values without changing the inner rule implementation.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RuleEntryOverrideAdapter{TContext}"/> class.
/// </remarks>
/// <param name="inner">The inner rule to execute.</param>
/// <param name="order">Override order for the flow graph.</param>
/// <param name="dependsOn">Optional dependency overrides.</param>
public sealed class RuleEntryOverrideAdapter<TContext>(IRule<TContext> inner, int order, IReadOnlyList<string>? dependsOn = null) : IRule<TContext>
{
    private readonly IRule<TContext> _inner = inner;

    /// <inheritdoc />
    public string Code => _inner.Code;

    /// <inheritdoc />
    public int Order { get; } = order;

    /// <inheritdoc />
    public IReadOnlyList<string> DependsOn { get; } = dependsOn is null ? [] : [.. dependsOn];

    /// <inheritdoc />
    public HookPoint HookPoint => _inner.HookPoint;

    /// <inheritdoc />
    public RuleType Type => _inner.Type;

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => _inner.Dependencies;

    /// <inheritdoc />
    public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
        => _inner.EvaluateAsync(ctx, facts, ct);

    /// <inheritdoc />
    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => _inner.ExecuteAsync(context, cancellationToken);
}
