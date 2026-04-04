using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.Core.Shared;

namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Service collection extensions for tenant quota management.
/// </summary>
public static class TenantQuotaEfServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF-based tenant quota store and tracker.
    /// </summary>
    /// <typeparam name="TContext">The EF Core context type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddTenantQuotaManagement<TContext>(this IServiceCollection services)
        where TContext : MDbContext
    {
        services.TryAddScoped<ITenantQuotaStore, EfTenantQuotaStore<TContext>>();
        services.TryAddScoped<ITenantQuotaTracker, TenantQuotaTracker>();
        return services;
    }
}
