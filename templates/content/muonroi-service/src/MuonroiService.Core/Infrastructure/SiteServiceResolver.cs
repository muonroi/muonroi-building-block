using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MuonroiService.Core.Constants;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Grpc;
#if (isDapper)
using Autofac;
#endif

namespace MuonroiService.Core.Infrastructure;

/// <summary>
/// Registers all site profiles and their services into the DI container.
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
    ///     typeof(TciSiteProfile).Assembly);
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

#if (isDapper)
    /// <summary>
    /// Autofac container configuration for per-site named repository resolution.
    /// Call from Program.cs:
    /// <c>builder.Host.ConfigureContainer&lt;ContainerBuilder&gt;(SiteServiceResolver.Configure);</c>
    /// </summary>
    public static void Configure(ContainerBuilder cb)
    {
        // TODO: Register per-site repository implementations
        // Example:
        // cb.RegisterType<TciOrderRepository>()
        //   .Named<IOrderRepository>(SiteIds.TCI)
        //   .InstancePerLifetimeScope();
        //
        // cb.RegisterType<DefaultOrderRepository>()
        //   .Named<IOrderRepository>(SiteIds.DEFAULT)
        //   .InstancePerLifetimeScope();
        //
        // cb.Register(ctx =>
        // {
        //     string siteId = ctx.Resolve<WorkContext>().SiteId;
        //     return ctx.IsRegisteredWithName<IOrderRepository>(siteId)
        //         ? ctx.ResolveNamed<IOrderRepository>(siteId)
        //         : ctx.ResolveNamed<IOrderRepository>(SiteIds.DEFAULT);
        // })
        // .As<IOrderRepository>()
        // .InstancePerLifetimeScope();
    }
#endif
}
