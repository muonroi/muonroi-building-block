using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Muonroi.RuleEngine.DecisionTable.Stores.Persistence;

[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "EF Core infrastructure methods return values guaranteed non-null by the framework; null-forgiving is structurally correct here.")]
internal sealed class DecisionTableDatabaseMigrator(
    IServiceProvider serviceProvider,
    IOptions<DecisionTableEngineOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        bool hasConnectionString =
            !string.IsNullOrWhiteSpace(options.Value.PostgresConnectionString) ||
            !string.IsNullOrWhiteSpace(options.Value.SqlServerConnectionString);
        if (!hasConnectionString || !options.Value.AutoMigrateDatabase)
        {
            return;
        }

        using IServiceScope scope = serviceProvider.CreateScope();
        DecisionTableDbContext dbContext = scope.ServiceProvider.GetRequiredService<DecisionTableDbContext>();
        bool hasMigrations = dbContext.Database.GetMigrations().Any();
        if (hasMigrations)
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            return;
        }

        IRelationalDatabaseCreator databaseCreator = dbContext.GetService<IRelationalDatabaseCreator>();
        if (await ContextTablesExistAsync(dbContext, cancellationToken))
        {
            return;
        }

        if (await databaseCreator.HasTablesAsync(cancellationToken))
        {
            await databaseCreator.CreateTablesAsync(cancellationToken);
            return;
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static async Task<bool> ContextTablesExistAsync(
        DecisionTableDbContext dbContext,
        CancellationToken cancellationToken)
    {
        DbConnection connection = dbContext.Database.GetDbConnection();
        bool shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            string defaultSchema = dbContext.Model.GetDefaultSchema() ?? "public";
            IEnumerable<(string Schema, string Table)> mappedTables = dbContext.Model.GetEntityTypes()
                .Select(entityType => (Schema: entityType.GetSchema() ?? defaultSchema, Table: entityType.GetTableName()))
                .Where(item => !string.IsNullOrWhiteSpace(item.Table))
                .Select(item => (item.Schema, item.Table!))
                .Distinct();

            foreach ((string schema, string table) in mappedTables)
            {
                await using DbCommand command = connection.CreateCommand();
                command.CommandText = """
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = @schema AND table_name = @table
                    LIMIT 1;
                    """;

                DbParameter schemaParameter = command.CreateParameter();
                schemaParameter.ParameterName = "@schema";
                schemaParameter.Value = schema;
                command.Parameters.Add(schemaParameter);

                DbParameter tableParameter = command.CreateParameter();
                tableParameter.ParameterName = "@table";
                tableParameter.Value = table;
                command.Parameters.Add(tableParameter);

                object? result = await command.ExecuteScalarAsync(cancellationToken);
                if (result is not null && result is not DBNull)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
