using Muonroi.Core.Abstractions.Models.Common;

namespace Muonroi.Core.Pagination;

public static class MPaginationExtensions
{
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
