using Muonroi.Core.Abstractions.Guards;
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
        MGuard.NotNull(options);
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
        MGuard.NotEmpty(siteId);
        MGuard.NotEmpty(serviceName);
        MGuard.NotEmpty(stepName);
        MGuard.NotNull(factory);

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
    /// Fallback site ID used for default hooks that apply to ALL sites
    /// unless a site registers its own override for the same (service, step, phase).
    /// Register with <c>siteId: SitePipelineHookRegistry.FallbackSiteId</c>.
    /// </summary>
    public const string FallbackSiteId = "*";

    /// <summary>
    /// Returns hook factories for the given composite key with fallback support.
    /// Lookup order:
    /// <list type="number">
    /// <item>Exact match: (siteId, serviceName, stepName, phase)</item>
    /// <item>Fallback: ("*", serviceName, stepName, phase) — if no exact match found</item>
    /// </list>
    /// This enables "default hooks" that apply to all sites without explicit per-site registration.
    /// A site overrides fallback by registering its own hook for the same (service, step, phase).
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

        // Fallback: check for wildcard ("*") hooks
        if (siteId != FallbackSiteId)
        {
            var fallbackKey = (FallbackSiteId, serviceName, stepName, phase);
            if (_hooks.TryGetValue(fallbackKey, out List<Func<IServiceProvider, ISiteStepHook>>? fallbackFactories))
            {
                return fallbackFactories;
            }
        }

        return [];
    }
}
