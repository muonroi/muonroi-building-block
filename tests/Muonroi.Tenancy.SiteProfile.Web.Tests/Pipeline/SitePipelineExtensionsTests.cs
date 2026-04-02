using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile.Web.Pipeline;
using NSubstitute;

namespace Muonroi.Tenancy.SiteProfile.Web.Tests.Pipeline;

/// <summary>
/// Unit tests for SitePipelineExtensions: verifies DI registration behavior.
/// </summary>
public class SitePipelineExtensionsTests
{
    // Dummy service type for TService generic parameter
    private sealed class DummyService;

    // -----------------------------------------------------------------------
    // EXT-01: AddSitePipeline registers SitePipelineHookRegistry as singleton
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSitePipeline_RegistersSitePipelineHookRegistry_AsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSitePipeline();

        // Act
        ServiceProvider sp = services.BuildServiceProvider();
        var registry1 = sp.GetRequiredService<SitePipelineHookRegistry>();
        var registry2 = sp.GetRequiredService<SitePipelineHookRegistry>();

        // Assert
        registry1.Should().NotBeNull();
        registry1.Should().BeSameAs(registry2, "singleton must return same instance");
    }

    // -----------------------------------------------------------------------
    // EXT-02: AddSitePipeline registers MSitePipeline<> as open-generic scoped
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSitePipeline_RegistersMSitePipeline_AsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSitePipeline();

        // Act
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(MSitePipeline<>));

        // Assert
        descriptor.Should().NotBeNull("MSitePipeline<> must be registered");
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    // -----------------------------------------------------------------------
    // EXT-03: AddSiteStepHook accumulates hooks visible via SitePipelineHookRegistry
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSiteStepHook_HooksAccumulated_VisibleViaRegistry()
    {
        // Arrange
        var services = new ServiceCollection();
        ISiteStepHook fakeHook = Substitute.For<ISiteStepHook>();

        services.AddSitePipeline();
        services.AddSiteStepHook<DummyService>(
            siteId: "sg01",
            stepName: "ProcessStep",
            phase: SiteStepHookPhase.Before,
            factory: _ => fakeHook);

        // Act
        ServiceProvider sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<SitePipelineHookRegistry>();

        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> factories =
            registry.GetHookFactories("sg01", nameof(DummyService), "ProcessStep", SiteStepHookPhase.Before);

        // Assert
        factories.Should().HaveCount(1);
        ISiteStepHook resolved = factories[0](sp);
        resolved.Should().BeSameAs(fakeHook);
    }

    // -----------------------------------------------------------------------
    // EXT-04: Multiple hooks for same (siteId, serviceName, stepName, phase) are all returned
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSiteStepHook_MultipleHooksForSameKey_AllRegistered()
    {
        // Arrange
        var services = new ServiceCollection();
        ISiteStepHook hook1 = Substitute.For<ISiteStepHook>();
        ISiteStepHook hook2 = Substitute.For<ISiteStepHook>();

        services.AddSitePipeline();
        services.AddSiteStepHook<DummyService>(
            siteId: "sg01", stepName: "ProcessStep", phase: SiteStepHookPhase.After, factory: _ => hook1);
        services.AddSiteStepHook<DummyService>(
            siteId: "sg01", stepName: "ProcessStep", phase: SiteStepHookPhase.After, factory: _ => hook2);

        // Act
        ServiceProvider sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<SitePipelineHookRegistry>();
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> factories =
            registry.GetHookFactories("sg01", nameof(DummyService), "ProcessStep", SiteStepHookPhase.After);

        // Assert
        factories.Should().HaveCount(2);
    }

    // -----------------------------------------------------------------------
    // EXT-05: Service name derived from typeof(TService).Name
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSiteStepHook_ServiceNameDerivedFromTypeName()
    {
        // Arrange
        var services = new ServiceCollection();
        ISiteStepHook hook = Substitute.For<ISiteStepHook>();

        services.AddSitePipeline();
        services.AddSiteStepHook<DummyService>(
            siteId: "hn01", stepName: "ValidateStep", phase: SiteStepHookPhase.Replace, factory: _ => hook);

        // Act
        ServiceProvider sp = services.BuildServiceProvider();
        var registry = sp.GetRequiredService<SitePipelineHookRegistry>();

        // Registered under "DummyService" (not the full namespace)
        IReadOnlyList<Func<IServiceProvider, ISiteStepHook>> factories =
            registry.GetHookFactories("hn01", "DummyService", "ValidateStep", SiteStepHookPhase.Replace);

        // Assert
        factories.Should().HaveCount(1);
    }

    // -----------------------------------------------------------------------
    // EXT-06: AddSitePipeline is idempotent (TryAdd pattern — no duplicates)
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSitePipeline_CalledTwice_RegistrySingletonNotDuplicated()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSitePipeline();
        services.AddSitePipeline(); // second call should be a no-op

        // Act
        ServiceProvider sp = services.BuildServiceProvider();
        var registry1 = sp.GetRequiredService<SitePipelineHookRegistry>();
        var registry2 = sp.GetRequiredService<SitePipelineHookRegistry>();

        // Assert — still single instance
        registry1.Should().BeSameAs(registry2);
    }
}
