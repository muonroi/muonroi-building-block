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
        DatabaseConfigs? rawConfigs = configuration.GetSection(nameof(DatabaseConfigs)).Get<DatabaseConfigs>();
        MGuard.Configured(rawConfigs != null && !string.IsNullOrEmpty(rawConfigs.DbType), "Database configuration is not properly set.", "DatabaseConfigs");
        DatabaseConfigs databaseConfigs = MGuard.NotNull(rawConfigs);

        // Register DatabaseConfigs for use in MigrationManager and other services
        services.TryAddSingleton(databaseConfigs);

        if (databaseConfigs.DbType == nameof(DbTypes.MongoDb))
        {
            Type? mongoConfiguratorType = Type.GetType("Muonroi.Data.EntityFrameworkCore.MongoDb.MongoDbContextConfigurator`1, Muonroi.Data.EntityFrameworkCore.MongoDb");
            MGuard.Configured(mongoConfiguratorType != null, "Database provider package for MongoDb is not installed. Please reference Muonroi.Data.EntityFrameworkCore.MongoDb package.", "DatabaseConfigs:DbType");

            var mongoConfigurator = Activator.CreateInstance(MGuard.NotNull(mongoConfiguratorType).MakeGenericType(typeof(TDbContext)));
            var method = MGuard.NotNull(mongoConfiguratorType).MakeGenericType(typeof(TDbContext)).GetMethod("ConfigureMongoDb");
            method?.Invoke(mongoConfigurator, new object[] { services, configuration });
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
                ILicenseGuard? guard = sp.GetService<ILicenseGuard>();
                LicenseConfigs? configs = sp.GetService<LicenseConfigs>();
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
        // Always register auth repositories unconditionally.
        // MAuthenticateTokenHelper depends on ITokenSigner + MTokenInfo which are registered by
        // AddValidateBearerToken — both are resolved at request time (not at startup), so
        // registration order does not matter.
        services.TryAddScoped<MAuthenticateTokenHelper<TPermission>>();
        services.TryAddScoped<IAuthenticateRepository, AuthenticateRepository<TDbContext, TPermission>>();
        services.TryAddScoped<IRefreshTokenValidator, DefaultRefreshTokenValidator<TDbContext, TPermission>>();

        // Fallback hasher — throws a clear error if used without Muonroi.Auth configured.
        // AddInMemoryRsaKeyStore / AddRedisRsaKeyStore override this with BCryptPasswordHasher.
        services.TryAddSingleton<IPasswordHasher, NotConfiguredPasswordHasher>();
    }

    /// <summary>
    /// Fallback hasher registered when Muonroi.Auth has not been configured.
    /// Throws a clear error so developers know exactly what to do.
    /// Overridden by BCryptPasswordHasher when AddInMemoryRsaKeyStore / AddRedisRsaKeyStore is called.
    /// </summary>
    private sealed class NotConfiguredPasswordHasher : IPasswordHasher
    {
        private const string Msg =
            "IPasswordHasher is not configured. Call services.AddInMemoryRsaKeyStore() or " +
            "services.AddRedisRsaKeyStore() to register BCryptPasswordHasher.";

        public string HashPassword(string password, out string salt)
        {
            MGuard.State(false, Msg, "NOT_CONFIGURED");
            salt = string.Empty;
            return string.Empty;
        }

        public bool VerifyPassword(string enteredPassword, string storedHash)
        {
            MGuard.State(false, Msg, "NOT_CONFIGURED");
            return false;
        }
    }


    private static string DecryptConnectionString(DatabaseConfigs databaseConfigs, IConfiguration configuration,
        bool isSecretDefault, string secretKey, string fingerprint = "")
    {
        bool enableEncryption = configuration.GetValue("EnableEncryption", false);

        // Encryption is OPT-IN, not mandatory
        if (!enableEncryption)
        {
            MGuard.State(databaseConfigs.DbType is nameof(DbTypes.SqlServer) or nameof(DbTypes.MySql) or nameof(DbTypes.PostgreSql) or nameof(DbTypes.Sqlite), "Unsupported database type: " + databaseConfigs.DbType);
            
            string? rawConn = databaseConfigs.DbType switch
            {
                nameof(DbTypes.SqlServer) => databaseConfigs.ConnectionStrings?.SqlServerConnectionString,
                nameof(DbTypes.MySql) => databaseConfigs.ConnectionStrings?.MySqlConnectionString,
                nameof(DbTypes.PostgreSql) => databaseConfigs.ConnectionStrings?.PostgreSqlConnectionString,
                nameof(DbTypes.Sqlite) => databaseConfigs.ConnectionStrings?.SqliteConnectionString,
                _ => null
            };
            return MGuard.Configured(rawConn, "DatabaseConfigs:ConnectionStrings");
        }

        // Encryption enabled - decrypt the connection string
        MGuard.State(databaseConfigs.DbType is nameof(DbTypes.SqlServer) or nameof(DbTypes.MySql) or nameof(DbTypes.PostgreSql) or nameof(DbTypes.Sqlite), "Unsupported database type: " + databaseConfigs.DbType);
        
        string? encryptedConn = databaseConfigs.DbType switch
        {
            nameof(DbTypes.SqlServer) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.SqlServerConnectionString, isSecretDefault, secretKey, fingerprint),
            nameof(DbTypes.MySql) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.MySqlConnectionString, isSecretDefault, secretKey, fingerprint),
            nameof(DbTypes.PostgreSql) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.PostgreSqlConnectionString, isSecretDefault, secretKey, fingerprint),
            nameof(DbTypes.Sqlite) => MStringExtension.DecryptConfigurationValue(configuration,
                databaseConfigs.ConnectionStrings?.SqliteConnectionString, isSecretDefault, secretKey, fingerprint),
            _ => null
        };
        return MGuard.Configured(encryptedConn, "DatabaseConfigs:ConnectionStrings");
    }

    private static void ConfigureDbContext<T, TPermission>(IServiceCollection services, string dbType, bool isSecretDefault, string secretKey)
        where T : MDbContext
        where TPermission : Enum
    {
        MGuard.State(dbType is nameof(DbTypes.SqlServer) or nameof(DbTypes.MySql) or nameof(DbTypes.PostgreSql) or nameof(DbTypes.Sqlite), "Unsupported database type: " + dbType);
        
        Type? configuratorType = dbType switch
        {
            nameof(DbTypes.SqlServer) => Type.GetType("Muonroi.Data.EntityFrameworkCore.SqlServer.SqlServerDbContextConfigurator`1, Muonroi.Data.EntityFrameworkCore.SqlServer"),
            nameof(DbTypes.MySql) => Type.GetType("Muonroi.Data.EntityFrameworkCore.MySql.MySqlDbContextConfigurator`1, Muonroi.Data.EntityFrameworkCore.MySql"),
            nameof(DbTypes.PostgreSql) => Type.GetType("Muonroi.Data.EntityFrameworkCore.PostgreSQL.PostgreSqlDbContextConfigurator`1, Muonroi.Data.EntityFrameworkCore.PostgreSQL"),
            nameof(DbTypes.Sqlite) => Type.GetType("Muonroi.Data.EntityFrameworkCore.Sqlite.SqliteDbContextConfigurator`1, Muonroi.Data.EntityFrameworkCore.Sqlite"),
            _ => null
        };

        MGuard.Configured(configuratorType != null, $"Database provider package for {dbType} is not installed. Please reference Muonroi.Data.EntityFrameworkCore.{dbType} package.", "DatabaseConfigs:DbType");

        services.AddScoped(typeof(IDbContextConfigurator<T>), MGuard.NotNull(configuratorType).MakeGenericType(typeof(T)));

        _ = services.AddDbContext<T>((serviceProvider, options) =>
        {
            IDbContextConfigurator<T> configurator = serviceProvider.GetRequiredService<IDbContextConfigurator<T>>();
            ITenantContext tenantContext = serviceProvider.GetService<ITenantContext>() ?? new NoOpTenantContext();
            ILicenseGuard? guard = serviceProvider.GetService<ILicenseGuard>();
            IConfiguration config = serviceProvider.GetRequiredService<IConfiguration>();

            DatabaseConfigs? rawDbConfigs = config.GetSection(nameof(DatabaseConfigs)).Get<DatabaseConfigs>();
            DatabaseConfigs dbConfigs = MGuard.Configured(rawDbConfigs, "DatabaseConfigs");

            bool enableEncryption = config.GetValue("EnableEncryption", false);
            string connectionString;

            if (enableEncryption)
            {
                // ENTANGLEMENT: Perform the decryption inside the guard's secure scope using LIVE key
                MGuard.Configured(guard != null, "ILicenseGuard is required when encryption is enabled.", "EnableEncryption");
                connectionString = MGuard.NotNull(guard).DecryptSecurely("db_connection", "dummy", (key, _) =>
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
