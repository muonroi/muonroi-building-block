using Muonroi.Core.Abstractions.Models.Common;

namespace Muonroi.Core.Pagination;

/// <summary>
/// Provides extension methods for pagination configuration.
/// </summary>
public static class MPaginationExtensions
{
    /// <summary>
    /// Adds pagination configuration to the service collection.
    /// </summary>
    /// <typeparam name="TPaging">The type of the pagination configuration.</typeparam>
    /// <param name="services">The service collection to add the configuration to.</param>
    /// <param name="configuration">The configuration to bind the settings from.</param>
    /// <param name="paginationConfigs">The pagination configuration object.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddPaginationConfigs<TPaging>(
        this IServiceCollection services,
        IConfiguration configuration,
        TPaging paginationConfigs) where TPaging : MPaginationConfig
    {
        configuration.GetSection(paginationConfigs.SectionName).Bind(paginationConfigs);

        paginationConfigs.DefaultPageIndex = Math.Max(paginationConfigs.DefaultPageIndex, 1);

        paginationConfigs.DefaultPageSize = Math.Max(paginationConfigs.DefaultPageSize, 15);

        paginationConfigs.MaxPageSize = Math.Max(paginationConfigs.MaxPageSize, paginationConfigs.DefaultPageSize);

        services.AddSingleton(paginationConfigs);
        return services;
    }
}
