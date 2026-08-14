namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Adapts an <see cref="ISiteStepHook"/> to <see cref="ICompensatableRule{TContext}"/>
/// so the hook can participate in a <c>RuleOrchestrator</c> pipeline.
/// Phase-based ordering: Before = -1000 + hook.Order, Default (Replace) = hook.Order, After = 1000 + hook.Order.
/// If the wrapped hook implements <see cref="ISiteCompensatableStepHook"/>,
/// compensation is delegated; otherwise <see cref="CompensateAsync"/> is a no-op.
/// </summary>
internal sealed class SiteStepHookRuleAdapter(
    ISiteStepHook hook,
    SiteStepHookPhase phase,
    string stepName,
    int hookIndex) : ICompensatableRule<FactBag>
{
    private readonly ISiteStepHook _hook = hook;
    private readonly int _phaseOrder = phase switch
    {
        SiteStepHookPhase.Before => -1000 + hook.Order,
        SiteStepHookPhase.After => 1000 + hook.Order,
        SiteStepHookPhase.Replace => hook.Order,
        _ => hook.Order
    };

    /// <inheritdoc />
    public string Code { get; } = $"{stepName}.{phase}.{hookIndex}.{hook.GetType().Name}";

    /// <inheritdoc />
    public string Name => _hook.GetType().Name;

    /// <inheritdoc />
    public int Order => _phaseOrder;

    /// <inheritdoc />
    public IReadOnlyList<string> DependsOn => _hook.DependsOn;

    /// <inheritdoc />
    public RuleType Type => RuleType.Business;

    /// <inheritdoc />
    public HookPoint HookPoint => HookPoint.BeforeRule;

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => [];

    /// <summary>
    /// Always returns <see cref="RuleResult.Passed"/> — hooks are unconditional.
    /// </summary>
    public Task<RuleResult> EvaluateAsync(FactBag ctx, FactBag facts, CancellationToken ct)
        => Task.FromResult(RuleResult.Passed());

    /// <summary>
    /// Delegates to the wrapped <see cref="ISiteStepHook.ExecuteAsync"/>.
    /// </summary>
    public async Task ExecuteAsync(FactBag context, CancellationToken cancellationToken = default)
        => await _hook.ExecuteAsync(context, cancellationToken);

    /// <summary>
    /// Delegates to <see cref="ISiteCompensatableStepHook.CompensateAsync"/> if the hook supports it;
    /// otherwise completes immediately.
    /// </summary>
    public async Task CompensateAsync(FactBag context, FactBag facts, CancellationToken cancellationToken = default)
    {
        if (_hook is ISiteCompensatableStepHook compensatable)
        {
            await compensatable.CompensateAsync(context, cancellationToken);
        }
    }
}

/// <summary>
/// Adapts a <c>Func&lt;FactBag, CancellationToken, Task&gt;</c> (the default step implementation)
/// to <see cref="IRule{TContext}"/> so it can participate in a <c>RuleOrchestrator</c> pipeline
/// alongside <see cref="SiteStepHookRuleAdapter"/> instances.
/// Order is always 0 (between Before hooks at -1000 and After hooks at +1000).
/// </summary>
internal sealed class DefaultImplRuleAdapter(Func<FactBag, CancellationToken, Task> impl, string stepName) : IRule<FactBag>
{
    private readonly Func<FactBag, CancellationToken, Task> _impl = impl;

    /// <inheritdoc />
    public string Code { get; } = $"{stepName}.Default";

    /// <inheritdoc />
    public string Name => "DefaultImpl";

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public IReadOnlyList<string> DependsOn => [];

    /// <inheritdoc />
    public RuleType Type => RuleType.Business;

    /// <inheritdoc />
    public HookPoint HookPoint => HookPoint.BeforeRule;

    /// <inheritdoc />
    public IEnumerable<Type> Dependencies => [];

    /// <summary>
    /// Always returns <see cref="RuleResult.Passed"/> — default impl is unconditional.
    /// </summary>
    public Task<RuleResult> EvaluateAsync(FactBag ctx, FactBag facts, CancellationToken ct)
        => Task.FromResult(RuleResult.Passed());

    /// <summary>
    /// Delegates to the wrapped default implementation function.
    /// </summary>
    public async Task ExecuteAsync(FactBag context, CancellationToken cancellationToken = default)
        => await _impl(context, cancellationToken);
}
