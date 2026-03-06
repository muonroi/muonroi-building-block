using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Muonroi.RuleEngine.DecisionTable.Stores.Persistence;

internal sealed class DecisionTableDatabaseMigrator(
    IServiceProvider serviceProvider,
    IOptions<DecisionTableEngineOptions> options) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.SqlServerConnectionString) || !options.Value.AutoMigrateDatabase)
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

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
