using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Helpers;
using Muonroi.UiEngine.Catalog.Options;
using Muonroi.UiEngine.Catalog.Persistence;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.UiEngine.Catalog.Extensions;

public static class UiEngineCatalogExtensions
{
    public static IServiceCollection AddUiEngineCatalog(
        this IServiceCollection services,
        Action<UiEngineCatalogOptions>? configure = null)
    {
        UiEngineCatalogOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.AddMemoryCache();
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        services.TryAddSingleton<IMDateTimeService, MDateTimeService>();
        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.TryAddScoped<ICatalogScanService, CatalogScanService>();

        if (!string.IsNullOrWhiteSpace(options.PostgresConnectionString))
        {
            services.AddDbContext<UiEngineCatalogDbContext>((_, db) =>
            {
                db.UseNpgsql(options.PostgresConnectionString);
            });
            services.AddScoped<ICatalogSnapshotStore, EfCoreCatalogSnapshotStore>();
            services.AddHostedService<UiEngineCatalogDatabaseMigrator>();
        }
        else if (!string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
        {
            services.AddDbContext<UiEngineCatalogDbContext>((_, db) =>
            {
                db.UseSqlServer(options.SqlServerConnectionString);
            });
            services.AddScoped<ICatalogSnapshotStore, EfCoreCatalogSnapshotStore>();
            services.AddHostedService<UiEngineCatalogDatabaseMigrator>();
        }
        else
        {
            services.TryAddSingleton<ICatalogSnapshotStore, InMemoryCatalogSnapshotStore>();
        }

        return services;
    }
}
