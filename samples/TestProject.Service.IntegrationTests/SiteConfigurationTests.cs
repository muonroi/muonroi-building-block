namespace TestProject.Service.IntegrationTests;

/// <summary>
/// INFR-05: Verifies that ISiteConfiguration.GetValue returns distinct per-site values
/// from the "Sites:{SiteId}:*" section of IConfiguration.
/// Tests use full DI pipeline with AddSiteConfiguration and FakeSiteProfileResolver.
/// </summary>
public sealed class SiteConfigurationIntegrationTests
{
    /// <summary>
    /// Builds a ServiceProvider with ISiteConfiguration registered for the given siteId.
    /// Uses in-memory IConfiguration with Sites:ALPHA, Sites:BRAVO, Sites:DEFAULT sections.
    /// </summary>
    private static (ServiceProvider provider, LogCapture logCapture) BuildProvider(string siteId)
    {
        var logCapture = new LogCapture();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sites:ALPHA:FeatureKey"]   = "alpha-feature-value",
                ["Sites:BRAVO:FeatureKey"]   = "bravo-feature-value",
                ["Sites:DEFAULT:FeatureKey"] = "default-feature-value"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<ISiteProfileResolver>(new FakeSiteProfileResolver(siteId));
        services.AddLogging(b => b.AddProvider(logCapture));
        services.AddSiteConfiguration();

        return (services.BuildServiceProvider(), logCapture);
    }

    [Fact]
    public void GetValue_Alpha_ReturnsAlphaValue()
    {
        // Arrange
        var (provider, logCapture) = BuildProvider("ALPHA");
        using var scope = provider.CreateScope();
        var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteConfigurationIntegrationTests>>();

        // Act
        string? value = siteConfig.GetValue<string>("FeatureKey");

        // Log resolved value for chain assertion
        logger.LogInformation("ISiteConfiguration resolved: FeatureKey = {Value} for site ALPHA", value);

        // Assert
        Assert.Equal("alpha-feature-value", value);
        Assert.True(logCapture.HasMessage("alpha-feature-value"),
            "Expected log to contain 'alpha-feature-value'");
    }

    [Fact]
    public void GetValue_Bravo_ReturnsBravoValue()
    {
        // Arrange
        var (provider, logCapture) = BuildProvider("BRAVO");
        using var scope = provider.CreateScope();
        var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteConfigurationIntegrationTests>>();

        // Act
        string? value = siteConfig.GetValue<string>("FeatureKey");

        // Log resolved value
        logger.LogInformation("ISiteConfiguration resolved: FeatureKey = {Value} for site BRAVO", value);

        // Assert
        Assert.Equal("bravo-feature-value", value);
        Assert.True(logCapture.HasMessage("bravo-feature-value"),
            "Expected log to contain 'bravo-feature-value'");
    }

    [Fact]
    public void GetValue_AlphaAndBravo_ReturnDistinctValues()
    {
        // Build ALPHA provider
        var (alphaProvider, alphaLogCapture) = BuildProvider("ALPHA");

        string? alphaValue;
        using (var scope = alphaProvider.CreateScope())
        {
            var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteConfigurationIntegrationTests>>();

            alphaValue = siteConfig.GetValue<string>("FeatureKey");
            logger.LogInformation("ISiteConfiguration resolved: FeatureKey = {Value} for site ALPHA", alphaValue);
        }

        // Build BRAVO provider
        var (bravoProvider, bravoLogCapture) = BuildProvider("BRAVO");

        string? bravoValue;
        using (var scope = bravoProvider.CreateScope())
        {
            var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteConfigurationIntegrationTests>>();

            bravoValue = siteConfig.GetValue<string>("FeatureKey");
            logger.LogInformation("ISiteConfiguration resolved: FeatureKey = {Value} for site BRAVO", bravoValue);
        }

        // Assert values are distinct
        Assert.NotEqual(alphaValue, bravoValue);
        Assert.Equal("alpha-feature-value", alphaValue);
        Assert.Equal("bravo-feature-value", bravoValue);

        // Log chain assertions — each capture contains its respective resolved value
        Assert.True(alphaLogCapture.HasMessage("alpha-feature-value"),
            "Expected alpha log to contain 'alpha-feature-value'");
        Assert.True(bravoLogCapture.HasMessage("bravo-feature-value"),
            "Expected bravo log to contain 'bravo-feature-value'");
    }

    [Fact]
    public void GetValue_WithDefaultValue_ReturnsDefaultWhenKeyMissing()
    {
        // Arrange — ALPHA site, key that does not exist in config
        var (provider, _) = BuildProvider("ALPHA");
        using var scope = provider.CreateScope();
        var siteConfig = scope.ServiceProvider.GetRequiredService<ISiteConfiguration>();

        // Act
        string result = siteConfig.GetValue("NonExistentKey", "fallback-value");

        // Assert
        Assert.Equal("fallback-value", result);
    }
}
