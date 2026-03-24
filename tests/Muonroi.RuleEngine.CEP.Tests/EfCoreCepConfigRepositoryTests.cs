using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.RuleEngine.CEP.Abstractions;
using Muonroi.RuleEngine.CEP.Options;
using Muonroi.RuleEngine.CEP.Persistence;
using Muonroi.RuleEngine.CEP.Repositories;

namespace Muonroi.RuleEngine.CEP.Tests;

public sealed class EfCoreCepConfigRepositoryTests
{
    [Fact]
    public async Task SaveAndGet_RoundTripsConfig_WithSqlite()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext("tenant-a", null, null, "corr-a", null, null, false, [], "test"));

        await using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();
        await using CepConfigDbContext dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        EfCoreCepConfigRepository repository = new(
            dbContext,
            new StubDateTimeService(new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)),
            new MJsonSerializeService(),
            accessor);

        CepConfig saved = await repository.SaveAsync(new CepConfig
        {
            Id = "fraud",
            Name = "Fraud",
            WindowType = WindowType.Sliding,
            WindowSize = TimeSpan.FromSeconds(30),
            TimeToLive = TimeSpan.FromMinutes(5),
            CorrelationKey = "cardId",
            Metadata = new Dictionary<string, string> { ["threshold"] = "3" }
        });

        CepConfig? loaded = await repository.GetAsync("fraud");

        Assert.NotNull(loaded);
        Assert.Equal("tenant-a", saved.TenantId);
        Assert.Equal("Fraud", loaded!.Name);
        Assert.Equal("cardId", loaded.CorrelationKey);
        Assert.Equal("3", loaded.Metadata["threshold"]);
    }

    [Fact]
    public async Task ListAndDelete_AreTenantScoped_WithSqlite()
    {
        SystemExecutionContextAccessor accessor = new();
        await using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();
        await using CepConfigDbContext dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync();

        EfCoreCepConfigRepository repository = new(
            dbContext,
            new StubDateTimeService(new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)),
            new MJsonSerializeService(),
            accessor);

        accessor.Set(new SystemExecutionContext("tenant-a", null, null, "corr-a", null, null, false, [], "test"));
        await repository.SaveAsync(new CepConfig { Id = "shared", Name = "Tenant A", WindowSize = TimeSpan.FromSeconds(10), TimeToLive = TimeSpan.FromSeconds(30) });

        accessor.Set(new SystemExecutionContext("tenant-b", null, null, "corr-b", null, null, false, [], "test"));
        await repository.SaveAsync(new CepConfig { Id = "shared", Name = "Tenant B", WindowSize = TimeSpan.FromSeconds(10), TimeToLive = TimeSpan.FromSeconds(30) });

        IReadOnlyList<CepConfig> tenantBItems = await repository.ListAsync();
        bool removed = await repository.DeleteAsync("shared");
        CepConfig? tenantBConfig = await repository.GetAsync("shared");

        accessor.Set(new SystemExecutionContext("tenant-a", null, null, "corr-a", null, null, false, [], "test"));
        CepConfig? tenantAConfig = await repository.GetAsync("shared");

        Assert.Single(tenantBItems);
        Assert.Equal("tenant-b", tenantBItems[0].TenantId);
        Assert.True(removed);
        Assert.Null(tenantBConfig);
        Assert.NotNull(tenantAConfig);
    }

    [Fact]
    public void AddCepWeb_UsesEfRepository_WhenConnectionStringConfigured()
    {
        ServiceCollection services = new();

        services.AddSingleton<Muonroi.Mediator.Mediator.Interfaces.IMediator, StubMediator>();
        services.AddCepWeb(options => options.PostgresConnectionString = "Host=localhost;Database=cep;Username=test;Password=test");

        ServiceDescriptor descriptor = Assert.Single(
            services, x => x.ServiceType == typeof(ICepConfigRepository));

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal("EfCoreCepConfigRepository", descriptor.ImplementationType?.Name);
    }

    private static CepConfigDbContext CreateDbContext(SqliteConnection connection)
    {
        DbContextOptions<CepConfigDbContext> options = new DbContextOptionsBuilder<CepConfigDbContext>()
            .UseSqlite(connection)
            .Options;

        return new CepConfigDbContext(
            options,
            new StubMediator(),
            Microsoft.Extensions.Options.Options.Create(new CepOptions { Schema = null }));
    }
}
