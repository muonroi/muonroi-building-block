namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Executes a workflow definition that can orchestrate rule tasks and service tasks.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public interface IMRuleWorkflowRunner<TContext>
{
    /// <summary>
    /// Executes the workflow asynchronously.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="workflow">The workflow definition to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the workflow execution result.</returns>
    Task<MRuleWorkflowResult<TContext>> ExecuteAsync(
        TContext context,
        MRuleWorkflowDefinition<TContext> workflow,
        CancellationToken cancellationToken = default);
}

