using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Rules.Contributors;

namespace Muonroi.Rules;

/// <summary>
/// Extension methods for registering FEEL web services.
/// </summary>
public static class FeelWebExtensions
{
    /// <summary>
    /// Adds FEEL web services to the service collection, including the manifest contributor and controllers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddFeelWeb(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, FeelPlaygroundManifestContributor>());
        services.AddControllers().AddApplicationPart(typeof(Controllers.FeelController).Assembly);
        return services;
    }
}
