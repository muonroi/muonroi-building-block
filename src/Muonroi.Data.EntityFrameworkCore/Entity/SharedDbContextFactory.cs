using Muonroi.Mediator.Mediator;

namespace Muonroi.Data.EntityFrameworkCore.Entity;

/// <summary>
/// Creates a design-time DbContext for EF Core tooling.
/// </summary>
/// <typeparam name="TContext">The EF Core context type.</typeparam>
public class SharedDbContextFactory<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : MDbContext
{
    /// <summary>
    /// Creates a DbContext instance for design-time operations.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>A configured DbContext instance.</returns>
    public TContext CreateDbContext(string[] args)
    {
        string? environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false, true)
            .AddJsonFile($"appsettings.{environmentName}.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        DatabaseConfigs? databaseConfigs = configuration.GetSection("DatabaseConfigs").Get<DatabaseConfigs>();
        if (databaseConfigs == null || string.IsNullOrEmpty(databaseConfigs.DbType))
        {
            throw new InvalidOperationException("Database configuration is not properly set.");
        }

        DbContextOptionsBuilder<TContext> builder = new();
        string connectionString = databaseConfigs.DbType switch
        {
            nameof(DbTypes.SqlServer) => databaseConfigs.ConnectionStrings?.SqlServerConnectionString,
            nameof(DbTypes.MySql) => databaseConfigs.ConnectionStrings?.MySqlConnectionString,
            nameof(DbTypes.PostgreSql) => databaseConfigs.ConnectionStrings?.PostgreSqlConnectionString,
            nameof(DbTypes.Sqlite) => databaseConfigs.ConnectionStrings?.SqliteConnectionString,
            _ => throw new InvalidOperationException("Unsupported database type: " + databaseConfigs.DbType)
        } ?? throw new InvalidOperationException("Connection string is not provided or is empty.");

        connectionString =
            MStringExtension.DecryptConfigurationValue(configuration, connectionString, true, string.Empty)
            ?? throw new InvalidOperationException("Connection string is not provided or is empty.");

        _ = databaseConfigs.DbType switch
        {
            nameof(DbTypes.SqlServer) => builder.UseSqlServer(connectionString),
            nameof(DbTypes.MySql) => builder.UseMySql(connectionString,
                Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(connectionString)),
            nameof(DbTypes.PostgreSql) => builder.UseNpgsql(connectionString),
            nameof(DbTypes.Sqlite) => builder.UseSqlite(connectionString),
            _ => throw new InvalidOperationException("Unsupported database type: " + databaseConfigs.DbType)
        };

        // Try to create with design-time compatible constructor
        // MDbContext-derived classes should have: (DbContextOptions, IMediator, ILicenseGuard?, IMLog?)
        Type contextType = typeof(TContext);

        // Try full constructor first
        ConstructorInfo? fullCtor = contextType.GetConstructor([
            typeof(DbContextOptions<TContext>), typeof(IMediator),
            typeof(ILicenseGuard), typeof(IMLog<>).MakeGenericType(contextType)
        ]);

        if (fullCtor != null)
        {
            return (TContext)fullCtor.Invoke([builder.Options, new NoMediator(), null, null]);
        }

        // Try constructor with base DbContextOptions
        ConstructorInfo? baseCtor = contextType.GetConstructor([
            typeof(DbContextOptions), typeof(IMediator),
            typeof(ILicenseGuard), typeof(IMLog<>).MakeGenericType(contextType)
        ]);

        if (baseCtor != null)
        {
            return (TContext)baseCtor.Invoke([builder.Options, new NoMediator(), null, null]);
        }

        // Fallback: try any constructor that accepts DbContextOptions
        foreach (ConstructorInfo ctor in contextType.GetConstructors())
        {
            ParameterInfo[] parameters = ctor.GetParameters();
            if (parameters.Length >= 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(DbContextOptions)))
            {
                object?[] ctorArgs = new object?[parameters.Length];
                ctorArgs[0] = builder.Options;

                for (int i = 1; i < parameters.Length; i++)
                {
                    ParameterInfo param = parameters[i];
                    if (param.ParameterType == typeof(IMediator))
                    {
                        ctorArgs[i] = new NoMediator();
                    }
                    else if (param.HasDefaultValue)
                    {
                        ctorArgs[i] = param.DefaultValue;
                    }
                    else
                    {
                        ctorArgs[i] = null;
                    }
                }

                return (TContext)ctor.Invoke(ctorArgs);
            }
        }

        throw new InvalidOperationException(
            $"Could not find a suitable constructor for {contextType.Name}. " +
            "Ensure it has a constructor accepting DbContextOptions.");
    }
}
