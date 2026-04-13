using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Diagnostics.Extensions;
using Muonroi.Diagnostics.Store;
using Xunit;

namespace Muonroi.Diagnostics.Tests;

public class MDiagnosticsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddMuonroiDiagnostics_Should_Register_TraceContext()
    {
        ServiceCollection services = new();
        services.AddSingleton(Mock.Of<IMJsonSerializeService>());
        services.AddMuonroiDiagnostics();

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.GetService<IMTraceContext>().Should().NotBeNull();
    }

    [Fact]
    public void AddMuonroiDiagnostics_Should_Register_InMemoryStore()
    {
        ServiceCollection services = new();
        services.AddSingleton(Mock.Of<IMJsonSerializeService>());
        services.AddMuonroiDiagnostics();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITraceSessionStore store = provider.GetRequiredService<ITraceSessionStore>();
        store.Should().BeOfType<InMemoryTraceSessionStore>();
    }

    [Fact]
    public void AddMuonroiDiagnosticsRedis_Should_Register_RedisStore()
    {
        ServiceCollection services = new();
        services.AddSingleton(Mock.Of<IMJsonSerializeService>());
        services.AddMuonroiDiagnosticsRedis();

        using ServiceProvider provider = services.BuildServiceProvider();
        // RedisTraceSessionStore requires IConnectionMultiplexer so it cannot be resolved here;
        // but it should be registered
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ITraceSessionStore));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be(typeof(RedisTraceSessionStore));
    }
}
