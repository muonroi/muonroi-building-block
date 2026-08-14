namespace Muonroi.RuleEngine.Proliferation.Persistence;

/// <summary>
/// Dependency injection helpers for persistence-backed proliferation storage.
/// </summary>
public static class ProliferationPersistenceExtensions
{
    /// <summary>
    /// Replaces the in-memory store with Postgres persistence.
    /// </summary>
    /// <param name="services">Service collection to extend.</param>
    /// <param name="connectionString">Postgres connection string.</param>
    public static IServiceCollection AddMProliferationPostgres(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ProliferationDbContext>(opt =>
            opt.UseNpgsql(connectionString));

        // Replace in-memory store with Postgres store
        services.RemoveAll<IProliferationStore>();
        services.AddScoped<IProliferationStore, PostgresProliferationStore>();

        return services;
    }
}
