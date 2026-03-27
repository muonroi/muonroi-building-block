namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Thin <see cref="IRuleContext"/> wrapper around a <see cref="FactBag"/>.
/// Used as the generic context when executing child sub-flows where the
/// concrete context type is unknown at compile time.
/// </summary>
public sealed class FactBagRuleContext : IRuleContext
{
    /// <summary>
    /// Gets the facts available to the child workflow.
    /// </summary>
    public FactBag Facts { get; }

    /// <summary>
    /// Creates a new fact-bag-backed rule context.
    /// </summary>
    /// <param name="facts">Facts to expose to the child workflow.</param>
    public FactBagRuleContext(FactBag facts)
    {
        Facts = facts;
    }

    /// <inheritdoc/>
    public void HaltGroup()
    {
        // no-op for sub-flow context - orchestrator manages halt via rule result
    }
}
