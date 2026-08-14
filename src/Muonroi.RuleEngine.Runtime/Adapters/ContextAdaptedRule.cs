namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Wraps an <see cref="IRule{TChild}"/> so it can execute inside a
/// <see cref="FactBagRuleContext"/> orchestration pipeline.
/// Used when a compiled rule (Type A) participates in a sub-flow where the
/// parent orchestrator operates on <see cref="FactBagRuleContext"/>.
/// Reconstructs the typed <typeparamref name="TChild"/> context from the FactBag
/// using <see cref="IContextFactory{TChild}"/>.
/// </summary>
/// <typeparam name="TChild">The concrete rule context type of the inner rule.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ContextAdaptedRule{TChild}"/> class.
/// </remarks>
/// <param name="inner">The inner rule to execute.</param>
/// <param name="factory">Factory used to build the child context.</param>
public sealed class ContextAdaptedRule<TChild>(IRule<TChild> inner, IContextFactory<TChild> factory) : IRule<FactBagRuleContext>
{
    private readonly IRule<TChild> _inner = inner;
    private readonly IContextFactory<TChild> _factory = factory;

    /// <inheritdoc />
    public string Code => _inner.Code;

    /// <inheritdoc />
    public int Order => _inner.Order;

    /// <inheritdoc />
    public string[] DependsOn => [.. _inner.DependsOn];

    /// <inheritdoc />
    public HookPoint HookPoint => _inner.HookPoint;

    /// <inheritdoc />
    public RuleType Type => _inner.Type;

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => _inner.Dependencies;

    /// <inheritdoc />
    public Task<RuleResult> EvaluateAsync(
        FactBagRuleContext context, FactBag facts, CancellationToken ct)
    {
        FactBag sourceFacts = new();
        foreach (KeyValuePair<string, object?> kv in context.Facts.AsReadOnly())
        {
            sourceFacts.Set(kv.Key, kv.Value);
        }

        foreach (KeyValuePair<string, object?> kv in facts.AsReadOnly())
        {
            sourceFacts.Set(kv.Key, kv.Value);
        }

        TChild childCtx = _factory.Create(sourceFacts);
        return _inner.EvaluateAsync(childCtx, facts, ct);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(FactBagRuleContext ctx, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
