namespace Muonroi.RuleEngine.Abstractions.Adapters;

/// <summary>
/// Projects a <typeparamref name="TContext"/> into a flat key/value dictionary for use in
/// FEEL expressions and Liquid templates.
/// Register a custom implementation to control which context fields are exposed to
/// expressions and under what names.
/// Default: reflection-based (all public properties, one level deep).
/// </summary>
/// <typeparam name="TContext">The rule execution context type.</typeparam>
public interface IContextProjector<TContext>
{
    /// <summary>
    /// Projects the specified context into a flat variable dictionary.
    /// </summary>
    /// <param name="context">The rule execution context.</param>
    /// <returns>A read-only dictionary mapping variable names to their values.</returns>
    IReadOnlyDictionary<string, object?> Project(TContext context);
}
