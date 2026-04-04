using Muonroi.Core.Abstractions.Exceptions;
using Dapper.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Web.Dapper;

namespace Muonroi.Tenancy.SiteProfile.Web;

/// <summary>
/// Marker interface for read-only (read-replica) Dapper operations.
/// Extends <see cref="IDapper"/> — concrete implementations add this to signal read-only intent.
///
/// Usage in site profile registration:
/// <code>
/// services.AddKeyedScoped&lt;IDapperRead, MySiteDapperRead&gt;("SITE_CODE");
/// </code>
/// </summary>
public interface IDapperRead : IDapper
{
}

/// <summary>
/// Extension methods for registering per-site Dapper infrastructure.
/// Mirrors <see cref="SiteProfileDbContextExtensions.AddSiteDbInfrastructure"/> for Dapper consumers.
/// </summary>
public static class SiteProfileDapperExtensions
{
    /// <summary>
    /// One-call setup for per-site Dapper data access infrastructure.
    ///
    /// Registers:
    /// <list type="bullet">
    ///   <item>A scoped <see cref="IConnectionStringProvider"/> — resolves write and read connection strings
    ///     per-request from the current site context, used by <c>BaseDapper&lt;T&gt;</c> implementations.</item>
    ///   <item>A scoped <see cref="IDapper"/> — resolved per-request via <see cref="ISiteProfileResolver"/>
    ///     from keyed registrations. Each site profile must register:
    ///     <c>services.AddKeyedScoped&lt;IDapper, TImpl&gt;("SITE_CODE")</c></item>
    ///   <item>A scoped <see cref="IDapperRead"/> — resolved per-request via <see cref="ISiteProfileResolver"/>
    ///     from keyed registrations. Falls back to the write <see cref="IDapper"/> if it implements
    ///     <see cref="IDapperRead"/> (common pattern: read impl also registers the write key).
    ///     Each site profile may register:
    ///     <c>services.AddKeyedScoped&lt;IDapperRead, TReadImpl&gt;("SITE_CODE")</c></item>
    /// </list>
    ///
    /// Call this ONCE in Program.cs after <c>AddMultiSiteProfiles()</c>.
    ///
    /// <example>
    /// <code>
    /// // Program.cs
    /// services.AddMultiSiteProfiles(configuration, sp => workContextAccessor.WorkContext?.SiteCode, assemblies);
    /// services.AddSiteDapperInfrastructure(o =>
    /// {
    ///     o.WriteConnectionString = sp => workContextAccessor.WorkContext?.ConnectionString!;
    ///     o.ReadConnectionString = sp => workContextAccessor.WorkContext?.ReadOnlyConnectionString;
    ///     o.ConnectionStringTransform = cs => Cryptography.Decrypt(secretKey, cs);
    /// });
    ///
    /// // In each ISiteProfile.RegisterServices():
    /// services.AddKeyedScoped&lt;IDapper, MySiteDapper&gt;("SITE_CODE");
    /// services.AddKeyedScoped&lt;IDapperRead, MySiteDapperRead&gt;("SITE_CODE");
    ///
    /// // Repository constructor — site-aware, no manual SiteId needed:
    /// public MyRepo(IDapper dapper, IDapperRead dapperRead) { ... }
    /// </code>
    /// </example>
    /// <seealso cref="AddSiteSqlBuilder"/>
    /// </summary>
    public static IServiceCollection AddSiteDapperInfrastructure(
        this IServiceCollection services,
        Action<SiteDapperInfrastructureOptions> configure)
    {
        var options = new SiteDapperInfrastructureOptions();
        configure(options);

        if (options.WriteConnectionString is null)
            throw new MArgumentException(nameof(configure), "SiteDapperInfrastructureOptions.WriteConnectionString resolver is required.");

        // Store options as singleton for downstream use by IConnectionStringProvider
        services.AddSingleton(options);

        // Register IConnectionStringProvider as scoped — resolves per-site connection strings.
        // BaseDapper<T> implementations call sp.GetRequiredService<IConnectionStringProvider>()
        // to build the IDbConnection. This overrides the singleton DefaultConnectionStringProvider
        // registered by AddDapper<T>() — call AddSiteDapperInfrastructure() AFTER AddDapper<T>().
        services.AddScoped<IConnectionStringProvider>(sp =>
        {
            var opts = sp.GetRequiredService<SiteDapperInfrastructureOptions>();
            return new SiteDapperConnectionStringProvider(opts, sp);
        });

        // Register IDapper (write) as scoped — resolved by site key from ISiteProfileResolver.
        // Each ISiteProfile.RegisterServices() must register:
        //   services.AddKeyedScoped<IDapper, TImpl>("SITE_CODE")
        services.AddScoped(sp =>
        {
            var resolver = sp.GetRequiredService<ISiteProfileResolver>();
            string siteId = resolver.Current.SiteId;

            return sp.GetKeyedService<IDapper>(siteId)
                ?? sp.GetKeyedService<IDapper>("default")
                ?? throw new MInternalException(
                    $"No keyed IDapper registered for site '{siteId}' or 'default'. " +
                    $"Ensure ISiteProfile.RegisterServices() calls: " +
                    $"services.AddKeyedScoped<IDapper, TImpl>(\"{siteId}\")");
        });

        // Register IDapperRead (read replica) as scoped — resolved by site key from ISiteProfileResolver.
        // Falls back to the write IDapper if it also implements IDapperRead,
        // or throws if neither keyed IDapperRead nor a compatible IDapper is available.
        services.AddScoped(sp =>
        {
            var resolver = sp.GetRequiredService<ISiteProfileResolver>();
            string siteId = resolver.Current.SiteId;

            // Try keyed IDapperRead first (dedicated read replica)
            var dapperRead = sp.GetKeyedService<IDapperRead>(siteId)
                ?? sp.GetKeyedService<IDapperRead>("default");

            if (dapperRead is not null) return dapperRead;

            // Fall back: check if the write IDapper also implements IDapperRead
            var writeDapper = sp.GetKeyedService<IDapper>(siteId)
                ?? sp.GetKeyedService<IDapper>("default");

            if (writeDapper is IDapperRead readCapable) return readCapable;

            throw new MInternalException(
                $"No keyed IDapperRead registered for site '{siteId}' or 'default'. " +
                $"Either register a dedicated read replica: " +
                $"services.AddKeyedScoped<IDapperRead, TReadImpl>(\"{siteId}\"), " +
                $"or make your IDapper implementation also implement IDapperRead.");
        });

        // Store optional ConnectionStringTransform as keyed singleton for downstream use
        if (options.ConnectionStringTransform is not null)
        {
            services.AddKeyedSingleton(
                "SiteDapper:ConnectionStringTransform",
                options.ConnectionStringTransform);
        }

        return services;
    }

    /// <summary>
    /// Registers SiteSqlBuilder infrastructure:
    /// <list type="bullet">
    ///   <item><see cref="DefaultSiteColumnMap"/> as keyed singleton with key <c>"default"</c> —
    ///     fallback for sites without custom column maps (PascalCase to UPPER_SNAKE_CASE).</item>
    ///   <item><see cref="ISiteColumnMap"/> via <see cref="SiteProfileExtensions.AddSiteResolvedService{TService}"/> —
    ///     resolves the keyed ISiteColumnMap by current site ID, falls back to <c>"default"</c>.</item>
    ///   <item><see cref="SiteSqlBuilder"/> as scoped — receives the site-resolved ISiteColumnMap.</item>
    /// </list>
    ///
    /// Sites override column mappings by registering their custom map in
    /// <see cref="ISiteProfile.RegisterServices"/>:
    /// <code>
    /// services.AddKeyedSingleton&lt;ISiteColumnMap, MySiteColumnMap&gt;("SITE_CODE");
    /// </code>
    ///
    /// Call once in Program.cs after <c>AddMultiSiteProfiles()</c>.
    /// This is an opt-in call, separate from <see cref="AddSiteDapperInfrastructure"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteSqlBuilder(this IServiceCollection services)
    {
        // Default column map — PascalCase to UPPER_SNAKE_CASE (fallback for all sites)
        services.AddKeyedSingleton<ISiteColumnMap, DefaultSiteColumnMap>("default");

        // Per-site resolution — tries exact site key, falls back to "default"
        services.AddSiteResolvedService<ISiteColumnMap>();

        // SiteSqlBuilder scoped — receives site-resolved ISiteColumnMap
        services.AddScoped<SiteSqlBuilder>();

        return services;
    }

    // -----------------------------------------------------------------------
    // Internal adapter types
    // -----------------------------------------------------------------------

    /// <summary>
    /// Scoped IConnectionStringProvider that delegates to consumer-provided resolvers.
    /// Distinguishes write vs read requests via the connectionName and readOnly flag.
    /// </summary>
    private sealed class SiteDapperConnectionStringProvider(
        SiteDapperInfrastructureOptions options,
        IServiceProvider sp) : IConnectionStringProvider
    {
        public string GetConnectionString(string connectionName, bool enableMasterSlave = false, bool readOnly = false)
        {
            // Determine if this is a read-replica request
            bool isReadRequest = readOnly
                || (!string.IsNullOrEmpty(connectionName)
                    && connectionName.Contains("Read", StringComparison.OrdinalIgnoreCase));

            string? raw = null;

            if (isReadRequest && options.ReadConnectionString is not null)
            {
                raw = options.ReadConnectionString(sp);
            }

            // Fall back to write connection string if no read string configured or not read request
            raw ??= options.WriteConnectionString!(sp);

            return options.ConnectionStringTransform is not null
                ? options.ConnectionStringTransform(raw)
                : raw;
        }
    }
}
