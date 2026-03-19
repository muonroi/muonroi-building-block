namespace Muonroi.RuleEngine.Abstractions;

/// <summary>
/// Orchestrates the execution of rules for a given context.
/// This is the primary entry point for rule execution in the Muonroi ecosystem.
/// </summary>
/// <typeparam name="TContext">The type of context used for rule evaluation.</typeparam>
public interface IMRuleOrchestrator<in TContext> where TContext : IRuleContext
{
    /// <summary>
    /// Executes all registered rules for the provided context asynchronously.
    /// </summary>
    /// <param name="context">The context carrying facts for evaluation.</param>
    /// <param name="cancellationToken">Token to cancel the execution.</param>
    /// <returns>A summary of the execution run.</returns>
    Task<OrchestratorResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default);
}
