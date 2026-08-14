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
/// (generic), leaving <c>DbContextOptions</c> non-generic untouched. Multiple site DbContexts can
/// coexist safely without conflicting in the Autofac container.
/// </summary>
public static class SiteProfileDbContextExtensions
{
    // Key used to store/retrieve SiteDbInfrastructureOptions from DI
    internal const string InfrastructureOptionsKey = "SiteDbContext:InfrastructureOptions";

    /// <summary>
    /// One-call setup for the entire site DbContext infrastructure.
    /// Registers <see cref="ITenantContext"/> and <see cref="ITenantConnectionStringFactory"/>
    /// adapters from consumer-provided resolvers, plus an optional connection string transform.
    ///
    /// Call this ONCE in Program.cs BEFORE <c>AddSiteServices()</c> or any <c>AddSiteDbContext&lt;T&gt;()</c>.
    ///
    /// <example>
    /// <code>
    /// services.AddSiteDbInfrastructure(o =>
    /// {
    ///     o.TenantId = sp => accessor.MultiTenantContext?.TenantInfo?.TenantId;
    ///     o.ConnectionString = sp => accessor.MultiTenantContext?.TenantInfo?.ConnectionString!;
    ///     o.ConnectionStringTransform = cs => Cryptography.Decrypt(secretKey, cs);
    ///     o.ConfigureDbContext = (b, cs) => b.UseSqlServer(cs, o => o.UseCompatibilityLevel(120));
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static IServiceCollection AddSiteDbInfrastructure(
        this IServiceCollection services,
        Action<SiteDbInfrastructureOptions> configure)
    {
        var options = new SiteDbInfrastructureOptions();
        configure(options);

        if (options.TenantId is null)
            return MGuard.Fail<IServiceCollection>("SiteDbInfrastructureOptions.TenantId resolver is required.");

        if (options.ConnectionString is null)
            return MGuard.Fail<IServiceCollection>("SiteDbInfrastructureOptions.ConnectionString resolver is required.");

        // Store options for AddSiteDbContext to consume
        services.AddSingleton(options);

        // Register ITenantContext — adapts consumer's tenant resolution to ecosystem interface
        Func<IServiceProvider, string?> tenantIdResolver = options.TenantId;
        services.AddScoped<ITenantContext>(sp =>
            new DelegatingTenantContext(tenantIdResolver(sp)));

        // Register ITenantConnectionStringFactory — adapts consumer's connection string resolution
        Func<IServiceProvider, string> connectionStringResolver = options.ConnectionString;
        services.AddScoped<ITenantConnectionStringFactory>(sp =>
            new DelegatingTenantConnectionStringFactory(connectionStringResolver(sp)));

        // Register connection string transform as keyed singleton (consumed by AddSiteDbContext fallback)
        if (options.ConnectionStringTransform is not null)
        {
            services.AddKeyedSingleton(
                "SiteDbContext:ConnectionStringTransform",
                options.ConnectionStringTransform);
        }

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TContext"/> as a scoped DbContext using the ecosystem tenant
    /// connection string factory, without registering the non-generic <c>DbContextOptions</c> base
    /// that causes Autofac "last wins" conflicts.
    ///
    /// Requires <see cref="AddSiteDbInfrastructure"/> to be called first (registers ITenantContext
    /// and ITenantConnectionStringFactory). Or consumer can register those interfaces manually.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteDbContext<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        // Register ONLY the generic DbContextOptions<TContext> — NOT the non-generic DbContextOptions base.
        // This is the key difference from AddDbContext<T>() which registers both.
        services.AddScoped(sp =>
        {
            ITenantContext tenantContext = sp.GetRequiredService<ITenantContext>();
            ITenantConnectionStringFactory connFactory = sp.GetRequiredService<ITenantConnectionStringFactory>();
            string raw = connFactory.GetConnectionString(tenantContext.TenantId);

            // Resolve transform from keyed DI (registered by AddSiteDbInfrastructure)
            Func<string, string>? transform =
                sp.GetKeyedService<Func<string, string>>("SiteDbContext:ConnectionStringTransform");
            string cs = transform is not null ? transform(raw) : raw;

            var builder = new DbContextOptionsBuilder<TContext>();

            // Resolve db provider configuration from options (registered by AddSiteDbInfrastructure)
            SiteDbInfrastructureOptions? options = sp.GetService<SiteDbInfrastructureOptions>();
            if (options?.ConfigureDbContext is not null)
            {
                options.ConfigureDbContext(builder, cs);
            }
            else
            {
                // Default: SQL Server for backward compatibility
                builder.UseSqlServer(cs);
            }

            return builder.Options;
        });

        // Register TContext itself as scoped, resolving the generic options from the provider.
        // ActivatorUtilities.CreateInstance allows TContext to have additional constructor parameters
        // resolved from the service provider (e.g., ILogger, domain services).
        services.AddScoped(sp =>
        {
            DbContextOptions<TContext> options = sp.GetRequiredService<DbContextOptions<TContext>>();
            return ActivatorUtilities.CreateInstance<TContext>(sp, options);
        });

        return services;
    }

    /// <summary>
    /// Backward-compatible overload that accepts <c>IConfiguration</c> (for source-generated code).
    /// The <paramref name="configuration"/> parameter is accepted but not used — tenant resolution
    /// and connection strings come from <see cref="SiteDbInfrastructureOptions"/> registered via
    /// <see cref="AddSiteDbInfrastructure"/>.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static IServiceCollection AddSiteDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration)
        where TContext : DbContext
        => AddSiteDbContext<TContext>(services);

    /// <summary>
    /// Registers the site migration runner that auto-discovers all site DbContext types
    /// (via the source-generated <c>SiteDbContextTypeRegistry.GetAllSiteDbContextTypes()</c>)
    /// and runs EF Core migrations at startup. Call after <see cref="AddSiteDbInfrastructure"/>.
    ///
    /// <example>
    /// <code>
    /// services.AddSiteMigrationRunner(); // defaults: AutoMigrate + unbounded parallelism
    ///
    /// services.AddSiteMigrationRunner(o =>
    /// {
    ///     o.Strategy = MigrationStrategy.ValidateOnly; // prod: check only, don't apply
    ///     o.MaxParallelism = 4;                        // limit concurrent migrations
    /// });
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration delegate for <see cref="SiteMigrationRunnerOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteMigrationRunner(
        this IServiceCollection services,
        Action<SiteMigrationRunnerOptions>? configure = null)
    {
        var options = new SiteMigrationRunnerOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddHostedService<SiteMigrationRunner>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="ISiteConfiguration"/> as a scoped service that reads per-site
    /// configuration from the "Sites:{SiteId}:*" section of appsettings.json.
    ///
    /// The current SiteId is resolved automatically via <see cref="ISiteProfileResolver"/>.
    /// Hot-reload is transparent — values re-read from IConfiguration on each call.
    ///
    /// Call after <see cref="AddSiteDbInfrastructure"/> (which registers ISiteProfileResolver).
    /// </summary>
    public static IServiceCollection AddSiteConfiguration(this IServiceCollection services)
    {
        services.AddScoped<ISiteConfiguration, SiteConfiguration>();
        return services;
    }

    /// <summary>
    /// Registers a per-site MediatR command handler so the ecosystem keyed DI system can dispatch
    /// <typeparamref name="TCmd"/> to the correct site-specific handler at runtime.
    ///
    /// <para>
    /// Under the hood this delegates to <c>AddSiteResolvedService&lt;IRequestHandler&lt;TCmd, TResp&gt;&gt;()</c>,
    /// which registers a scoped factory that resolves the correct keyed handler by the current
    /// <c>SiteId</c> (with a <c>"default"</c> fallback if no site-specific key is found).
    /// </para>
    ///
    /// <para>
    /// Each site-specific handler must be registered separately inside its
    /// <c>ISiteProfile.RegisterServices()</c> implementation using a keyed registration:
    /// </para>
    ///
    /// <example>
    /// <code>
    /// // Program.cs — register the dispatcher factory once:
    /// services.AddSiteCommandHandler&lt;CreateOrderCommand, CreateOrderResponse&gt;();
    ///
    /// // SiteASiteProfile.RegisterServices() — register the SiteA implementation:
    /// services.AddKeyedScoped&lt;IRequestHandler&lt;CreateOrderCommand, CreateOrderResponse&gt;,
    ///     SiteACreateOrderHandler&gt;("SiteA");
    ///
    /// // SiteBSiteProfile.RegisterServices() — register the SiteB implementation:
    /// services.AddKeyedScoped&lt;IRequestHandler&lt;CreateOrderCommand, CreateOrderResponse&gt;,
    ///     SiteBCreateOrderHandler&gt;("SiteB");
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="TCmd">The command/request type implementing <see cref="IRequest{TResp}"/>.</typeparam>
    /// <typeparam name="TResp">The response type returned by the handler.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSiteCommandHandler<TCmd, TResp>(
        this IServiceCollection services)
        where TCmd : IRequest<TResp>
    {
        services.AddSiteResolvedService<IRequestHandler<TCmd, TResp>>();
        return services;
    }

    // -----------------------------------------------------------------------
    // Internal adapter types — minimal implementations of ecosystem interfaces
    // that delegate to consumer-provided resolvers.
    // -----------------------------------------------------------------------

    private sealed class DelegatingTenantContext(string? tenantId) : ITenantContext
    {
        public string? TenantId { get; set; } = tenantId;
    }

    private sealed class DelegatingTenantConnectionStringFactory(string connectionString) : ITenantConnectionStringFactory
    {
        public string GetConnectionString(string? tenantId) => connectionString;
    }
}
