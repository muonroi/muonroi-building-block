using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// Stores and retrieves site step hook factories keyed by (siteId, serviceName, stepName, phase).
/// Registered as a singleton — hooks are registered once at startup.
/// </summary>
public sealed class SitePipelineHookRegistry
{
    private readonly ConcurrentDictionary<(string siteId, string serviceName, string stepName, SiteStepHookPhase phase), List<Func<IServiceProvider, ISiteStepHook>>> _hooks = new();

    /// <summary>
    /// Initializes the registry. If hook options are provided, flushes them into the registry.
    /// </summary>
    public SitePipelineHookRegistry()
    {
    }

    /// <summary>
    /// Initializes the registry and populates it from options accumulated during DI configuration.
    /// </summary>
    public SitePipelineHookRegistry(IOptions<SitePipelineHookOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (HookRegistration reg in options.Value.Hooks)
        {
            Register(reg.SiteId, reg.ServiceName, reg.StepName, reg.Phase, reg.Factory);
        }
    }

    /// <summary>
    /// Registers a hook factory for the given composite key.
    /// </summary>
    public void Register(
        string siteId,
        string serviceName,
        string stepName,
        SiteStepHookPhase phase,
        Func<IServiceProvider, ISiteStepHook> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepName);
        ArgumentNullException.ThrowIfNull(factory);

        var key = (siteId, serviceName, stepName, phase);
        _hooks.AddOrUpdate(key,
            _ => [factory],
            (_, existing) =>
            {
                lock (existing)
                {
                    existing.Add(factory);
                    return existing;
                }
            });
    }

    /// <summary>
    /// Returns all hook factories registered for the given composite key.
    /// Returns an empty list if no hooks are registered.
    /// </summary>
    public IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> GetHookFactories(
        string siteId,
        string serviceName,
        string stepName,
        SiteStepHookPhase phase)
    {
        var key = (siteId, serviceName, stepName, phase);
        if (_hooks.TryGetValue(key, out List<Func<IServiceProvider, ISiteStepHook>>? factories))
        {
            return factories;
        }

        return [];
    }
}
