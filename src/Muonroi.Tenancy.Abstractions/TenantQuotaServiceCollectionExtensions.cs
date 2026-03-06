using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.Tenancy.Abstractions;

public static class TenantQuotaServiceCollectionExtensions
{
    public static IServiceCollection AddTenantQuotaManagement(this IServiceCollection services)
    {
        services.TryAddSingleton<ITenantQuotaStore, InMemoryTenantQuotaStore>();
        services.TryAddScoped<ITenantQuotaTracker, InMemoryTenantQuotaTracker>();
        return services;
    }
}
