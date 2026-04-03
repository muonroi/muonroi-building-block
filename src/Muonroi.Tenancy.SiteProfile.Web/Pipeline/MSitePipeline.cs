using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Named-step pipeline runner that lets developers write service functions as named steps.
/// Sites can hook into ANY step via Before/After/Replace — eliminating the need for virtual overrides.
/// This is an ALTERNATIVE to virtual override pattern, not a replacement.
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
    /// </summary>
    /// <param name="stepName">The logical name of the step (used as registry key).</param>
    /// <param name="facts">The shared fact bag — passed to defaultImpl and all hooks.</param>
    /// <param name="defaultImpl">The default step implementation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="executionMode">
    /// Controls failure behavior:
    /// - AllOrNothing: first hook failure propagates immediately.
    /// - BestEffort: all hooks run, errors aggregated in AggregateException.
    /// - CompensateOnFailure: not supported in v1 (throws NotSupportedException).
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

        if (executionMode == ExecutionMode.CompensateOnFailure)
        {
            throw new NotSupportedException(
                "ExecutionMode.CompensateOnFailure is not supported by MSitePipeline in v1. " +
                "Use AllOrNothing or BestEffort.");
        }

        string siteId = _siteResolver.Current.SiteId;

        _log?.Info(
            "[Pipeline:{ServiceName}] Step '{StepName}' started for site '{SiteId}'",
            _serviceName, stepName, siteId);

        if (executionMode == ExecutionMode.BestEffort)
        {
            await RunStepBestEffort(stepName, facts, defaultImpl, siteId, ct);
        }
        else
        {
            await RunStepAllOrNothing(stepName, facts, defaultImpl, siteId, ct);
        }

        _log?.Info(
            "[Pipeline:{ServiceName}] Step '{StepName}' completed for site '{SiteId}'",
            _serviceName, stepName, siteId);
    }

    private async Task RunStepAllOrNothing(
        string stepName,
        FactBag facts,
        Func<FactBag, CancellationToken, Task> defaultImpl,
        string siteId,
        CancellationToken ct)
    {
        // Execute Before hooks
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> beforeFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Before);

        foreach (Func<IServiceProvider, ISiteStepHook> factory in beforeFactories)
        {
            ISiteStepHook hook = factory(_serviceProvider);
            _log?.Info(
                "[Pipeline:{ServiceName}] Hook 'Before' for step '{StepName}' on site '{SiteId}'",
                _serviceName, stepName, siteId);
            await hook.ExecuteAsync(facts, ct);
        }

        // Check for Replace hooks — if any exist, skip defaultImpl
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> replaceFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Replace);

        if (replaceFactories.Count > 0)
        {
            foreach (Func<IServiceProvider, ISiteStepHook> factory in replaceFactories)
            {
                ISiteStepHook hook = factory(_serviceProvider);
                _log?.Info(
                    "[Pipeline:{ServiceName}] Hook 'Replace' for step '{StepName}' on site '{SiteId}'",
                    _serviceName, stepName, siteId);
                await hook.ExecuteAsync(facts, ct);
            }
        }
        else
        {
            // No Replace hooks — execute default implementation
            await defaultImpl(facts, ct);
        }

        // Execute After hooks
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> afterFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.After);

        foreach (Func<IServiceProvider, ISiteStepHook> factory in afterFactories)
        {
            ISiteStepHook hook = factory(_serviceProvider);
            _log?.Info(
                "[Pipeline:{ServiceName}] Hook 'After' for step '{StepName}' on site '{SiteId}'",
                _serviceName, stepName, siteId);
            await hook.ExecuteAsync(facts, ct);
        }
    }

    private async Task RunStepBestEffort(
        string stepName,
        FactBag facts,
        Func<FactBag, CancellationToken, Task> defaultImpl,
        string siteId,
        CancellationToken ct)
    {
        var errors = new List<Exception>();

        // Execute Before hooks
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> beforeFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Before);

        foreach (Func<IServiceProvider, ISiteStepHook> factory in beforeFactories)
        {
            try
            {
                ISiteStepHook hook = factory(_serviceProvider);
                _log?.Info(
                    "[Pipeline:{ServiceName}] Hook 'Before' for step '{StepName}' on site '{SiteId}'",
                    _serviceName, stepName, siteId);
                await hook.ExecuteAsync(facts, ct);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        // Check for Replace hooks — if any exist, skip defaultImpl
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> replaceFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.Replace);

        if (replaceFactories.Count > 0)
        {
            foreach (Func<IServiceProvider, ISiteStepHook> factory in replaceFactories)
            {
                try
                {
                    ISiteStepHook hook = factory(_serviceProvider);
                    _log?.Info(
                        "[Pipeline:{ServiceName}] Hook 'Replace' for step '{StepName}' on site '{SiteId}'",
                        _serviceName, stepName, siteId);
                    await hook.ExecuteAsync(facts, ct);
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }
        }
        else
        {
            try
            {
                await defaultImpl(facts, ct);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        // Execute After hooks
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> afterFactories =
            _registry.GetHookFactories(siteId, _serviceName, stepName, SiteStepHookPhase.After);

        foreach (Func<IServiceProvider, ISiteStepHook> factory in afterFactories)
        {
            try
            {
                ISiteStepHook hook = factory(_serviceProvider);
                _log?.Info(
                    "[Pipeline:{ServiceName}] Hook 'After' for step '{StepName}' on site '{SiteId}'",
                    _serviceName, stepName, siteId);
                await hook.ExecuteAsync(facts, ct);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(
                $"[Pipeline:{_serviceName}] Step '{stepName}' encountered {errors.Count} hook failure(s) in BestEffort mode.",
                errors);
        }
    }
}
