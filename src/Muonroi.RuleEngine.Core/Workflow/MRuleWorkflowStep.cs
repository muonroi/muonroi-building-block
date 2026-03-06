namespace Muonroi.RuleEngine.Core.Workflow;

/// <summary>
/// Workflow step categories aligned with BPMN-like tasks/gateways.
/// </summary>
public enum MRuleWorkflowStepType
{
    Start = 0,
    RuleTask = 1,
    ServiceTask = 2,
    ExclusiveGateway = 3,
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

    public string Id { get; }
    public string Name { get; }
    public MRuleWorkflowStepType StepType { get; }

    internal Task<string?> ExecuteAsync(
        MRuleWorkflowExecutionContext<TContext> context,
        CancellationToken cancellationToken)
    {
        return _execute(context, cancellationToken);
    }

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

