namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Resolves the correct gRPC client for the current site at runtime.
/// Consumers inject this interface instead of writing if/switch on SiteCode.
///
/// <example>
/// <code>
/// // Resolve client for current site:
/// var client = factory.CreateForCurrentSite&lt;FcdService.FcdServiceClient&gt;("fcd");
/// var response = await client.CreateAsync(request);
/// </code>
/// </example>
/// </summary>
public interface ISiteGrpcClientFactory
{
    /// <summary>
    /// Creates the gRPC client registered for the current site.
    /// Falls back to the "default" registration when no site-specific client is found.
    /// </summary>
    /// <typeparam name="TBase">The gRPC client base type (e.g., FcdService.FcdServiceClient).</typeparam>
    /// <param name="serviceName">The named service key used during AddSiteGrpcClient registration.</param>
    /// <returns>The site-specific (or default) gRPC client instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when neither a site-specific nor a default client is registered for <paramref name="serviceName"/>.
    /// </exception>
    TBase CreateForCurrentSite<TBase>(string serviceName) where TBase : ClientBase;
}
