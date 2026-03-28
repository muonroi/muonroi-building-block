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
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

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

        services.AddScoped<ISiteGrpcClientFactory, SiteGrpcClientFactory>();
        return services;
    }
}
