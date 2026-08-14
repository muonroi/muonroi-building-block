namespace TestProject.Service.IntegrationTests;

/// <summary>
/// FAIL-01: Verifies that resolving ISiteProfileResolver for an unknown SiteCode throws
/// MInternalException with a clear, operator-readable message — not a cryptic DI error.
///
/// The "default" fallback in AddMultiSiteProfiles silently resolves when a site named "DEFAULT"
/// or "default" is registered (OrdinalIgnoreCase match). To reliably trigger the not-found path,
/// these tests register only ALPHA and BRAVO — no DEFAULT — so "NONEXISTENT" has no fallback.
///
/// Tests cover: unknown site code error, message content (site name + available list),
/// and absence of internal DI framework details in the error message.
/// </summary>
public sealed class FailureScenario_SiteCodeNotFoundTests
{
    private const string UnknownSiteCode = "NONEXISTENT";

    /// <summary>
    /// Builds a ServiceProvider using AddMultiSiteProfiles with ALPHA and BRAVO assemblies only.
    /// No DEFAULT / "default" profile — so fallback cannot silently resolve the unknown site code.
    /// </summary>
    private static ServiceProvider BuildProvider(string siteCode)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();

        // Register ALPHA and BRAVO only — no DEFAULT so fallback cannot resolve NONEXISTENT
        services.AddMultiSiteProfiles(
            configuration,
            siteCodeAccessor: _ => siteCode,
            assemblies:
            [
                typeof(AlphaSiteProfile).Assembly,
                typeof(BravoSiteProfile).Assembly
            ]);

        // Keyed implementations for the registered profiles
        services.AddKeyedScoped<IOrderService, AlphaOrderService>(SiteIds.ALPHA);
        services.AddKeyedScoped<IOrderService, BravoOrderService>(SiteIds.BRAVO);

        services.AddSiteResolvedService<IOrderService>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void ResolvingUnknownSiteCode_Throws_MInternalException()
    {
        // Arrange
        using var provider = BuildProvider(UnknownSiteCode);
        using var scope = provider.CreateScope();

        // Act & Assert — resolving ISiteProfileResolver with unknown site code must throw
        var ex = Assert.Throws<MInternalException>(
            () => scope.ServiceProvider.GetRequiredService<ISiteProfileResolver>());

        Assert.NotNull(ex);
    }

    [Fact]
    public void ExceptionMessage_Contains_TheUnknownSiteCode()
    {
        // Arrange
        using var provider = BuildProvider(UnknownSiteCode);
        using var scope = provider.CreateScope();

        // Act
        var ex = Assert.Throws<MInternalException>(
            () => scope.ServiceProvider.GetRequiredService<ISiteProfileResolver>());

        // Assert — message must name the unknown code so operators know what to fix
        Assert.Contains(UnknownSiteCode, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExceptionMessage_Contains_AvailableSiteList()
    {
        // Arrange
        using var provider = BuildProvider(UnknownSiteCode);
        using var scope = provider.CreateScope();

        // Act
        var ex = Assert.Throws<MInternalException>(
            () => scope.ServiceProvider.GetRequiredService<ISiteProfileResolver>());

        // Assert — message lists registered site codes (ALPHA + BRAVO in this setup)
        Assert.Contains("Available:", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SiteIds.ALPHA, ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SiteIds.BRAVO, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExceptionMessage_DoesNotExpose_InternalDiFrameworkDetails()
    {
        // Arrange
        using var provider = BuildProvider(UnknownSiteCode);
        using var scope = provider.CreateScope();

        // Act
        var ex = Assert.Throws<MInternalException>(
            () => scope.ServiceProvider.GetRequiredService<ISiteProfileResolver>());

        // Assert — message must NOT leak internal DI namespace details
        Assert.DoesNotContain("Microsoft.Extensions.DependencyInjection", ex.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Autofac", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
