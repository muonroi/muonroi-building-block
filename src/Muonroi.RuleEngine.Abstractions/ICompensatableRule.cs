namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Represents a rule that can compensate its side effects when a rule pipeline fails.
/// </summary>
/// <typeparam name="TContext">The type of the context object.</typeparam>
public interface ICompensatableRule<in TContext> : IRule<TContext>
{
    /// <summary>
    /// Attempts to undo or mitigate side effects produced by the rule.
    /// </summary>
    /// <param name="context">The context object used when the rule executed.</param>
    /// <param name="facts">The fact bag populated during rule execution.</param>
    /// <param name="cancellationToken">A token for cancelling the compensation.</param>
    /// <remarks>
    /// Implementations MUST NOT throw exceptions. Any errors should be handled internally
    /// (for example by logging or recording details in <paramref name="facts"/>).
    /// Compensation should be idempotent because it may be invoked multiple times.
    /// </remarks>
    Task CompensateAsync(TContext context, FactBag facts, CancellationToken cancellationToken = default);
}
