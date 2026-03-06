namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Executes workflows that mix rule tasks, service tasks, and gateways.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public sealed class MRuleWorkflowRunner<TContext>(
    IMRuleExecutionRouter<TContext> ruleRouter,
    Microsoft.Extensions.Options.IOptions<MRuleWorkflowOptions>? options,
    ILogger<MRuleWorkflowRunner<TContext>>? logger = null) : IMRuleWorkflowRunner<TContext>
{
    private readonly MRuleWorkflowOptions _options = options?.Value ?? new MRuleWorkflowOptions();
    private readonly ILogger<MRuleWorkflowRunner<TContext>> _logger =
        logger ?? NullLogger<MRuleWorkflowRunner<TContext>>.Instance;

    public async Task<MRuleWorkflowResult<TContext>> ExecuteAsync(
        TContext context,
        MRuleWorkflowDefinition<TContext> workflow,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        MRuleWorkflowExecutionContext<TContext> executionContext = new(context, new FactBag(), ruleRouter);
        List<string> executedSteps = [];

        string? currentStepId = workflow.StartStepId;
        int executedCount = 0;
        int maxSteps = _options.MaxSteps > 0 ? _options.MaxSteps : 256;

        while (!string.IsNullOrWhiteSpace(currentStepId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            executedCount++;
            if (executedCount > maxSteps)
            {
                throw new InvalidOperationException(
                    $"Workflow '{workflow.Name}' exceeded max steps ({maxSteps}). Possible cyclic transition.");
            }

            if (!workflow.Steps.TryGetValue(currentStepId, out MRuleWorkflowStep<TContext>? step))
            {
                throw new InvalidOperationException(
                    $"Workflow '{workflow.Name}' references unknown step '{currentStepId}'.");
            }

            executionContext.CurrentStepId = currentStepId;
            executedSteps.Add(currentStepId);

            _logger.LogDebug(
                "Executing workflow step {StepId} ({StepType}) in workflow {WorkflowName}",
                step.Id,
                step.StepType,
                workflow.Name);

            string? nextStepId = await step.ExecuteAsync(executionContext, cancellationToken);
            if (step.StepType == MRuleWorkflowStepType.End)
            {
                break;
            }

            currentStepId = nextStepId;
        }

        return new MRuleWorkflowResult<TContext>
        {
            WorkflowName = workflow.Name,
            Context = context,
            Facts = executionContext.Facts,
            ExecutedSteps = executedSteps,
            State = new Dictionary<string, object?>(executionContext.State)
        };
    }
}

