namespace Muonroi.RuleEngine.Abstractions.Adapters;

/// <summary>
/// Reconstructs a <typeparamref name="TContext"/> from a flat <see cref="FactBag"/>.
/// Required for Sub Flow execution: the parent maps facts to the child FactBag,
/// and child rules that need a typed context use this factory to reconstruct it.
/// Default: reflection-based (matches fact keys to properties with parameterless ctor).
/// Custom: register per TContext type for full control.
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public interface IContextFactory<TContext>
{
    /// <summary>
    /// Creates a <typeparamref name="TContext"/> instance populated from the given <see cref="FactBag"/>.
    /// </summary>
    /// <param name="facts">The fact bag containing the input values.</param>
    /// <returns>A new context instance with properties set from the fact bag.</returns>
    TContext Create(FactBag facts);
}
