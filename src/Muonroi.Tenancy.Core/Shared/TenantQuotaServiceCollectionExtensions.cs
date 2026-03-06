namespace Muonroi.Tenancy.Core.Shared;

public static class TenantQuotaServiceCollectionExtensions
{
    public static IServiceCollection AddTenantQuotaManagement(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantQuotaStore, InMemoryTenantQuotaStore>();
        services.TryAddScoped<ITenantQuotaTracker, TenantQuotaTracker>();
        return services;
    }
}
