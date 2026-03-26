namespace Muonroi.Quota.Abstractions;

/// <summary>
/// Service registration helpers for tenant quota management.
/// </summary>
public static class TenantQuotaServiceCollectionExtensions
{
    /// <summary>
    /// Registers tenant quota store and tracker services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddTenantQuotaManagement(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantQuotaStore, InMemoryTenantQuotaStore>();
        services.TryAddScoped<ITenantQuotaTracker, InMemoryTenantQuotaTracker>();
        return services;
    }
}
