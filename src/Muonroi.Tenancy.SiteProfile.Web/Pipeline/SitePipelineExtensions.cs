namespace Muonroi.Tenancy.SiteProfile.Web.Pipeline;

/// <summary>
/// DI registration extensions for the MSitePipeline infrastructure.
/// </summary>
public static class SitePipelineExtensions
{
    /// <summary>
    /// Registers the MSitePipeline infrastructure:
    /// - <see cref="SitePipelineHookRegistry"/> as singleton (stores hooks keyed by site/service/step/phase)
    /// - <see cref="MSitePipeline{TContext}"/> as open-generic scoped (resolved per-request per TContext)
    ///
    /// Call once in Program.cs after <c>AddMultiSiteProfiles()</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSitePipeline(this IServiceCollection services)
    {
        // Ensure options bag is registered even if no hooks are added
        services.AddOptions<SitePipelineHookOptions>();

        services.TryAddSingleton(sp =>
        {
            IOptions<SitePipelineHookOptions> options = sp.GetRequiredService<IOptions<SitePipelineHookOptions>>();
            return new SitePipelineHookRegistry(options);
        });

        // MSitePipeline<TContext> is open-generic scoped — resolved per-request, parameterized per service type
        services.TryAddScoped(typeof(MSitePipeline<>), typeof(MSitePipeline<>));

        return services;
    }

    /// <summary>
    /// Registers a step hook for a specific site, service, step, and phase.
    /// The service name is derived from <typeparamref name="TService"/>.
    /// </summary>
    /// <typeparam name="TService">
    /// The service type whose name is used as the registry key (e.g., <c>OrderService</c>).
    /// Must match the <c>TContext</c> type used when constructing <see cref="MSitePipeline{TContext}"/>.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="siteId">The site identifier (e.g., "sg01", "hn01").</param>
    /// <param name="stepName">The step name to hook into (e.g., "SaveOrder").</param>
    /// <param name="phase">When to run: Before, After, or Replace the default implementation.</param>
    /// <param name="factory">Factory to resolve the hook instance from DI.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteStepHook<TService>(
        this IServiceCollection services,
        string siteId,
        string stepName,
        SiteStepHookPhase phase,
        Func<IServiceProvider, ISiteStepHook> factory)
    {
        // Ensure options are configured
        services.AddOptions<SitePipelineHookOptions>();

        // Accumulate the hook registration in options — flushed into registry when registry is constructed
        services.Configure<SitePipelineHookOptions>(o =>
        {
            o.Hooks.Add(new HookRegistration(
                siteId,
                typeof(TService).Name,
                stepName,
                phase,
                factory));
        });

        return services;
    }
}
