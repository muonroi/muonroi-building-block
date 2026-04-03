namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Creates rule instances using the application's dependency injection container.
/// </summary>
/// <typeparam name="TContext">The execution context type.</typeparam>
public interface IRuleFactory<TContext>
{
    /// <summary>Create a rule instance for the given rule type.</summary>
    IRule<TContext> Create(Type ruleType);
}
