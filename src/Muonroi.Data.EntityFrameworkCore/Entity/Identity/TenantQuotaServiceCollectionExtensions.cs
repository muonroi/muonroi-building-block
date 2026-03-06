using Muonroi.Tenancy.Abstractions.Interfaces;
using Muonroi.Tenancy.Core.Shared;

namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

public static class TenantQuotaEfServiceCollectionExtensions
{
    public static IServiceCollection AddTenantQuotaManagement<TContext>(this IServiceCollection services)
        where TContext : MDbContext
    {
        services.TryAddScoped<ITenantQuotaStore, EfTenantQuotaStore<TContext>>();
        services.TryAddScoped<ITenantQuotaTracker, TenantQuotaTracker>();
        return services;
    }
}
