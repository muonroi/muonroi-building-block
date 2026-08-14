namespace Muonroi.RuleEngine.CEP;

/// <summary>
/// Service registration helpers for CEP web hosting.
/// </summary>
public static class CepWebExtensions
{
    /// <summary>
    /// Registers CEP services, repositories, and controllers for web applications.
    /// </summary>
    public static IServiceCollection AddCepWeb(
        this IServiceCollection services,
        Action<CepOptions>? configure = null)
    {
        CepOptions options = new();
        configure?.Invoke(options);

        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        services.TryAddSingleton<IMDateTimeService, MDateTimeService>();
        services.TryAddSingleton<IMJsonSerializeService, MJsonSerializeService>();
        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

        if (!string.IsNullOrWhiteSpace(options.PostgresConnectionString))
        {
            services.AddDbContext<CepConfigDbContext>((_, db) =>
            {
                db.UseNpgsql(options.PostgresConnectionString);
            });
            services.TryAddScoped<ICepConfigRepository, EfCoreCepConfigRepository>();
            services.AddHostedService<CepConfigDatabaseMigrator>();
        }
        else if (!string.IsNullOrWhiteSpace(options.SqlServerConnectionString))
        {
            services.AddDbContext<CepConfigDbContext>((_, db) =>
            {
                db.UseSqlServer(options.SqlServerConnectionString);
            });
            services.TryAddScoped<ICepConfigRepository, EfCoreCepConfigRepository>();
            services.AddHostedService<CepConfigDatabaseMigrator>();
        }
        else
        {
            services.TryAddSingleton<ICepConfigRepository, InMemoryCepConfigRepository>();
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUiEngineManifestContributor, Contributors.CepManifestContributor>());
        services.AddControllers().AddApplicationPart(typeof(Controllers.CepController).Assembly);
        return services;
    }
}
