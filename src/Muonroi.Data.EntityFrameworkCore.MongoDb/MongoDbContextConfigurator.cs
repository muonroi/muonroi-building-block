using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Data.EntityFrameworkCore.Entity;
using Muonroi.Data.EntityFrameworkCore.Entity.DatabaseConfig;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.Data.EntityFrameworkCore.MongoDb;

/// <summary>
/// Configures MongoDB services for a <see cref="MDbContext"/> that uses MongoDB for storage.
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "MongoDB configurator uses reflection-based type registration where MGuard.NotNull is incompatible with unconstrained generic type parameters.")]
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
        MGuard.Fail<object>(
            "MongoDB does not use DbContextOptionsBuilder. Configure MongoDB services directly in the IServiceCollection.",
            MErrorCodes.Data.MongoNotSupported);
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
            return MGuard.Fail<IServiceCollection>("MongoDb connection string is not configured.", "ConnectionStrings:MongoDbConnectionString");

        string? mongoDbName = configuration.GetSection("DatabaseConfigs")["DatabaseName"];
        if (string.IsNullOrEmpty(mongoDbName))
            return MGuard.Fail<IServiceCollection>("MongoDb database name is not configured.", "DatabaseConfigs:DatabaseName");

        string result = $"{mongoDbConnectionString}/{mongoDbName}?authSource=admin";

        services.AddSingleton<IMongoClient>(new MongoClient(result))
            .AddScoped(x => x.GetService<IMongoClient>()!.StartSession());

        DatabaseConfigs? databaseSettings = configuration.GetSection(nameof(DatabaseConfigs)).Get<DatabaseConfigs>();
        services.AddSingleton(databaseSettings!);

        return services;
    }
}
