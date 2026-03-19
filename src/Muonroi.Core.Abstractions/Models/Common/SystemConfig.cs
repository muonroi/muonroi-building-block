namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary>
/// Configuration extensions for system settings.
/// </summary>
public static class SystemConfig
{
    /// <summary>
    /// Adds system configurations from the specified configuration section to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add configurations to.</param>
    /// <param name="configuration">The configuration to bind from.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddSystemConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ResourceSetting appSettings = [];
        configuration.GetSection(nameof(ResourceSetting)).Bind(appSettings);
        _ = services.AddSingleton(appSettings);
        return services;
    }
}
