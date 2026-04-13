using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestProject.Aggregate.Core.Constants;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Grpc;

namespace TestProject.Aggregate.Core.Infrastructure;

/// <summary>
/// Registers all site profiles and their services into the DI container.
/// Aggregate pattern: handler-based dispatch, no EF Core DbContext.
/// </summary>
public static class SiteServiceResolver
{
    /// <summary>
    /// Discovers all <see cref="ISiteProfile"/> implementations from the given assemblies
    /// and registers their services + per-request resolver.
    /// <para>
    /// Call from Program.cs:
    /// <code>
    /// services.AddSiteServices(configuration,
    ///     typeof(DefaultSiteProfile).Assembly,
    ///     typeof(AlphaSiteProfile).Assembly,
    ///     typeof(BravoSiteProfile).Assembly);
    /// </code>
    /// </para>
    /// </summary>
    public static IServiceCollection AddSiteServices(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] siteAssemblies)
    {
        services.AddMultiSiteProfiles(
            configuration,
            siteCodeAccessor: sp =>
            {
                // gRPC: from ISiteCodeHolder; HTTP: from HttpContext header
                var holder = sp.GetService<ISiteCodeHolder>();
                return holder?.SiteCode ?? SiteIds.DEFAULT;
            },
            assemblies: siteAssemblies
        );

        return services;
    }
}
