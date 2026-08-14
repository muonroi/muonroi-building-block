namespace Muonroi.Rules;

/// <summary>
/// Extension methods for registering FEEL web services.
/// </summary>
[Obsolete("Deprecated: Use Muonroi.RuleEngine.Runtime instead. This package will be removed in a future version.")]
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
