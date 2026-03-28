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
}
