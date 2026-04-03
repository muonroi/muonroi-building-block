namespace Muonroi.Rules.Rules
{
    /// <summary>
    /// Defines a composable business rule evaluated against a given context.
    /// </summary>
    /// <typeparam name="TContext">Type of the context object.</typeparam>
    [Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
    public interface IBusinessRule<TContext>
    {
        /// <summary>
        /// Unique code used to identify the rule.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Checks whether the rule is satisfied for the provided <paramref name="context"/>.
        /// </summary>
        Task<bool> IsSatisfiedAsync(TContext context, CancellationToken cancellationToken = default);
    }
}
