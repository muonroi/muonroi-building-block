using Muonroi.Core.Abstractions.Guards;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Endpoint routing extensions for mapping site-specific gRPC services.
///
/// <para>
/// <b>Two proto modes supported:</b>
/// </para>
///
/// <para>
/// <b>Mode A — Shared proto (default + optional fields):</b><br/>
/// All sites use one .proto. Site differences via optional fields + C# inheritance.<br/>
/// Register normally: <c>app.MapGrpcService&lt;FcdGrpcService&gt;()</c>
/// </para>
///
/// <para>
/// <b>Mode B — Per-site proto (completely different structure):</b><br/>
/// Site has its own .proto with different RPCs/messages.<br/>
/// Mark implementation with <see cref="SiteGrpcServiceAttribute"/>.<br/>
/// Register via: <c>app.MapSiteGrpcServices(assemblies)</c>
/// </para>
///
/// <para>
/// <b>Combined usage in Program.cs:</b>
/// <code>
/// // Shared proto service (default for most sites)
/// app.MapGrpcService&lt;FcdGrpcService&gt;();
///
/// // Auto-discover and map per-site proto services
/// app.MapSiteGrpcServices(typeof(TciFcdGrpcService).Assembly);
/// </code>
/// Both modes share the same <see cref="SiteCodeGrpcInterceptor"/> — it sets
/// <see cref="ISiteCodeHolder.SiteCode"/> regardless of which service handles the request.
/// </para>
/// </summary>
public static class SiteGrpcEndpointExtensions
{
    // Cached reflection: MapGrpcService<T>() is generic, we need to call it per-type
    private static readonly MethodInfo MapGrpcServiceMethod =
        typeof(GrpcEndpointRouteBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(GrpcEndpointRouteBuilderExtensions.MapGrpcService)
                        && m.IsGenericMethod
                        && m.GetParameters().Length == 1);

    /// <summary>
    /// Scans assemblies for gRPC service implementations marked with
    /// <see cref="SiteGrpcServiceAttribute"/> and maps each as a gRPC endpoint.
    ///
    /// <para>
    /// Each discovered service is registered via <c>MapGrpcService&lt;T&gt;()</c>.
    /// The <see cref="SiteCodeGrpcInterceptor"/> (registered via <see cref="SiteGrpcExtensions.AddSiteGrpcServices"/>)
    /// handles SiteCode extraction for ALL mapped services — both shared and per-site.
    /// </para>
    /// </summary>
    /// <param name="app">The endpoint route builder (typically WebApplication).</param>
    /// <param name="assemblies">Assemblies to scan for <see cref="SiteGrpcServiceAttribute"/>.</param>
    /// <returns>List of (SiteId, ServiceType) pairs that were mapped.</returns>
    public static IReadOnlyList<(string SiteId, Type ServiceType)> MapSiteGrpcServices(
        this IEndpointRouteBuilder app,
        params Assembly[] assemblies)
    {
        List<(string SiteId, Type ServiceType)> mapped = [];

        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<Type> siteServices = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                            && t.GetCustomAttribute<SiteGrpcServiceAttribute>() is not null);

            foreach (Type serviceType in siteServices)
            {
                SiteGrpcServiceAttribute attr = serviceType.GetCustomAttribute<SiteGrpcServiceAttribute>()!;

                // Call MapGrpcService<serviceType>(app) via reflection
                MethodInfo genericMethod = MapGrpcServiceMethod.MakeGenericMethod(serviceType);
                genericMethod.Invoke(null, [app]);

                mapped.Add((attr.SiteId, serviceType));
            }
        }

        return mapped;
    }

    /// <summary>
    /// Maps per-site gRPC services using an AOT-safe registry delegate instead of reflection.
    /// Use with the source-generated <c>SiteGrpcServiceRegistry.GetAllSiteGrpcServices()</c>:
    /// <code>
    /// app.MapSiteGrpcServices(SiteGrpcServiceRegistry.GetAllSiteGrpcServices);
    /// </code>
    /// </summary>
    /// <param name="app">The endpoint route builder (typically WebApplication).</param>
    /// <param name="registryProvider">
    /// Delegate returning the list of site gRPC service descriptors.
    /// Typically <c>SiteGrpcServiceRegistry.GetAllSiteGrpcServices</c> from source-generated code.
    /// </param>
    /// <returns>List of (SiteId, ServiceType) pairs that were mapped.</returns>
    public static IReadOnlyList<(string SiteId, Type ServiceType)> MapSiteGrpcServices(
        this IEndpointRouteBuilder app,
        Func<IReadOnlyList<SiteGrpcServiceDescriptor>> registryProvider)
    {
        MGuard.NotNull(registryProvider);

        IReadOnlyList<SiteGrpcServiceDescriptor> descriptors = registryProvider();
        List<(string SiteId, Type ServiceType)> mapped = new(descriptors.Count);

        foreach (SiteGrpcServiceDescriptor descriptor in descriptors)
        {
            MethodInfo genericMethod = MapGrpcServiceMethod.MakeGenericMethod(descriptor.ServiceType);
            genericMethod.Invoke(null, [app]);
            mapped.Add((descriptor.SiteId, descriptor.ServiceType));
        }

        return mapped;
    }

    /// <summary>
    /// AOT-safe overload accepting source-generated descriptors.
    /// The source generator emits <c>GeneratedSiteGrpcServiceDescriptor</c> inline
    /// to avoid external assembly reference issues in the generator context.
    /// This overload bridges the generated type to the runtime mapping logic.
    /// <example>
    /// <code>
    /// app.MapSiteGrpcServices(SiteGrpcServiceRegistry.GetAllSiteGrpcServices);
    /// </code>
    /// </example>
    /// </summary>
    public static IReadOnlyList<(string SiteId, Type ServiceType)> MapSiteGrpcServices<TDescriptor>(
        this IEndpointRouteBuilder app,
        Func<IReadOnlyList<TDescriptor>> registryProvider)
        where TDescriptor : class
    {
        MGuard.NotNull(registryProvider);

        IReadOnlyList<TDescriptor> descriptors = registryProvider();
        List<(string SiteId, Type ServiceType)> mapped = new(descriptors.Count);

        foreach (TDescriptor descriptor in descriptors)
        {
            // Use duck-typing via reflection to read SiteId and ServiceType properties
            // from the generated record type (which has identical shape to SiteGrpcServiceDescriptor)
            Type descType = descriptor.GetType();
            string siteId = (string)descType.GetProperty("SiteId")!.GetValue(descriptor)!;
            Type serviceType = (Type)descType.GetProperty("ServiceType")!.GetValue(descriptor)!;

            MethodInfo genericMethod = MapGrpcServiceMethod.MakeGenericMethod(serviceType);
            genericMethod.Invoke(null, [app]);
            mapped.Add((siteId, serviceType));
        }

        return mapped;
    }
}
