using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Extension methods for registering per-site DbContexts without the Autofac non-generic conflict.
///
/// Problem: EF Core's <c>services.AddDbContext&lt;T&gt;()</c> registers both <c>DbContextOptions&lt;T&gt;</c>
/// (generic) AND <c>DbContextOptions</c> (non-generic base). When multiple site DbContexts call
/// <c>AddDbContext&lt;T&gt;()</c>, Autofac uses "last wins" for the non-generic registration — breaking
/// <c>EFCoreStoreDbContext&lt;TenantInfo&gt;</c> (the tenant store) which resolves the non-generic
/// <c>DbContextOptions</c> and ends up with the wrong factory.
///
/// Fix: <see cref="AddSiteDbContext{TContext}"/> registers ONLY <c>DbContextOptions&lt;T&gt;</c>
/// (generic), leaving <c>DbContextOptions</c> non-generic untouched. Multiple site DbContexts can
/// coexist safely without conflicting in the Autofac container.
/// </summary>
public static class SiteProfileDbContextExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> as a scoped DbContext using the ecosystem tenant
    /// connection string factory, without registering the non-generic <c>DbContextOptions</c> base
    /// that causes Autofac "last wins" conflicts.
    ///
    /// Use this instead of <c>services.AddDbContext&lt;T&gt;()</c> for per-site DbContexts to avoid
    /// the Autofac registration conflict with <c>EFCoreStoreDbContext&lt;TenantInfo&gt;</c>.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration (passed through for future extensibility).</param>
    /// <param name="connectionStringTransform">
    /// Optional transform applied to the raw connection string before use.
    /// Use this for consumers that encrypt connection strings — e.g.,
    /// <c>cs =&gt; Cryptography.Decrypt(secretKey, cs)</c>.
    /// When null, the raw connection string from <see cref="ITenantConnectionStringFactory"/> is used as-is.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<string, string>? connectionStringTransform = null)
        where TContext : DbContext
    {
        // Register ONLY the generic DbContextOptions<TContext> — NOT the non-generic DbContextOptions base.
        // This is the key difference from AddDbContext<T>() which registers both.
        services.AddScoped<DbContextOptions<TContext>>(sp =>
        {
            var tenantContext = sp.GetRequiredService<ITenantContext>();
            var connFactory = sp.GetRequiredService<ITenantConnectionStringFactory>();
            var raw = connFactory.GetConnectionString(tenantContext.TenantId);

            // Resolve transform: explicit param wins, then keyed DI fallback (for consumers that register
            // a named transform via services.AddKeyedSingleton("SiteDbContext:ConnectionStringTransform", ...))
            var transform = connectionStringTransform
                ?? sp.GetKeyedService<Func<string, string>>("SiteDbContext:ConnectionStringTransform");
            var cs = transform is not null ? transform(raw) : raw;

            var builder = new DbContextOptionsBuilder<TContext>();
            builder.UseSqlServer(cs);
            return builder.Options;
        });

        // Register TContext itself as scoped, resolving the generic options from the provider.
        // ActivatorUtilities.CreateInstance allows TContext to have additional constructor parameters
        // resolved from the service provider (e.g., ILogger, domain services).
        services.AddScoped<TContext>(sp =>
        {
            var options = sp.GetRequiredService<DbContextOptions<TContext>>();
            return ActivatorUtilities.CreateInstance<TContext>(sp, options);
        });

        return services;
    }
}
