using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Grpc.Core;

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
    GrpcClientFactoryAccessor grpcClientFactoryAccessor,
    IServiceProvider serviceProvider) : ISiteGrpcClientFactory
{
    /// <inheritdoc />
    public TBase CreateForCurrentSite<TBase>(string serviceName)
        where TBase : ClientBase
    {
        MGuard.NotEmpty(serviceName);

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
            throw new MInternalException(
                $"No gRPC client registered for site '{siteId}' (or 'default') with service name '{serviceName}'. " +
                $"Ensure Program.cs calls: services.AddSiteGrpcClient<TClient>(\"{siteId}\", \"{serviceName}\") " +
                $"and services.AddGrpcClient<TClient>() is also registered.");
        }

        // Create client with correct Type using cached CallInvoker
        object? client = grpcClientFactoryAccessor.CreateClient(descriptor.ClientType, serviceName);
        if (client is null)
        {
            throw new MInternalException(
                $"No gRPC client cached for site '{siteId}' service '{serviceName}' " +
                $"(type: {descriptor.ClientType.Name}). " +
                $"Ensure app.InitializeSiteGrpcClients() is called in Program.cs.");
        }

        if (client is not TBase typedClient)
        {
            throw new InvalidCastException(
                $"gRPC client for site '{siteId}' service '{serviceName}' is of type '{client.GetType().Name}' " +
                $"which cannot be cast to '{typeof(TBase).Name}'. " +
                $"Ensure the registered TClient derives from the requested TBase type.");
        }

        return typedClient;
    }

    /// <inheritdoc />
    public TFacade CreateFacadeForCurrentSite<TFacade>(string serviceName)
        where TFacade : class
    {
        MGuard.NotEmpty(serviceName);

        string siteId = resolver.Current.SiteId;

        // Key pattern: facade:{serviceName}:{siteId}
        // 1. Try site-specific facade
        string siteKey = $"facade:{serviceName}:{siteId}";
        TFacade? facade = serviceProvider.GetKeyedService<TFacade>(siteKey);

        // 2. Fall back to "default"
        if (facade is null)
        {
            string defaultKey = $"facade:{serviceName}:default";
            facade = serviceProvider.GetKeyedService<TFacade>(defaultKey);
        }

        if (facade is null)
        {
            throw new MInternalException(
                $"No gRPC facade registered for site '{siteId}' (or 'default') with service name '{serviceName}'. " +
                $"Ensure Program.cs calls: services.AddSiteGrpcFacadeClient<TFacade, TImpl>(\"{siteId}\", \"{serviceName}\") " +
                $"and services.AddSiteGrpcFacadeClient<TFacade, TImpl>(\"default\", \"{serviceName}\") as fallback.");
        }

        return facade;
    }
}
