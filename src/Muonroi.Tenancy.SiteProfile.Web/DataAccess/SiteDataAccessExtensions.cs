namespace Muonroi.Tenancy.SiteProfile.Web.DataAccess;

/// <summary>
/// Extension methods for registering unified data access infrastructure.
/// </summary>
public static class SiteDataAccessExtensions
{
    /// <summary>
    /// Registers the unified data access facade and query executor.
    ///
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="ISiteQueryExecutor"/> as scoped — wraps SiteSqlBuilder + IDapperRead
    ///     with auto <c>[[PropertyName]]</c> marker interpolation (DATA-02).</item>
    ///   <item><see cref="IMSiteDataAccess{TContext}"/> as scoped — unified facade for EF writes
    ///     + Dapper reads (DATA-01).</item>
    /// </list>
    ///
    /// Prerequisites: Call <c>AddSiteSqlBuilder()</c> and <c>AddSiteDapperInfrastructure()</c>
    /// before this method so that <c>SiteSqlBuilder</c> and <c>IDapperRead</c> are available.
    ///
    /// <example>
    /// <code>
    /// // Program.cs
    /// services.AddSiteSqlBuilder();
    /// services.AddSiteDapperInfrastructure(o => { ... });
    /// services.AddSiteDataAccess&lt;MySiteContext&gt;();
    ///
    /// // In a handler or service:
    /// public class MyHandler(IMSiteDataAccess&lt;MySiteContext&gt; data)
    /// {
    ///     public async Task Handle()
    ///     {
    ///         var rows = await data.QueryAsync&lt;OrderDto&gt;(
    ///             "SELECT [[BookingNo]] FROM ORDERS WHERE [[SiteCode]] = @site",
    ///             new { site = "TCI" });
    ///         await data.WriteAsync(new Order { ... });
    ///         await data.SaveChangesAsync();
    ///     }
    /// }
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="TContext">The per-site DbContext type. Must inherit from <see cref="MDbContext"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteDataAccess<TContext>(this IServiceCollection services)
        where TContext : MDbContext
    {
        // ISiteQueryExecutor — scoped, composes SiteSqlBuilder + IDapperRead
        services.TryAddScoped<ISiteQueryExecutor, SiteQueryExecutor>();

        // IMSiteDataAccess<TContext> — scoped, the unified facade
        services.AddScoped<IMSiteDataAccess<TContext>, MSiteDataAccess<TContext>>();

        return services;
    }

    /// <summary>
    /// Registers EF-to-Dapper column sync infrastructure for a specific site DbContext type.
    ///
    /// When <paramref name="configure"/> sets <c>SyncColumnMappingFromEfModel = true</c>:
    /// <list type="bullet">
    ///   <item>Registers <see cref="EfColumnSyncHostedService"/> as an <see cref="Microsoft.Extensions.Hosting.IHostedService"/>
    ///     — reads EF <c>IModel</c> metadata at startup (DATA-03, D-09).</item>
    ///   <item>Decorates <see cref="ISiteColumnMap"/> with <see cref="EfSyncedColumnMap"/> — manual
    ///     overrides always win, synced EF data fills gaps (DATA-05, D-13/D-14).</item>
    /// </list>
    ///
    /// Call AFTER <c>AddSiteSqlBuilder()</c> (which registers <c>ISiteColumnMap</c>).
    ///
    /// <example>
    /// <code>
    /// // Program.cs
    /// services.AddSiteSqlBuilder();
    /// services.AddEfColumnSync&lt;MySiteContext&gt;(o =>
    /// {
    ///     o.SyncColumnMappingFromEfModel = true;
    /// });
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="TContext">
    /// The site DbContext type whose EF <c>IModel</c> is used as the column mapping source.
    /// Must match a type discovered by the source-generated <c>SiteDbContextTypeRegistry</c>.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Delegate to configure <see cref="EfColumnSyncOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfColumnSync<TContext>(
        this IServiceCollection services,
        Action<EfColumnSyncOptions> configure)
        where TContext : MDbContext
    {
        var options = new EfColumnSyncOptions();
        configure(options);

        if (!options.SyncColumnMappingFromEfModel)
            return services; // No-op if not opted in (D-12)

        // Register the hosted service that reads EF IModel at startup (D-09)
        services.AddHostedService<EfColumnSyncHostedService>();

        // Decorate ISiteColumnMap: wrap the existing unkeyed registration with EfSyncedColumnMap.
        // Manual overrides in the inner map always take precedence (D-13/DATA-05).
        // Uses manual decoration (no Scrutor dependency required).
        Type dbContextType = typeof(TContext);

        var existingDescriptor = services.LastOrDefault(d =>
            d.ServiceType == typeof(ISiteColumnMap) && d.ServiceKey is null);

        if (existingDescriptor is not null)
        {
            services.Remove(existingDescriptor);
            services.AddScoped<ISiteColumnMap>(sp =>
            {
                // Resolve the inner ISiteColumnMap from the original descriptor
                ISiteColumnMap inner = ResolveFromDescriptor<ISiteColumnMap>(existingDescriptor, sp);

                // Get synced column entries for this DbContext (populated at startup by EfColumnSyncHostedService)
                var syncedEntries = EfColumnSyncHostedService.GetSyncedEntries(dbContextType);
                return new EfSyncedColumnMap(inner, syncedEntries);
            });
        }

        return services;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves a service instance from a <see cref="ServiceDescriptor"/> without re-registering it.
    /// Supports factory, type, and instance descriptors.
    /// </summary>
    private static TService ResolveFromDescriptor<TService>(
        ServiceDescriptor descriptor,
        IServiceProvider sp)
        where TService : class
    {
        if (descriptor.ImplementationFactory is not null)
            return (TService)descriptor.ImplementationFactory(sp);

        if (descriptor.ImplementationType is not null)
            return (TService)ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);

        if (descriptor.ImplementationInstance is not null)
            return (TService)descriptor.ImplementationInstance;

        MGuard.State(false, $"ServiceDescriptor for {typeof(TService).Name} has no factory, type, or instance.");
        return default!;
    }
}
