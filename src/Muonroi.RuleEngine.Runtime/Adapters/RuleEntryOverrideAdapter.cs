namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Wraps a compiled rule so flow-graph order and dependency metadata can override
/// the generated/default values without changing the inner rule implementation.
/// </summary>
public sealed class RuleEntryOverrideAdapter<TContext> : IRule<TContext>
{
    private readonly IRule<TContext> _inner;

    public string Code => _inner.Code;
    public int Order { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public HookPoint HookPoint => _inner.HookPoint;
    public RuleType Type => _inner.Type;
    public string Name => _inner.Name;
    public IEnumerable<Type> Dependencies => _inner.Dependencies;

    public RuleEntryOverrideAdapter(IRule<TContext> inner, int order, IReadOnlyList<string>? dependsOn = null)
    {
        _inner = inner;
        Order = order;
        DependsOn = dependsOn is null ? [] : [.. dependsOn];
    }

    public Task<RuleResult> EvaluateAsync(TContext ctx, FactBag facts, CancellationToken ct)
        => _inner.EvaluateAsync(ctx, facts, ct);

    public Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
        => _inner.ExecuteAsync(context, cancellationToken);
}
