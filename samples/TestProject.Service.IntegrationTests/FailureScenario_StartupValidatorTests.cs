namespace TestProject.Service.IntegrationTests;

/// <summary>
/// FAIL-02: Verifies that SiteProfileStartupValidator throws MInternalException at startup
/// when a keyed service is missing for a registered site — specifically when BRAVO's IOrderService
/// keyed registration is deliberately omitted.
///
/// Design note: Real ISiteProfile implementations auto-register their keyed services inside
/// RegisterServices(). To test a "missing" keyed service, this suite registers FakeSiteProfile
/// instances directly (their RegisterServices is a no-op) so keyed service registration is
/// fully controlled by the test.
///
/// Note on static tracker: SiteProfileExtensions.s_tracker is static. Each AddSiteProfile() /
/// AddSiteResolvedService() call appends to the shared tracker, which accumulates across tests
/// in the same process. Tests assert using Contains checks (not exact counts) to remain resilient.
/// </summary>
public sealed class FailureScenario_StartupValidatorTests
{
    /// <summary>
    /// Builds a ServiceProvider using FakeSiteProfile for all 3 sites but registers only
    /// DEFAULT and ALPHA keyed IOrderService implementations — deliberately omitting BRAVO.
    /// </summary>
    private static ServiceProvider BuildProviderWithMissingBravoService()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Register 3 fake site profiles via AddSiteProfile (no-op RegisterServices, controlled keyed services below)
        services.AddSiteProfile(new FakeSiteProfile(SiteIds.DEFAULT), configuration);
        services.AddSiteProfile(new FakeSiteProfile(SiteIds.ALPHA), configuration);
        services.AddSiteProfile(new FakeSiteProfile(SiteIds.BRAVO), configuration);

        // Register ISiteProfileResolver pointing to DEFAULT
        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(SiteIds.DEFAULT));

        // Track IOrderService as a site-resolved service type
        services.AddSiteResolvedService<IOrderService>();

        // Deliberately register DEFAULT and ALPHA keyed services — BRAVO is intentionally missing
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
        services.AddKeyedScoped<IOrderService, AlphaOrderService>(SiteIds.ALPHA);
        // NOTE: BravoOrderService intentionally NOT registered to trigger validation failure

        // validateScopes: false — startup validator resolves keyed services from root provider
        return services.BuildServiceProvider(validateScopes: false);
    }

    [Fact]
    public async Task StartupValidator_WithMissingBravoService_Throws_MInternalException()
    {
        // Arrange
        await using var provider = BuildProviderWithMissingBravoService();
        var validator = provider.GetRequiredService<IHostedService>();

        // Act & Assert — validator must throw because BRAVO keyed service is missing
        await Assert.ThrowsAsync<MInternalException>(
            () => validator.StartAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ExceptionMessage_Contains_MissingSiteName_BRAVO()
    {
        // Arrange
        await using var provider = BuildProviderWithMissingBravoService();
        var validator = provider.GetRequiredService<IHostedService>();

        // Act
        var ex = await Assert.ThrowsAsync<MInternalException>(
            () => validator.StartAsync(CancellationToken.None));

        // Assert — message names which site has the missing registration
        Assert.Contains(SiteIds.BRAVO, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExceptionMessage_Contains_MissingServiceTypeName()
    {
        // Arrange
        await using var provider = BuildProviderWithMissingBravoService();
        var validator = provider.GetRequiredService<IHostedService>();

        // Act
        var ex = await Assert.ThrowsAsync<MInternalException>(
            () => validator.StartAsync(CancellationToken.None));

        // Assert — message names which service type is missing
        Assert.Contains(nameof(IOrderService), ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExceptionMessage_Contains_StartupValidationFailedMarker()
    {
        // Arrange
        await using var provider = BuildProviderWithMissingBravoService();
        var validator = provider.GetRequiredService<IHostedService>();

        // Act
        var ex = await Assert.ThrowsAsync<MInternalException>(
            () => validator.StartAsync(CancellationToken.None));

        // Assert — message contains the sentinel phrase used by SiteProfileStartupValidator
        Assert.Contains("startup validation FAILED", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
