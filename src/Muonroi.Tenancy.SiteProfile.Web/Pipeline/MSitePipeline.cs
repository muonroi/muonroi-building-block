using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Core;

namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Named-step pipeline runner that lets developers write service functions as named steps.
/// Sites can hook into ANY step via Before/After/Replace — eliminating the need for virtual overrides.
/// This is an ALTERNATIVE to virtual override pattern, not a replacement.
/// Internally delegates to <see cref="RuleOrchestrator{TContext}"/> for DFS topo-sort ordering,
/// execution tracing, and compensation support.
/// </summary>
/// <typeparam name="TContext">
/// The service context type (e.g., the calling service class).
/// Used to derive serviceName for hook registry lookups.
/// </typeparam>
public class MSitePipeline<TContext>
{
    private readonly SitePipelineHookRegistry _registry;
    private readonly ISiteProfileResolver _siteResolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMLog<MSitePipeline<TContext>>? _log;
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of <see cref="MSitePipeline{TContext}"/>.
    /// </summary>
    public MSitePipeline(
        SitePipelineHookRegistry registry,
        ISiteProfileResolver siteResolver,
        IServiceProvider serviceProvider,
        IMLog<MSitePipeline<TContext>>? log = null)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _siteResolver = siteResolver ?? throw new ArgumentNullException(nameof(siteResolver));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _log = log;
        _serviceName = typeof(TContext).Name;
    }

    /// <summary>
    /// Executes a named pipeline step with hook interception.
    /// Internally constructs a <see cref="RuleOrchestrator{TContext}"/> from adapted hooks
    /// and delegates execution, gaining DFS topo-sort ordering, tracing, and compensation.
    /// </summary>
    /// <param name="stepName">The logical name of the step (used as registry key).</param>
    /// <param name="facts">The shared fact bag — passed to defaultImpl and all hooks.</param>
    /// <param name="defaultImpl">The default step implementation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="executionMode">
    /// Controls failure behavior:
    /// - AllOrNothing: first hook failure propagates immediately.
    /// - BestEffort: all hooks run, errors aggregated in AggregateException.
    /// - CompensateOnFailure: hooks implementing ISiteCompensatableStepHook are compensated in LIFO order.
    /// </param>
    public async Task RunStep(
        string stepName,
        FactBag facts,
        Func<FactBag, CancellationToken, Task> defaultImpl,
        CancellationToken ct = default,
        ExecutionMode executionMode = ExecutionMode.AllOrNothing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        ArgumentNullException.ThrowIfNull(facts);
        ArgumentNullException.ThrowIfNull(defaultImpl);

        string siteId = _siteResolver.Current.SiteId;

        _log?.Info(
            "[Pipeline:{ServiceName}] Step '{StepName}' started for site '{SiteId}'",
            _serviceName, stepName, siteId);

        // Build rule list from registry hooks + defaultImpl
        var rules = new List<IRule<FactBag>>();
        int index = 0;

        // Add Before hooks as rules
        foreach (Func<IServiceProvider, ISiteStepHook> factory in
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Before))
        {
            ISiteStepHook hook = factory(_serviceProvider);
            rules.Add(new SiteStepHookRuleAdapter(hook, SiteStepHookPhase.Before, stepName, index++));
        }

        // Check for Replace hooks — if any exist, they substitute the default impl
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> replaceFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Replace);

        if (replaceFactories.Count > 0)
        {
            foreach (Func<IServiceProvider, ISiteStepHook> factory in replaceFactories)
            {
                ISiteStepHook hook = factory(_serviceProvider);
                rules.Add(new SiteStepHookRuleAdapter(hook, SiteStepHookPhase.Replace, stepName, index++));
            }
        }
        else
        {
            // No Replace hooks — add default impl as a rule at Order=0
            rules.Add(new DefaultImplRuleAdapter(defaultImpl, stepName));
        }

        // Add After hooks as rules
        foreach (Func<IServiceProvider, ISiteStepHook> factory in
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.After))
        {
            ISiteStepHook hook = factory(_serviceProvider);
            rules.Add(new SiteStepHookRuleAdapter(hook, SiteStepHookPhase.After, stepName, index++));
        }

        // Create orchestrator and execute
        var orchestrator = new RuleOrchestrator<FactBag>(
            rules: rules,
            hooks: Array.Empty<IHookHandler<FactBag>>(),
            logger: null);

        OrchestratorResult result = await orchestrator.ExecuteWithResultAsync(
            context: facts,
            executionMode: executionMode,
            cancellationToken: ct);

        // Handle failure — preserve existing exception behavior
        if (!result.IsSuccess)
        {
            if (executionMode == ExecutionMode.BestEffort)
            {
                throw new AggregateException(
                    $"[Pipeline:{_serviceName}] Step '{stepName}' encountered {result.Errors.Count} hook failure(s) in BestEffort mode.",
                    result.Errors.Select(e => new InvalidOperationException(e)));
            }

            // AllOrNothing / CompensateOnFailure — propagate first error
            string firstError = result.Errors.Count > 0
                ? result.Errors[0]
                : "Unknown pipeline error";
            throw new InvalidOperationException(
                $"[Pipeline:{_serviceName}] Step '{stepName}' failed: {firstError}");
        }

        _log?.Info(
            "[Pipeline:{ServiceName}] Step '{StepName}' completed for site '{SiteId}'",
            _serviceName, stepName, siteId);
    }
}
