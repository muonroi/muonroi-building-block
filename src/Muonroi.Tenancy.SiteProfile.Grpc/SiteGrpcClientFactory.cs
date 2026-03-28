using System.Reflection;
using Grpc.Core;
using Grpc.Net.ClientFactory;

namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Resolves the correct gRPC client for the current site using <see cref="ISiteProfileResolver"/>
/// and the <see cref="SiteGrpcClientRegistry"/> populated at startup.
///
/// Registered automatically by <see cref="SiteGrpcExtensions.AddSiteGrpcClientFactory"/>.
/// Lifetime: Scoped — resolved once per HTTP request.
/// </summary>
internal sealed class SiteGrpcClientFactory(
    ISiteProfileResolver resolver,
    SiteGrpcClientRegistry registry,
    GrpcClientFactory grpcClientFactory) : ISiteGrpcClientFactory
{
    /// <inheritdoc />
    public TBase CreateForCurrentSite<TBase>(string serviceName)
        where TBase : ClientBase
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        string siteId = resolver.Current.SiteId;

        // 1. Try site-specific descriptor
        SiteGrpcClientDescriptor? descriptor =
            registry.Descriptors.FirstOrDefault(d =>
                d.SiteId.Equals(siteId, StringComparison.OrdinalIgnoreCase)
                && d.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        // 2. Fall back to "default"
        descriptor ??= registry.Descriptors.FirstOrDefault(d =>
            d.SiteId.Equals("default", StringComparison.OrdinalIgnoreCase)
            && d.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        if (descriptor is null)
        {
            throw new InvalidOperationException(
                $"No gRPC client registered for site '{siteId}' (or 'default') with service name '{serviceName}'. " +
                $"Ensure Program.cs calls: services.AddSiteGrpcClient<TClient>(\"{siteId}\", \"{serviceName}\") " +
                $"and services.AddGrpcClient<TClient>() is also registered.");
        }

        // GrpcClientFactory.CreateClient<TClient>(string) is generic — invoke via reflection
        // using descriptor.ClientType so no static type parameter is needed at call site.
        // The name must match what was passed to services.AddGrpcClient<TClient>(name, ...).
        // When AddGrpcClient<TClient>() is called without an explicit name, the default is typeof(TClient).FullName.
        MethodInfo createClientMethod = typeof(GrpcClientFactory)
            .GetMethod(nameof(GrpcClientFactory.CreateClient))!
            .MakeGenericMethod(descriptor.ClientType);

        object client = createClientMethod.Invoke(grpcClientFactory, [descriptor.ClientType.FullName!])!;

        if (client is not TBase typedClient)
        {
            throw new InvalidCastException(
                $"gRPC client for site '{siteId}' service '{serviceName}' is of type '{client.GetType().Name}' " +
                $"which cannot be cast to '{typeof(TBase).Name}'. " +
                $"Ensure the registered TClient derives from the requested TBase type.");
        }

        return typedClient;
    }
}
