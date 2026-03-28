using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile.Web.HotReload;
using Muonroi.Tenancy.SiteProfile.Web.Telemetry;

namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Extension methods for registering SiteProfile Web components (middleware + hot-reload).
/// </summary>
public static class SiteProfileWebExtensions
{
    /// <summary>
    /// Adds SiteProfile state registry and hot-reload client as a hosted service.
    /// The state registry is the mutable bridge between hot-reload events and middleware.
    /// </summary>
    public static IServiceCollection AddSiteProfileHotReload(
        this IServiceCollection services,
        Action<SiteProfileHotReloadOptions> configure)
    {
        var options = new SiteProfileHotReloadOptions();
        configure(options);
        services.AddSingleton(options);
        services.AddSingleton<ISiteProfileStateRegistry, SiteProfileStateRegistry>();
        services.AddHostedService<SiteProfileHotReloadClient>();
        return services;
    }

    /// <summary>
    /// Adds middleware that returns 503 for disabled sites. Place after UseRouting and any tenant resolution middleware.
    /// </summary>
    public static IApplicationBuilder UseSiteProfileStateMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SiteProfileStateMiddleware>();
    }

    /// <summary>
    /// Adds middleware that enriches each request with site-scoped observability:
    /// Activity.Current site.id tag, IMLog SiteId scope, and site_profile_requests_total counter.
    ///
    /// Place after <see cref="UseSiteProfileStateMiddleware"/> in the pipeline.
    /// Requires <see cref="Muonroi.Tenancy.SiteProfile.ISiteProfileResolver"/> to be registered (via AddMultiSiteProfiles).
    /// When resolver is absent, the middleware is a no-op passthrough.
    /// </summary>
    public static IApplicationBuilder UseSiteProfileTelemetry(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SiteProfileTelemetryMiddleware>();
    }
}
