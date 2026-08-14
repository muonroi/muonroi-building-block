using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// DI extensions for gRPC site resolution.
/// </summary>
public static class SiteGrpcExtensions
{
    /// <summary>
    /// Registers gRPC site resolution services with custom options.
    /// Consumer defines which metadata key carries the SiteCode.
    /// <para>
    /// <code>
    /// // Consumer A: SiteCode comes via PascalCase metadata
    /// services.AddSiteGrpcServices(o => o.MetadataKey = "SiteCode");
    ///
    /// // Consumer B: SiteCode comes via kebab-case header
    /// services.AddSiteGrpcServices(o => {
    ///     o.MetadataKey = "x-site-code";
    ///     o.HttpHeaderFallbackKey = "x-site-code";
    /// });
    ///
    /// // Then add interceptor to gRPC pipeline:
    /// services.AddGrpc(o =&gt; o.Interceptors.Add&lt;SiteCodeGrpcInterceptor&gt;());
    /// </code>
    /// </para>
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional configuration for metadata key and behavior.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSiteGrpcServices(
        this IServiceCollection services,
        Action<SiteGrpcOptions>? configure = null)
    {
        services.AddScoped<ISiteCodeHolder, SiteCodeHolder>();

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            // Default: MetadataKey = "SiteCode" (common PascalCase convention)
            services.Configure<SiteGrpcOptions>(_ => { });
        }

        services.AddSingleton<SiteCodeGrpcInterceptor>();
        return services;
    }

    /// <summary>
    /// Registers a per-site gRPC client binding with the <see cref="ISiteGrpcClientFactory"/>.
    ///
    /// Call once per site-client pair during startup. Multiple calls for different sites
    /// (or different service names) stack without conflict.
    ///
    /// <example>
    /// <code>
    /// // Program.cs — register per-site clients
    /// services.AddGrpcClient&lt;TciFcdClient&gt;(o => o.Address = new Uri("https://tci-grpc:5001"));
    /// services.AddGrpcClient&lt;DefaultFcdClient&gt;(o => o.Address = new Uri("https://default-grpc:5001"));
    ///
    /// services.AddSiteGrpcClientFactory();
    /// services.AddSiteGrpcClient&lt;TciSite.FcdClient&gt;("TCI", "fcd");
    /// services.AddSiteGrpcClient&lt;DefaultSite.FcdClient&gt;("DEFAULT", "fcd");
    ///
    /// // Aggregate service — no if/switch on SiteCode:
    /// public class FcdAggregator(ISiteGrpcClientFactory factory)
    /// {
    ///     public async Task ProcessAsync()
    ///     {
    ///         var client = factory.CreateForCurrentSite&lt;FcdServiceBase&gt;("fcd");
    ///         await client.CreateAsync(request);
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="TClient">
    /// The concrete gRPC client type (must be registered via <c>services.AddGrpcClient&lt;TClient&gt;</c> separately).
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="siteId">
    /// The site key — must match <c>ISiteProfile.SiteId</c> exactly (case-sensitive).
    /// Use <c>"default"</c> as the fallback site key.
    /// </param>
    /// <param name="serviceName">
    /// A logical name for the gRPC service (e.g., <c>"fcd"</c>).
    /// Multiple client types for different sites share the same service name.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSiteGrpcClient<TClient>(
        this IServiceCollection services,
        string siteId,
        string serviceName)
        where TClient : ClientBase
    {
        MGuard.NotEmpty(siteId);
        MGuard.NotEmpty(serviceName);

        // Each call registers a descriptor. AddSiteGrpcClientFactory() reads all of them.
        services.AddSingleton(new SiteGrpcClientDescriptor(siteId, serviceName, typeof(TClient)));
        return services;
    }

    /// <summary>
    /// Registers <see cref="ISiteGrpcClientFactory"/> and builds the <see cref="SiteGrpcClientRegistry"/>
    /// from all <see cref="SiteGrpcClientDescriptor"/> entries added via <see cref="AddSiteGrpcClient{TClient}"/>.
    ///
    /// Call ONCE in Program.cs after all <c>AddSiteGrpcClient</c> calls.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSiteGrpcClientFactory(this IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var registry = new SiteGrpcClientRegistry();
            foreach (SiteGrpcClientDescriptor desc in sp.GetServices<SiteGrpcClientDescriptor>())
            {
                registry.Add(desc);
            }
            return registry;
        });

        // Accessor for root-level GrpcClientFactory — works around Autofac scoped resolution issue
        services.AddSingleton<GrpcClientFactoryAccessor>();

        services.AddScoped<ISiteGrpcClientFactory, SiteGrpcClientFactory>();
        return services;
    }

    /// <summary>
    /// Captures the root-level <see cref="GrpcClientFactory"/> from <c>WebApplication.Services</c>
    /// (the MS DI root) so that Autofac-scoped resolution can use it.
    /// <para>
    /// <b>MUST</b> be called in Program.cs after <c>builder.Build()</c> and before <c>app.Run()</c>.
    /// </para>
    /// <code>
    /// WebApplication app = builder.Build();
    /// app.InitializeSiteGrpcClients(); // &lt;-- required for Autofac compatibility
    ///  await app.RunAsync();
    /// </code>
    /// </summary>
    /// <param name="app">The built web application.</param>
    public static Microsoft.AspNetCore.Builder.WebApplication InitializeSiteGrpcClients(
        this Microsoft.AspNetCore.Builder.WebApplication app)
    {
        var accessor = app.Services.GetService<GrpcClientFactoryAccessor>();
        if (accessor is null)
        {
            return app; // AddSiteGrpcClientFactory() not called — no-op
        }

        var factory = app.Services.GetService<GrpcClientFactory>();
        var registry = app.Services.GetService<SiteGrpcClientRegistry>();
        if (factory is not null && registry is not null)
        {
            accessor.Initialize(factory, registry);
        }

        return app;
    }

    /// <summary>
    /// Registers a site-specific gRPC handler as a keyed scoped service.
    /// The handler inherits from the shared proto base class and overrides RPCs as needed.
    /// Use <c>"default"</c> as <paramref name="siteId"/> for the fallback handler.
    ///
    /// <para>
    /// Consumer registers one handler per site + one "default" fallback:
    /// <code>
    /// services.AddSiteGrpcHandler&lt;FullContainerDeliveryBase, TciFcdService&gt;("TCI");
    /// services.AddSiteGrpcHandler&lt;FullContainerDeliveryBase, SharedFcdService&gt;("default");
    /// </code>
    /// </para>
    ///
    /// <para>
    /// Resolution order at dispatch time (via <see cref="SiteGrpcDispatchHelper{TServiceBase}"/>):
    /// <list type="number">
    ///   <item>Exact <paramref name="siteId"/> match</item>
    ///   <item>Fallback to <c>"default"</c></item>
    ///   <item><c>RpcException</c> with <c>StatusCode.Internal</c> if neither registered</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <typeparam name="TServiceBase">
    /// The proto-generated gRPC service base class (e.g., <c>FullContainerDeliveryBase</c>).
    /// </typeparam>
    /// <typeparam name="TImpl">
    /// The concrete implementation class. Must derive from <typeparamref name="TServiceBase"/>.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="siteId">
    /// The site key (e.g., <c>"TCI"</c>, <c>"BRAVO"</c>). Use <c>"default"</c> for the fallback handler.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSiteGrpcHandler<TServiceBase, TImpl>(
        this IServiceCollection services,
        string siteId)
        where TServiceBase : class
        where TImpl : class, TServiceBase
    {
        MGuard.NotEmpty(siteId);
        services.AddKeyedScoped<TServiceBase, TImpl>(siteId);
        return services;
    }

    /// <summary>
    /// Registers a unified facade client for a specific site using keyed DI.
    /// The facade implementation (generated by [GenerateSiteGrpcFacade]) wraps shared + per-site
    /// gRPC clients behind one interface.
    ///
    /// <para>
    /// Call once per site-facade pair during startup. Use <c>"default"</c> as the fallback siteId.
    /// <code>
    /// // Program.cs — register per-site facade clients
    /// services.AddSiteGrpcFacadeClient&lt;ITciFcdClient, TciFcdClientFacade&gt;("TCI", "fcd");
    /// services.AddSiteGrpcFacadeClient&lt;ITciFcdClient, TciFcdClientFacade&gt;("default", "fcd");
    ///
    /// // Aggregate service — single facade for all RPCs:
    /// public class FcdAggregator(ISiteGrpcClientFactory factory)
    /// {
    ///     public async Task ProcessAsync()
    ///     {
    ///         var facade = factory.CreateFacadeForCurrentSite&lt;ITciFcdClient&gt;("fcd");
    ///         var result = await facade.CreateV4Async(request);
    ///     }
    /// }
    /// </code>
    /// </para>
    /// </summary>
    /// <typeparam name="TFacade">
    /// The facade interface type (must be decorated with [GenerateSiteGrpcFacade] and partially implemented).
    /// </typeparam>
    /// <typeparam name="TImpl">
    /// The generated facade implementation class (e.g., <c>TciFcdClientFacade</c>).
    /// Must implement <typeparamref name="TFacade"/>.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="siteId">
    /// The site key — must match <c>ISiteProfile.SiteId</c> exactly (case-sensitive).
    /// Use <c>"default"</c> as the fallback site key.
    /// </param>
    /// <param name="serviceName">
    /// A logical name for the gRPC service (e.g., <c>"fcd"</c>).
    /// Must match the name used in <see cref="ISiteGrpcClientFactory.CreateFacadeForCurrentSite{TFacade}"/>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSiteGrpcFacadeClient<TFacade, TImpl>(
        this IServiceCollection services,
        string siteId,
        string serviceName)
        where TFacade : class
        where TImpl : class, TFacade
    {
        MGuard.NotEmpty(siteId);
        MGuard.NotEmpty(serviceName);

        services.AddKeyedScoped($"facade:{serviceName}:{siteId}", (sp, _) =>
        {
            var accessor = sp.GetRequiredService<GrpcClientFactoryAccessor>();

            // Generated facades have ClientBase-only constructors.
            var ctors = typeof(TImpl).GetConstructors();
            var ctor = ctors.FirstOrDefault(c => c.GetParameters()
                    .All(p => typeof(ClientBase).IsAssignableFrom(p.ParameterType)))
                  ?? ctors[0];

            var ctorParams = ctor.GetParameters();
            var args = new object[ctorParams.Length];
            for (int i = 0; i < ctorParams.Length; i++)
            {
                var paramType = ctorParams[i].ParameterType;
                args[i] = accessor.CreateClient(paramType, serviceName)
                    ?? sp.GetService(paramType)
                    ?? MGuard.Fail<object>(
                        $"Cannot resolve gRPC client '{paramType.Name}' for facade '{typeof(TImpl).Name}'. " +
                        $"Ensure AddGrpcClient<{paramType.Name}>() is registered and " +
                        $"app.InitializeSiteGrpcClients() is called in Program.cs.");
            }
            return (TFacade)ctor.Invoke(args);
        });
        return services;
    }


    /// <summary>
    /// Registers <see cref="SiteGrpcDispatchHelper{TServiceBase}"/> as a scoped service for the
    /// specified proto base class.
    ///
    /// <para>
    /// Consumer still maps their dispatcher proxy via <c>MapGrpcService</c> in endpoint config.
    /// The helper is injected into the dispatcher's constructor.
    /// <code>
    /// // Program.cs — registration
    /// services.AddSiteGrpcHandler&lt;FullContainerDeliveryBase, TciFcdService&gt;("TCI");
    /// services.AddSiteGrpcHandler&lt;FullContainerDeliveryBase, SharedFcdService&gt;("default");
    /// services.AddSiteGrpcDispatcher&lt;FullContainerDeliveryBase&gt;();
    ///
    /// // Endpoint mapping
    /// app.MapGrpcService&lt;FcdDispatcher&gt;();
    ///
    /// // Dispatcher implementation
    /// public class FcdDispatcher : FullContainerDeliveryBase
    /// {
    ///     private readonly SiteGrpcDispatchHelper&lt;FullContainerDeliveryBase&gt; _helper;
    ///     public FcdDispatcher(SiteGrpcDispatchHelper&lt;FullContainerDeliveryBase&gt; helper)
    ///         =&gt; _helper = helper;
    ///
    ///     public override Task&lt;CreateV4Reply&gt; CreateV4(CreateV4Request req, ServerCallContext ctx)
    ///         =&gt; _helper.DispatchAsync(ctx, (h, c) =&gt; h.CreateV4(req, c));
    /// }
    /// </code>
    /// </para>
    /// </summary>
    /// <typeparam name="TServiceBase">
    /// The proto-generated gRPC service base class (e.g., <c>FullContainerDeliveryBase</c>).
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddSiteGrpcDispatcher<TServiceBase>(
        this IServiceCollection services)
        where TServiceBase : class
    {
        services.AddScoped<SiteGrpcDispatchHelper<TServiceBase>>();
        return services;
    }
}
