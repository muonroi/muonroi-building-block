namespace Muonroi.UiEngine.Catalog.Extensions;

/// <summary>
/// Service registration helpers for the UI engine catalog.
/// </summary>
public static class UiEngineCatalogExtensions
{
    /// <summary>
    /// Registers catalog services and persistence providers.
    /// </summary>
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
