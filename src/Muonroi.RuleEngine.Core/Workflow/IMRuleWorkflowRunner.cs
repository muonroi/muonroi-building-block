namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Executes a workflow definition that can orchestrate rule tasks and service tasks.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public interface IMRuleWorkflowRunner<TContext>
{
    Task<MRuleWorkflowResult<TContext>> ExecuteAsync(
        TContext context,
        MRuleWorkflowDefinition<TContext> workflow,
        CancellationToken cancellationToken = default);
}

