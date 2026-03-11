using Muonroi.RuleEngine.Abstractions.Adapters;

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
public sealed class ContextAdaptedRule<TChild> : IRule<FactBagRuleContext>
{
    private readonly IRule<TChild> _inner;
    private readonly IContextFactory<TChild> _factory;

    public string Code => _inner.Code;
    public int Order => _inner.Order;
    public string[] DependsOn => [.. _inner.DependsOn];
    public HookPoint HookPoint => _inner.HookPoint;
    public RuleType Type => _inner.Type;
    public string Name => _inner.Name;
    public IEnumerable<Type> Dependencies => _inner.Dependencies;

    public ContextAdaptedRule(IRule<TChild> inner, IContextFactory<TChild> factory)
    {
        _inner   = inner;
        _factory = factory;
    }

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

    public Task ExecuteAsync(FactBagRuleContext ctx, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
