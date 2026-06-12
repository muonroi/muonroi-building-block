using Microsoft.Extensions.DependencyInjection;
using Muonroi.Core.Abstractions.Ecosystem;

namespace Muonroi.Core.Abstractions.Tests.Ecosystem;

public class EcosystemServiceCollectionExtensionsTests
{
    [Fact]
    public void GetOrCreateRegistry_OnEmptyCollection_ReturnsNonNullRegistry()
    {
        var services = new ServiceCollection();
        var registry = services.GetOrCreateRegistry();
        registry.Should().NotBeNull();
    }

    [Fact]
    public void GetOrCreateRegistry_ReturnedInstance_IsIMEcosystemRegistry()
    {
        var services = new ServiceCollection();
        var registry = services.GetOrCreateRegistry();
        registry.Should().BeAssignableTo<IMEcosystemRegistry>();
    }

    [Fact]
    public void GetOrCreateRegistry_ServiceDescriptorPresent_AfterCall()
    {
        var services = new ServiceCollection();
        services.GetOrCreateRegistry();
        services.Should().Contain(sd => sd.ServiceType == typeof(IMEcosystemRegistry));
    }

    [Fact]
    public void GetOrCreateRegistry_CalledTwice_ServiceDescriptorRegisteredOnce()
    {
        // TryAddSingleton ensures first registration wins
        var services = new ServiceCollection();
        services.GetOrCreateRegistry();
        services.GetOrCreateRegistry();

        var count = services.Count(sd => sd.ServiceType == typeof(IMEcosystemRegistry));
        count.Should().Be(1);
    }

    [Fact]
    public void GetOrCreateRegistry_RegisterAndHas_WorkEndToEnd()
    {
        var services = new ServiceCollection();
        var registry = services.GetOrCreateRegistry();

        // The registry returned is functional
        registry.Register(MCapability.RuleEngine);
        registry.Has(MCapability.RuleEngine).Should().BeTrue();
        registry.Has(MCapability.Auth).Should().BeFalse();
    }

    [Fact]
    public void GetOrCreateRegistry_ServiceDescriptor_IsSingleton()
    {
        var services = new ServiceCollection();
        services.GetOrCreateRegistry();

        var descriptor = services.First(sd => sd.ServiceType == typeof(IMEcosystemRegistry));
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
