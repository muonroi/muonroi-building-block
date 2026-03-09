using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Muonroi.RuleEngine.CEP.Options;

namespace Muonroi.RuleEngine.CEP.Persistence;

internal sealed class CepConfigDatabaseMigrator(
    IServiceProvider serviceProvider,
    IOptions<CepOptions> options) : IHostedService
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
        CepConfigDbContext dbContext = scope.ServiceProvider.GetRequiredService<CepConfigDbContext>();
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
