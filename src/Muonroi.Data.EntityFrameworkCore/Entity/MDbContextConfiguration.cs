using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Extensions;
using Muonroi.Governance.Abstractions.License;

namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Registers EF Core configuration for Muonroi DbContexts.
/// </summary>
public static class MDbContextConfiguration
{
    /// <summary>
    /// Adds and configures the DbContext, permission sync, and auth services.
    /// </summary>
    /// <typeparam name="TDbContext">The EF Core context type.</typeparam>
    /// <typeparam name="TPermission">The permission enum type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="isSecretDefault">Whether to use default secret behavior.</param>
    /// <param name="secretKey">Optional secret key override.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddDbContextConfigure<TDbContext, TPermission>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isSecretDefault = true,
        string secretKey = "")
        where TDbContext : MDbContext
        where TPermission : Enum
    {
        DatabaseConfigs? databaseConfigs = configuration.GetSection(nameof(DatabaseConfigs)).Get<DatabaseConfigs>();
        if (databaseConfigs == null || string.IsNullOrEmpty(databaseConfigs.DbType))
            throw new MConfigurationException("Database configuration is not properly set.", "DatabaseConfigs");

        // Register DatabaseConfigs for use in MigrationManager and other services
        services.TryAddSingleton(databaseConfigs);

        if (databaseConfigs.DbType == nameof(DbTypes.MongoDb))
        {
            MongoDbContextConfigurator<TDbContext> mongoConfigurator = new();
            _ = mongoConfigurator.ConfigureMongoDb(services, configuration);
        }
        else
        {
            TenantConnectionStringsOptions options = new();
            IConfigurationSection tenantSection = configuration.GetSection(TenantConnectionStringsOptions.SectionName);
            tenantSection.Bind(options);
            
            // Register default ITenantContext if not already registered
            services.TryAddScoped<ITenantContext, TenantContext>();
            services.Configure<MultiTenantOptions>(configuration.GetSection(MultiTenantOptions.SectionName));
            services.TryAddSingleton<TenantSchemaSelector>();
            services.TryAddScoped<ISaveChangesInterceptor>(sp =>
            {
                ILicenseGuard guard = sp.GetRequiredService<ILicenseGuard>();
                LicenseConfigs configs = sp.GetRequiredService<LicenseConfigs>();
                return new LicenseSaveChangesInterceptor(guard, configs);
            });

            ConfigureDbContext<TDbContext, TPermission>(services, databaseConfigs.DbType, isSecretDefault, secretKey);
        }

        _ = services.AddScoped<IPermissionSyncService, PermissionSyncService<TDbContext>>();
        _ = services.AddPermissionProviders(typeof(TDbContext).Assembly);

        services.SystemDependencyInjectionService<TDbContext, TPermission>();

        return services;
    }

    private static void SystemDependencyInjectionService<TDbContext, TPermission>(this IServiceCollection services)
        where TDbContext : MDbContext
        where TPermission : Enum
    {
        _ = services.AddScoped<IAuthenticateRepository, AuthenticateRepository<TDbContext, TPermission>>();
        _ = services.AddScoped<IRefreshTokenValidator, DefaultRefreshTokenValidator<TDbContext, TPermission>>();
    }


    private static string DecryptConnectionString(DatabaseConfigs databaseConfigs, IConfiguration configuration,
        bool isSecretDefault, string secretKey, string fingerprint = "")
    {
        bool enableEncryption = configuration.GetValue("EnableEncryption", false);

        // Encryption is OPT-IN, not mandatory
        if (!enableEncryption)
        {
            return databaseConfigs.DbType switch
            {
                nameof(DbTypes.SqlServer) => databaseConfigs.ConnectionStrings?.SqlServerConnectionString,
                nameof(DbTypes.MySql) => databaseConfigs.ConnectionStrings?.MySqlConnectionString,
                nameof(DbTypes.PostgreSql) => databaseConfigs.ConnectionStrings?.PostgreSqlConnectionString,
                nameof(DbTypes.Sqlite) => databaseConfigs.ConnectionStrings?.SqliteConnectionString,
                _ => throw new MConfigurationException("Unsupported database type: " + databaseConfigs.DbType, "DatabaseConfigs:DbType")
            } ?? throw new MConfigurationException("Connection string is empty.", "DatabaseConfigs:ConnectionStrings");
        }

        // Encryption enabled - decrypt the connection string
        return databaseConfigs.DbType switch
        {
            nameof(DbTypes.SqlServer) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.SqlServerConnectionString, isSecretDefault, secretKey, fingerprint),
            nameof(DbTypes.MySql) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.MySqlConnectionString, isSecretDefault, secretKey, fingerprint),
            nameof(DbTypes.PostgreSql) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.PostgreSqlConnectionString, isSecretDefault, secretKey, fingerprint),
            nameof(DbTypes.Sqlite) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.SqliteConnectionString, isSecretDefault, secretKey, fingerprint),
            _ => throw new MConfigurationException("Unsupported database type: " + databaseConfigs.DbType, "DatabaseConfigs:DbType")
        } ?? throw new MConfigurationException("Connection string is not provided or is empty.", "DatabaseConfigs:ConnectionStrings");
    }

    private static void ConfigureDbContext<T, TPermission>(IServiceCollection services, string dbType, bool isSecretDefault, string secretKey)
        where T : MDbContext
        where TPermission : Enum
    {
        _ = dbType switch
        {
            nameof(DbTypes.SqlServer) => services
                .AddScoped<IDbContextConfigurator<T>, SqlServerDbContextConfigurator<T>>(),
            nameof(DbTypes.MySql) => services.AddScoped<IDbContextConfigurator<T>, MySqlDbContextConfigurator<T>>(),
            nameof(DbTypes.PostgreSql) => services
                .AddScoped<IDbContextConfigurator<T>, PostgreSqlDbContextConfigurator<T>>(),
            nameof(DbTypes.Sqlite) => services.AddScoped<IDbContextConfigurator<T>, SqliteDbContextConfigurator<T>>(),
            _ => throw new MConfigurationException("Unsupported database type: " + dbType, "DatabaseConfigs:DbType")
        };

        _ = services.AddDbContext<T>((serviceProvider, options) =>
        {
            IDbContextConfigurator<T> configurator = serviceProvider.GetRequiredService<IDbContextConfigurator<T>>();
            ITenantContext tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
            ILicenseGuard guard = serviceProvider.GetRequiredService<ILicenseGuard>();
            IConfiguration config = serviceProvider.GetRequiredService<IConfiguration>();

            DatabaseConfigs dbConfigs = config.GetSection(nameof(DatabaseConfigs)).Get<DatabaseConfigs>()
                            ?? throw new MConfigurationException("DatabaseConfigs not found.", "DatabaseConfigs");

            bool enableEncryption = config.GetValue("EnableEncryption", false);
            string connectionString;

            if (enableEncryption)
            {
                // ENTANGLEMENT: Perform the decryption inside the guard's secure scope using LIVE key
                connectionString = guard.DecryptSecurely("db_connection", "dummy", (key, _) =>
                    DecryptConnectionString(dbConfigs, config, isSecretDefault, secretKey, key));
            }
            else
            {
                // No encryption - use plain connection string
                connectionString = DecryptConnectionString(dbConfigs, config, isSecretDefault, secretKey, "");
            }

            TenantSchemaSelector? schemaSelector = serviceProvider.GetService<TenantSchemaSelector>();
            connectionString = schemaSelector?.ApplyToConnectionString(connectionString, tenantContext.TenantId) ?? connectionString;
            configurator.Configure((DbContextOptionsBuilder<T>)options, connectionString);
            foreach (ISaveChangesInterceptor interceptor in serviceProvider.GetServices<ISaveChangesInterceptor>())
                options.AddInterceptors(interceptor);
        });
        _ = services.AddScoped<MDbContext>(sp => sp.GetRequiredService<T>());
    }
}
