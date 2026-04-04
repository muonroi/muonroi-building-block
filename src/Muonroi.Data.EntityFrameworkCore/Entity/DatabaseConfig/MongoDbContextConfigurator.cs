using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;

/// <summary>
/// Configures MongoDB services for a <see cref="MDbContext"/> that uses MongoDB for storage.
/// </summary>
public class MongoDbContextConfigurator<T> : IDbContextConfigurator<T> where T : MDbContext
{
    /// <summary>
    /// MongoDB does not use EF Core options, so this throws by design.
    /// </summary>
    /// <param name="options">Unused options builder.</param>
    /// <param name="connectionString">Unused connection string.</param>
    /// <exception cref="NotSupportedException">Always thrown for MongoDB.</exception>
    public void Configure(DbContextOptionsBuilder<T> options, string connectionString)
    {
        throw new NotSupportedException(
            "MongoDB does not use DbContextOptionsBuilder. Configure MongoDB services directly in the IServiceCollection.");
    }

    /// <summary>
    /// Registers MongoDB client and related settings from configuration.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public IServiceCollection ConfigureMongoDb(IServiceCollection services, IConfiguration configuration)
    {
        string? mongoDbConnectionString = configuration.GetConnectionString("MongoDbConnectionString");
        if (string.IsNullOrEmpty(mongoDbConnectionString))
            throw new MConfigurationException("MongoDb connection string is not configured.", "ConnectionStrings:MongoDbConnectionString");

        string? mongoDbName = configuration.GetSection("DatabaseConfigs")["DatabaseName"];
        if (string.IsNullOrEmpty(mongoDbName))
            throw new MConfigurationException("MongoDb database name is not configured.", "DatabaseConfigs:DatabaseName");

        string result = $"{mongoDbConnectionString}/{mongoDbName}?authSource=admin";

        services.AddSingleton<IMongoClient>(new MongoClient(result))
            .AddScoped(x => x.GetService<IMongoClient>()!.StartSession());

        DatabaseConfigs? databaseSettings = configuration.GetSection(nameof(DatabaseConfigs)).Get<DatabaseConfigs>();
        services.AddSingleton(databaseSettings!);

        return services;
    }
}
