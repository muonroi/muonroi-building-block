namespace Muonroi.Tenancy.SiteProfile.Tests;

public class SiteProfileBootstrapTests
{
    private class FakeDbContext : DbContext { }

    private class FakeBehavior : ISiteProfileBehavior
    {
        public void Apply(IServiceCollection services, IConfiguration configuration, string siteId)
        {
            services.AddSingleton(new BehaviorMarker(siteId));
        }
    }

    private record BehaviorMarker(string SiteId);

    [Fact]
    public void RegisterSiteServices_WithDbContextType_RegistersDbContext()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        SiteProfileBootstrap.RegisterSiteServices("TEST", typeof(FakeDbContext), null, services, config);

        // Check if DbContextOptions<FakeDbContext> is registered (standard EF registration via AddSiteDbContext)
        services.Should().Contain(x => x.ServiceType == typeof(DbContextOptions<FakeDbContext>));
    }

    [Fact]
    public void RegisterSiteServices_SkipDbContext_True_SkipsRegistration()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        SiteProfileBootstrap.RegisterSiteServices("TEST", typeof(FakeDbContext), null, services, config, skipDbContext: true);

        services.Should().NotContain(x => x.ServiceType == typeof(DbContextOptions<FakeDbContext>));
    }

    [Fact]
    public void RegisterSiteServices_NullDbContextType_SkipsRegistration()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        SiteProfileBootstrap.RegisterSiteServices("TEST", null, null, services, config);

        services.Should().NotContain(x => x.ServiceType == typeof(DbContextOptions<FakeDbContext>));
    }

    [Fact]
    public void RegisterSiteServices_WithBehaviorTypes_AppliesBehaviors()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        SiteProfileBootstrap.RegisterSiteServices("TEST", null, [typeof(FakeBehavior)], services, config);

        var provider = services.BuildServiceProvider();
        var marker = provider.GetService<BehaviorMarker>();
        marker.Should().NotBeNull();
        marker!.SiteId.Should().Be("TEST");
    }

    [Fact]
    public void RegisterSiteServices_NullBehaviorTypes_NoBehaviorApplied()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        SiteProfileBootstrap.RegisterSiteServices("TEST", null, null, services, config);

        var provider = services.BuildServiceProvider();
        provider.GetService<BehaviorMarker>().Should().BeNull();
    }

    [Fact]
    public void RegisterSiteServices_InvalidBehaviorType_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        // Pass a type that doesn't implement ISiteProfileBehavior
        SiteProfileBootstrap.RegisterSiteServices("TEST", null, [typeof(string)], services, config);

        var provider = services.BuildServiceProvider();
        provider.GetService<BehaviorMarker>().Should().BeNull();
    }
}
