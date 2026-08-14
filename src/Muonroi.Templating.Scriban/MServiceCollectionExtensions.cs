namespace Muonroi.Templating.Scriban;

/// <summary>
/// Service collection extensions for Scriban templating.
/// </summary>
public static class MServiceCollectionExtensions
{
    /// <summary>
    /// Adds Scriban templating services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddScribanTemplating(this IServiceCollection services)
    {
        services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
        return services;
    }
}
