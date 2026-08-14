namespace Muonroi.Rules.Rules
{
    /// <summary>
    /// Defines a hook that determines whether a rule should execute for a given context.
    /// Use this to implement feature flags and gradual rollouts per tenant or user group.
    /// </summary>
    /// <typeparam name="T">Type of the context passed to the rule.</typeparam>
    [Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
    public interface IRuleActivationStrategy<T>
    {
        /// <summary>
        /// Returns <c>true</c> if <paramref name="rule"/> is active for the provided <paramref name="context"/>.
        /// </summary>
        bool IsActive(IRule<T> rule, T context);
    }
}
