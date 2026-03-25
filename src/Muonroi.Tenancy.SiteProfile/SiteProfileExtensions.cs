using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Extension methods for ISiteProfile registration.
/// </summary>
public static class SiteProfileExtensions
{
    /// <summary>
    /// Register all services for a specific site profile.
    /// One-liner in Program.cs: services.AddSiteProfile&lt;Sg01Profile&gt;(config)
    /// </summary>
    public static IServiceCollection AddSiteProfile<TProfile>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TProfile : class, ISiteProfile, new()
    {
        var profile = new TProfile();
        services.AddSingleton<ISiteProfile>(profile);
        profile.RegisterServices(services, configuration);
        return services;
    }

    /// <summary>
    /// Register a site profile from an existing instance.
    /// Useful when profile needs constructor injection or runtime configuration.
    /// </summary>
    public static IServiceCollection AddSiteProfile(
        this IServiceCollection services,
        ISiteProfile profile,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(profile);
        services.AddSingleton(profile);
        profile.RegisterServices(services, configuration);
        return services;
    }
}
