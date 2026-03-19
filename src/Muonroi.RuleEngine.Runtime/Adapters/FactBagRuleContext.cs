namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Thin <see cref="IRuleContext"/> wrapper around a <see cref="FactBag"/>.
/// Used as the generic TContext when executing child sub-flows where the
/// concrete context type is unknown at compile time.
/// </summary>
public sealed class FactBagRuleContext : IRuleContext
{
    public FactBag Facts { get; }

    public FactBagRuleContext(FactBag facts)
    {
        Facts = facts;
    }

    /// <inheritdoc/>
    public void HaltGroup()
    {
        // no-op for sub-flow context — orchestrator manages halt via rule result
    }
}
