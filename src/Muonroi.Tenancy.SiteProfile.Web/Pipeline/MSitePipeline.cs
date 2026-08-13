using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
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
/// <remarks>
/// Initializes a new instance of <see cref="MSitePipeline{TContext}"/>.
/// </remarks>
public class MSitePipeline<TContext>(
    SitePipelineHookRegistry registry,
    ISiteProfileResolver siteResolver,
    IServiceProvider serviceProvider,
    IMLog<MSitePipeline<TContext>>? log = null)
{
    private readonly SitePipelineHookRegistry _registry = MGuard.NotNull(registry);
    private readonly ISiteProfileResolver _siteResolver = MGuard.NotNull(siteResolver);
    private readonly IServiceProvider _serviceProvider = MGuard.NotNull(serviceProvider);
    private readonly IMLog<MSitePipeline<TContext>>? _log = log;
    private readonly string _serviceName = typeof(TContext).Name;

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
        MGuard.NotEmpty(stepName);
        MGuard.NotNull(facts);
        MGuard.NotNull(defaultImpl);

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

        // Check for Replace hooks — if any exist, they substitute the default impl entirely
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> replaceFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Replace);

        // Check for Wrap hooks — they compose around the default impl (decorator pattern)
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> wrapFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Wrap);

        if (replaceFactories.Count > 0)
        {
            // Replace takes priority — Wrap hooks ignored when Replace is registered
            foreach (Func<IServiceProvider, ISiteStepHook> factory in replaceFactories)
            {
                ISiteStepHook hook = factory(_serviceProvider);
                rules.Add(new SiteStepHookRuleAdapter(hook, SiteStepHookPhase.Replace, stepName, index++));
            }
        }
        else if (wrapFactories.Count > 0)
        {
            // Wrap hooks compose around defaultImpl (innermost = default, outermost = last wrapper)
            // Build the chain: wrapper3( wrapper2( wrapper1( defaultImpl ) ) )
            Func<FactBag, CancellationToken, Task> composed = defaultImpl;
            foreach (Func<IServiceProvider, ISiteStepHook> factory in wrapFactories)
            {
                ISiteStepHook hook = factory(_serviceProvider);
                if (hook is ISiteWrapperHook wrapper)
                {
                    Func<FactBag, CancellationToken, Task> inner = composed; // capture for closure
                    composed = (f, ct2) => wrapper.WrapAsync(f, inner, ct2);
                }
            }
            rules.Add(new DefaultImplRuleAdapter(composed, stepName));
        }
        else
        {
            // No Replace or Wrap hooks — add default impl as a rule at Order=0
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
#pragma warning disable MSTD0001 // AggregateException preserves per-step inner exceptions for BestEffort pipeline inspection
                throw new AggregateException(
                    $"[Pipeline:{_serviceName}] Step '{stepName}' encountered {result.Errors.Count} hook failure(s) in BestEffort mode.",
                    result.Errors.Select(e => new MInternalException(e)));
#pragma warning restore MSTD0001
            }

            // AllOrNothing / CompensateOnFailure — propagate first error
            string firstError = result.Errors.Count > 0
                ? result.Errors[0]
                : "Unknown pipeline error";
            MGuard.State(false, $"[Pipeline:{_serviceName}] Step '{stepName}' failed: {firstError}");
        }

        _log?.Info(
            "[Pipeline:{ServiceName}] Step '{StepName}' completed for site '{SiteId}'",
            _serviceName, stepName, siteId);
    }
}
