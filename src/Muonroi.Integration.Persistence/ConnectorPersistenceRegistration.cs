using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Integration.Abstractions;

namespace Muonroi.Integration.Persistence;

/// <summary>
/// DI registration for connector persistence services.
/// </summary>
public static class ConnectorPersistenceRegistration
{
    /// <summary>Registers EF-based connector persistence services.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    public static IServiceCollection AddMConnectorPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<ConnectorDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IConnectorCredentialStore, EfConnectorCredentialStore>();
        services.AddScoped<IConnectorConfigStore, EfConnectorConfigStore>();

        return services;
    }
}
