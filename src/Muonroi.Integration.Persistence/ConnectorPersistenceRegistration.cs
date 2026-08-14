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
        // Use the (IServiceProvider, DbContextOptionsBuilder) overload so any IInterceptor
        // singletons registered in DI (e.g. TenantRlsConnectionInterceptor) are attached to
        // ConnectorDbContext connections — mirroring the proven RuleEngineDbContext pattern.
        // This ensures app.current_tenant_id is set via set_config() before each INSERT/SELECT,
        // satisfying the WITH CHECK RLS policy on connector_configs / connector_credentials.
        services.AddDbContext<ConnectorDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            IInterceptor[] interceptors = [.. sp.GetServices<IInterceptor>()];
            if (interceptors.Length > 0)
            {
                options.AddInterceptors(interceptors);
            }
        });

        services.AddScoped<IConnectorCredentialStore, EfConnectorCredentialStore>();
        services.AddScoped<IConnectorConfigStore, EfConnectorConfigStore>();

        return services;
    }
}
