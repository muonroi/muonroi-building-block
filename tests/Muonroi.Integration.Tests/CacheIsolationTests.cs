using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Core.Abstractions.Context;
namespace Muonroi.Integration.Tests;


public class CacheIsolationTests
{
    [Fact]
    public async Task MultiTenant_CacheIsolation_ShouldWork()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();

        Mock<ISystemExecutionContextAccessor> mockAccessor = new();
        Mock<ISystemExecutionContext> mockContext = new();
        mockAccessor.Setup(x => x.Get()).Returns(mockContext.Object);

        services.AddSingleton(mockAccessor.Object);
        services.AddSingleton<IMultiLevelCacheService, MultiLevelCacheService>();

        ServiceProvider provider = services.BuildServiceProvider();
        IMultiLevelCacheService cache = provider.GetRequiredService<IMultiLevelCacheService>();

        // Act & Assert

        // Tenant A
        mockContext.Setup(x => x.TenantId).Returns("TenantA");
        await cache.SetAsync("foo", "ValueA");

        // Tenant B
        mockContext.Setup(x => x.TenantId).Returns("TenantB");
        await cache.SetAsync("foo", "ValueB");

        // Verify Tenant A still has ValueA
        mockContext.Setup(x => x.TenantId).Returns("TenantA");
        string? valA = await cache.GetAsync<string>("foo");
        valA.Should().Be("ValueA");

        // Verify Tenant B still has ValueB
        mockContext.Setup(x => x.TenantId).Returns("TenantB");
        string? valB = await cache.GetAsync<string>("foo");
        valB.Should().Be("ValueB");
    }
}
