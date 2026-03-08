namespace Muonroi.Integration.Tests;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Caching.Memory.MultiLevel;
using Muonroi.Tenancy.Core;
using FluentAssertions;
using Xunit;
using Moq;
using Muonroi.Core.Abstractions.Context;

public class CacheIsolationTests
{
    [Fact]
    public async Task MultiTenant_CacheIsolation_ShouldWork()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        
        var mockAccessor = new Mock<ISystemExecutionContextAccessor>();
        var mockContext = new Mock<ISystemExecutionContext>();
        mockAccessor.Setup(x => x.Get()).Returns(mockContext.Object);
        
        services.AddSingleton(mockAccessor.Object);
        services.AddSingleton<IMultiLevelCacheService, MultiLevelCacheService>();
        
        var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IMultiLevelCacheService>();

        // Act & Assert
        
        // Tenant A
        mockContext.Setup(x => x.TenantId).Returns("TenantA");
        await cache.SetAsync("foo", "ValueA");

        // Tenant B
        mockContext.Setup(x => x.TenantId).Returns("TenantB");
        await cache.SetAsync("foo", "ValueB");

        // Verify Tenant A still has ValueA
        mockContext.Setup(x => x.TenantId).Returns("TenantA");
        var valA = await cache.GetAsync<string>("foo");
        valA.Should().Be("ValueA");

        // Verify Tenant B still has ValueB
        mockContext.Setup(x => x.TenantId).Returns("TenantB");
        var valB = await cache.GetAsync<string>("foo");
        valB.Should().Be("ValueB");
    }
}
