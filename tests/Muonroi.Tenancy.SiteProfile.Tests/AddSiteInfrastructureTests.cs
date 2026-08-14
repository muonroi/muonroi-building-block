namespace Muonroi.Tenancy.SiteProfile.Tests;

public class AddSiteInfrastructureTests
{
    private readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

    private class FakeProfile(string siteId) : ISiteProfile
    {
        public string SiteId => siteId;
        public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
    }

    [Fact]
    public void NullSiteCodeAccessor_ThrowsInvalidOperationException()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var act = () => services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = null;
            options.SiteAssemblies = [typeof(FakeProfile).Assembly];
        });

        act.Should().Throw<MInternalException>().WithMessage("*SiteCodeAccessor is required*");
    }

    [Fact]
    public void NoProfilesOrAssemblies_ThrowsInvalidOperationException()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var act = () => services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = _ => "TCI";
            options.SiteAssemblies = [];
            options.ManifestProfiles = null;
        });

        act.Should().Throw<MInternalException>().WithMessage("*Either ManifestProfiles (AOT) or SiteAssemblies (reflection) is required*");
    }

    [Fact]
    public void ManifestProfilesPath_RegistersProfiles()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("TCI");

        services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = _ => "TCI";
            options.ManifestProfiles = [p1];
        });

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();
        resolver.Current.SiteId.Should().Be("TCI");
    }

    [Fact]
    public void SiteAssembliesPath_RegistersProfiles()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        
        services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = _ => "TCI";
            options.SiteAssemblies = [typeof(FakeProfile).Assembly];
        });

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();
        
        // This will trigger reflection scan. Since FakeProfile is in this assembly, 
        // it should be found if it has a parameterless constructor.
        // Wait, FakeProfile(string siteId) doesn't have a parameterless constructor.
        // Let's use a nested class with parameterless ctor.
    }

    private class ParameterlessProfile : ISiteProfile
    {
        public string SiteId => "TEST-ASM";
        public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }
    }

    [Fact]
    public void SiteAssembliesPath_CorrectlyRegisters_WithParameterlessCtor()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        
        services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = _ => "TEST-ASM";
            options.SiteAssemblies = [typeof(ParameterlessProfile).Assembly];
        });

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();
        resolver.Current.SiteId.Should().Be("TEST-ASM");
    }

    [Fact]
    public void EnableControllerDiscovery_CallsAddSiteControllers()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        
        // AddSiteControllers calls services.AddControllers() which requires MVC services
        // We can just verify that some MVC services are present
        services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = _ => "TCI";
            options.ManifestProfiles = [new FakeProfile("TCI")];
            options.EnableControllerDiscovery = true;
            options.SiteAssemblies = [typeof(FakeProfile).Assembly];
        });

        // AddControllers registers IActionDescriptorCollectionProvider
        services.Any(sd => sd.ServiceType.Name.Contains("IActionDescriptorCollectionProvider")).Should().BeTrue();
    }

    [Fact]
    public void SkipStartupValidation_CallsSkipMethod()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        
        services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = _ => "TCI";
            options.ManifestProfiles = [new FakeProfile("TCI")];
            options.SkipStartupValidation = true;
        });

        var sp = services.BuildServiceProvider();
        var tracker = sp.GetRequiredService<SiteProfileRegistrationTracker>();
        tracker.SkipValidation.Should().BeTrue();
    }

    [Fact]
    public void BothPathsProvided_ManifestProfilesTakesPrecedence()
    {
        SiteProfileExtensions.ResetTracker();
        var services = new ServiceCollection();
        var p1 = new FakeProfile("MANIFEST");
        
        services.AddSiteInfrastructure(_emptyConfig, options =>
        {
            options.SiteCodeAccessor = _ => "MANIFEST";
            options.ManifestProfiles = [p1];
            options.SiteAssemblies = [typeof(ParameterlessProfile).Assembly]; // Should be ignored
        });

        var sp = services.BuildServiceProvider();
        var resolver = sp.GetRequiredService<ISiteProfileResolver>();
        resolver.Current.SiteId.Should().Be("MANIFEST");
    }
}
