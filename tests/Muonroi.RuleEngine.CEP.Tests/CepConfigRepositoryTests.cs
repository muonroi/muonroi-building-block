using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.CEP.Repositories;

namespace Muonroi.RuleEngine.CEP.Tests;

public class CepConfigRepositoryTests
{
    [Fact]
    public async Task SaveAndGet_RoundTripsConfig()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext("tenant-a", null, null, "corr-a", null, null, false, [], "test"));
        InMemoryCepConfigRepository repository = new(new StubDateTimeService(new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)), accessor);

        CepConfig saved = await repository.SaveAsync(new CepConfig
        {
            Id = "fraud",
            Name = "Fraud",
            WindowType = WindowType.Sliding,
            WindowSize = TimeSpan.FromSeconds(30),
            TimeToLive = TimeSpan.FromMinutes(5),
            CorrelationKey = "cardId"
        });

        CepConfig? loaded = await repository.GetAsync("fraud");

        Assert.NotNull(loaded);
        Assert.Equal("tenant-a", saved.TenantId);
        Assert.Equal("Fraud", loaded!.Name);
        Assert.Equal("cardId", loaded.CorrelationKey);
    }

    [Fact]
    public async Task ListAsync_IsTenantScoped()
    {
        SystemExecutionContextAccessor accessor = new();
        InMemoryCepConfigRepository repository = new(new StubDateTimeService(new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)), accessor);

        accessor.Set(new SystemExecutionContext("tenant-a", null, null, "corr-a", null, null, false, [], "test"));
        await repository.SaveAsync(new CepConfig { Id = "a1", Name = "Tenant A", WindowSize = TimeSpan.FromSeconds(10), TimeToLive = TimeSpan.FromSeconds(10) });

        accessor.Set(new SystemExecutionContext("tenant-b", null, null, "corr-b", null, null, false, [], "test"));
        await repository.SaveAsync(new CepConfig { Id = "b1", Name = "Tenant B", WindowSize = TimeSpan.FromSeconds(10), TimeToLive = TimeSpan.FromSeconds(10) });

        IReadOnlyList<CepConfig> tenantBItems = await repository.ListAsync();

        Assert.Single(tenantBItems);
        Assert.Equal("tenant-b", tenantBItems[0].TenantId);
        Assert.Equal("b1", tenantBItems[0].Id);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyCurrentTenantConfig()
    {
        SystemExecutionContextAccessor accessor = new();
        InMemoryCepConfigRepository repository = new(new StubDateTimeService(new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)), accessor);

        accessor.Set(new SystemExecutionContext("tenant-a", null, null, "corr-a", null, null, false, [], "test"));
        await repository.SaveAsync(new CepConfig { Id = "shared", Name = "Tenant A", WindowSize = TimeSpan.FromSeconds(10), TimeToLive = TimeSpan.FromSeconds(10) });

        accessor.Set(new SystemExecutionContext("tenant-b", null, null, "corr-b", null, null, false, [], "test"));
        await repository.SaveAsync(new CepConfig { Id = "shared", Name = "Tenant B", WindowSize = TimeSpan.FromSeconds(10), TimeToLive = TimeSpan.FromSeconds(10) });
        bool removed = await repository.DeleteAsync("shared");
        CepConfig? tenantBConfig = await repository.GetAsync("shared");

        accessor.Set(new SystemExecutionContext("tenant-a", null, null, "corr-a", null, null, false, [], "test"));
        CepConfig? tenantAConfig = await repository.GetAsync("shared");

        Assert.True(removed);
        Assert.Null(tenantBConfig);
        Assert.NotNull(tenantAConfig);
    }
}
