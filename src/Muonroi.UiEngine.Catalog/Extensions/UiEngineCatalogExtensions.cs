using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.UiEngine.Catalog.Extensions;

public static class UiEngineCatalogExtensions
{
    public static IServiceCollection AddUiEngineCatalog(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.TryAddScoped<ICatalogScanService, CatalogScanService>();
        return services;
    }
}
