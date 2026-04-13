namespace Muonroi.Tenancy.SiteProfile.Web.Tests;

public class SiteConfigurationTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IConfiguration BuildConfig(Dictionary<string, string?> data)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    private static ISiteProfileResolver MockResolver(string siteId)
    {
        var profile = new Mock<ISiteProfile>();
        profile.Setup(p => p.SiteId).Returns(siteId);
        var resolver = new Mock<ISiteProfileResolver>();
        resolver.Setup(r => r.Current).Returns(profile.Object);
        return resolver.Object;
    }

    // -----------------------------------------------------------------------
    // CONF-01: per-site value scoped to Sites:{SiteId}
    // -----------------------------------------------------------------------

    [Fact]
    public void GetValue_ReturnsValueFromCurrentSiteSection()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Sites:TCI:FeatureKey"] = "tci-value",
            ["Sites:SG01:FeatureKey"] = "sg01-value"
        });

        ISiteConfiguration sut = new SiteConfiguration(MockResolver("TCI"), config);

        Assert.Equal("tci-value", sut.GetValue<string>("FeatureKey"));
    }

    // -----------------------------------------------------------------------
    // CONF-02: no SiteId parameter — different resolver contexts return different values
    // -----------------------------------------------------------------------

    [Fact]
    public void GetValue_TwoDifferentSites_ReturnDistinctValues()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Sites:TCI:FeatureKey"] = "tci-value",
            ["Sites:SG01:FeatureKey"] = "sg01-value"
        });

        ISiteConfiguration tci = new SiteConfiguration(MockResolver("TCI"), config);
        ISiteConfiguration sg01 = new SiteConfiguration(MockResolver("SG01"), config);

        Assert.NotEqual(tci.GetValue<string>("FeatureKey"), sg01.GetValue<string>("FeatureKey"));
        Assert.Equal("tci-value", tci.GetValue<string>("FeatureKey"));
        Assert.Equal("sg01-value", sg01.GetValue<string>("FeatureKey"));
    }

    [Fact]
    public void GetValue_MissingSiteKey_ReturnsDefault()
    {
        var config = BuildConfig(new Dictionary<string, string?> { });
        ISiteConfiguration sut = new SiteConfiguration(MockResolver("TCI"), config);

        Assert.Null(sut.GetValue<string>("Missing"));
        Assert.Equal("fallback", sut.GetValue<string>("Missing", "fallback"));
    }

    // -----------------------------------------------------------------------
    // CONF-03: hot-reload — live config re-read on each call
    // -----------------------------------------------------------------------

    [Fact]
    public void GetValue_AfterConfigUpdate_ReturnsNewValue()
    {
        // Use MemoryConfigurationSource which is mutable
        var source = new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string?>
            {
                ["Sites:TCI:FeatureKey"] = "old-value"
            }
        };
        var config = new ConfigurationBuilder().Add(source).Build();
        ISiteConfiguration sut = new SiteConfiguration(MockResolver("TCI"), config);

        Assert.Equal("old-value", sut.GetValue<string>("FeatureKey"));

        // Simulate hot-reload by updating the in-memory config root directly
        ((IConfigurationRoot)config)["Sites:TCI:FeatureKey"] = "new-value";

        // Implementation must NOT cache — must re-read
        Assert.Equal("new-value", sut.GetValue<string>("FeatureKey"));
    }

    // -----------------------------------------------------------------------
    // DI registration — AddSiteConfiguration() wires scoped ISiteConfiguration
    // -----------------------------------------------------------------------

    [Fact]
    public void AddSiteConfiguration_RegistersIsiteConfigurationAsScoped()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Sites:TCI:Key"] = "val"
        });
        services.AddSingleton<IConfiguration>(config);

        // Provide a mock ISiteProfileResolver
        var resolver = MockResolver("TCI");
        services.AddSingleton(resolver);

        services.AddSiteConfiguration();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();

        Assert.NotNull(siteConfig);
        Assert.Equal("val", siteConfig.GetValue<string>("Key"));
    }
}
