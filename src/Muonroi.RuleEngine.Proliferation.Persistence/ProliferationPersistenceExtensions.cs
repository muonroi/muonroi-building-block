using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.RuleEngine.Proliferation;

namespace Muonroi.RuleEngine.Proliferation.Persistence;

public static class ProliferationPersistenceExtensions
{
    /// <summary>
    /// Replace the in-memory store with Postgres persistence.
    /// Call after AddMProliferationEngine().
    /// </summary>
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
