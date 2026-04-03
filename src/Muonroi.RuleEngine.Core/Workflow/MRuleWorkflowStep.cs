namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Workflow step categories aligned with BPMN-like tasks/gateways.
/// </summary>
public enum MRuleWorkflowStepType
{
    /// <summary>The entry point of the workflow.</summary>
    Start = 0,
    /// <summary>A task that executes a rule engine set.</summary>
    RuleTask = 1,
    /// <summary>A task that executes custom logic or external services.</summary>
    ServiceTask = 2,
    /// <summary>A gateway that diverts the flow based on a condition.</summary>
    ExclusiveGateway = 3,
    /// <summary>An end point of the workflow.</summary>
    End = 4
}

/// <summary>
/// Single executable workflow step.
/// </summary>
/// <typeparam name="TContext">Workflow context type.</typeparam>
public sealed class MRuleWorkflowStep<TContext>
{
    private readonly Func<MRuleWorkflowExecutionContext<TContext>, CancellationToken, Task<string?>> _execute;

    private MRuleWorkflowStep(
        string id,
        string name,
        MRuleWorkflowStepType stepType,
        Func<MRuleWorkflowExecutionContext<TContext>, CancellationToken, Task<string?>> execute)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Step id is required.", nameof(id));
        }

        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        StepType = stepType;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <summary>
    /// Gets the unique identifier for the step.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the display name for the step.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the category of the workflow step.
    /// </summary>
    public MRuleWorkflowStepType StepType { get; }

    internal Task<string?> ExecuteAsync(
        MRuleWorkflowExecutionContext<TContext> context,
        CancellationToken cancellationToken)
    {
        return _execute(context, cancellationToken);
    }

    /// <summary>
    /// Creates a start step.
    /// </summary>
    /// <param name="id">The step identifier.</param>
    /// <param name="nextStepId">The identifier of the next step.</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>A new <see cref="MRuleWorkflowStep{TContext}"/> representing a start node.</returns>
    public static MRuleWorkflowStep<TContext> Start(
        string id,
        string nextStepId,
        string? name = null)
    {
        return new MRuleWorkflowStep<TContext>(
            id,
            name ?? "Start",
            MRuleWorkflowStepType.Start,
            (_, _) => Task.FromResult<string?>(nextStepId));
    }

    /// <summary>
    /// Creates a rule task step.
    /// </summary>
    /// <param name="id">The step identifier.</param>
    /// <param name="nextStepId">The identifier of the next step.</param>
    /// <param name="modeOverride">Optional rule execution mode override.</param>
    /// <param name="traditionalPath">Optional delegate for traditional rule execution.</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>A new <see cref="MRuleWorkflowStep{TContext}"/> representing a rule execution task.</returns>
    public static MRuleWorkflowStep<TContext> RuleTask(
        string id,
        string nextStepId,
        RuleExecutionMode? modeOverride = null,
        Func<MRuleWorkflowExecutionContext<TContext>, CancellationToken, Task>? traditionalPath = null,
        string? name = null)
    {
        return new MRuleWorkflowStep<TContext>(
            id,
            name ?? "RuleTask",
            MRuleWorkflowStepType.RuleTask,
            async (ctx, ct) =>
            {
                Func<CancellationToken, Task>? traditionalDelegate = null;
                if (traditionalPath is not null)
                {
                    traditionalDelegate = token => traditionalPath(ctx, token);
                }

                FactBag facts = await ctx.RuleRouter.ExecuteAsync(
                    ctx.Context,
                    traditionalDelegate,
                    modeOverride,
                    ct);
                MergeFacts(ctx.Facts, facts);
                return nextStepId;
            });
    }

    /// <summary>
    /// Creates a service task step.
    /// </summary>
    /// <param name="id">The step identifier.</param>
    /// <param name="nextStepId">The identifier of the next step.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>A new <see cref="MRuleWorkflowStep{TContext}"/> representing a custom logic task.</returns>
    public static MRuleWorkflowStep<TContext> ServiceTask(
        string id,
        string nextStepId,
        Func<MRuleWorkflowExecutionContext<TContext>, CancellationToken, Task> action,
        string? name = null)
    {
        return new MRuleWorkflowStep<TContext>(
            id,
            name ?? "ServiceTask",
            MRuleWorkflowStepType.ServiceTask,
            async (ctx, ct) =>
            {
                await action(ctx, ct);
                return nextStepId;
            });
    }

    /// <summary>
    /// Creates an exclusive gateway step.
    /// </summary>
    /// <param name="id">The step identifier.</param>
    /// <param name="decision">The delegate that determines the next step identifier.</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>A new <see cref="MRuleWorkflowStep{TContext}"/> representing a conditional gateway.</returns>
    public static MRuleWorkflowStep<TContext> ExclusiveGateway(
        string id,
        Func<MRuleWorkflowExecutionContext<TContext>, CancellationToken, Task<string>> decision,
        string? name = null)
    {
        return new MRuleWorkflowStep<TContext>(
            id,
            name ?? "ExclusiveGateway",
            MRuleWorkflowStepType.ExclusiveGateway,
            async (ctx, ct) =>
            {
                string next = await decision(ctx, ct);
                return next;
            });
    }

    /// <summary>
    /// Creates an end step.
    /// </summary>
    /// <param name="id">The step identifier.</param>
    /// <param name="name">Optional display name.</param>
    /// <returns>A new <see cref="MRuleWorkflowStep{TContext}"/> representing a workflow termination node.</returns>
    public static MRuleWorkflowStep<TContext> End(
        string id,
        string? name = null)
    {
        return new MRuleWorkflowStep<TContext>(
            id,
            name ?? "End",
            MRuleWorkflowStepType.End,
            (_, _) => Task.FromResult<string?>(null));
    }

    private static void MergeFacts(FactBag destination, FactBag source)
    {
        foreach ((string key, object? value) in source.AsReadOnly())
        {
            destination[key] = value;
        }
    }
}

