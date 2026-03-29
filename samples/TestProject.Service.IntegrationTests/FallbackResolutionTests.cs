using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-25: Demonstrates fallback resolution — ALPHA has no custom IOrderService override;
/// keyed DI falls back to DEFAULT (the fallback key in AddSiteResolvedService).
/// Verifies via log that DEFAULT service was resolved for ALPHA requests when only DEFAULT is registered.
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// </summary>
public sealed class FallbackResolutionTests
{
    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    /// <summary>
    /// Builds a provider where only DEFAULT ("default" key) is registered for IOrderService.
    /// ALPHA has NO keyed registration → AddSiteResolvedService falls back to "default" key.
    /// Note: AddSiteResolvedService built-in fallback uses "default" (lowercase) key.
    /// </summary>
    private static (ServiceProvider provider, LogCapture logCapture) BuildFallbackProvider(SiteHolder holder)
    {
        var logCapture = new LogCapture();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(logCapture));

        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));

        // Register under both "DEFAULT" and "default" keys so fallback works
        // AddSiteResolvedService first tries exact site key, then "default" (lowercase)
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
        services.AddKeyedScoped<IOrderService, DefaultOrderService>("default");

        // Dispatch factory: ALPHA → tries "ALPHA" (not found) → falls back to "default"
        services.AddSiteResolvedService<IOrderService>();

        return (services.BuildServiceProvider(validateScopes: true), logCapture);
    }

    [Fact]
    public void AlphaSite_NoKeyedService_FallsBackToDefault_AndLogsDefaultType()
    {
        // [TEST:BREAKPOINT] FallbackResolutionTests - Alpha fallback to DEFAULT
        var holder = new SiteHolder { SiteId = SiteIds.ALPHA };
        var (provider, logCapture) = BuildFallbackProvider(holder);

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FallbackResolutionTests>>();
        logger.LogInformation(
            "[TEST:BREAKPOINT] FallbackResolutionTests - Service resolved: {Type} for site {SiteId}",
            service.GetType().Name, holder.SiteId);

        // ALPHA had no keyed service — DefaultOrderService should be resolved as fallback
        Assert.IsType<DefaultOrderService>(service);
        Assert.True(logCapture.HasMessage("DefaultOrderService"),
            "Expected log to contain 'DefaultOrderService' — DEFAULT was used as fallback for ALPHA");
    }

    [Fact]
    public void DefaultSite_Registered_ResolvesNormally_NoFallbackNeeded()
    {
        // [TEST:BREAKPOINT] FallbackResolutionTests - DEFAULT resolves normally
        var holder = new SiteHolder { SiteId = SiteIds.DEFAULT };
        var (provider, logCapture) = BuildFallbackProvider(holder);

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<FallbackResolutionTests>>();
        logger.LogInformation(
            "[TEST:BREAKPOINT] FallbackResolutionTests - Service resolved: {Type} for site {SiteId}",
            service.GetType().Name, holder.SiteId);

        Assert.IsType<DefaultOrderService>(service);
        Assert.True(logCapture.HasMessage("DefaultOrderService"),
            "Expected log to contain 'DefaultOrderService' for DEFAULT site");
    }

    [Fact]
    public void AllThreeSites_WithAllKeyed_EachResolvesCorrectImpl()
    {
        // [TEST:BREAKPOINT] FallbackResolutionTests - Full three-site resolution
        // When all sites have keyed registrations, no fallback needed
        foreach (var (siteId, expectedType) in new[]
        {
            (SiteIds.DEFAULT, typeof(DefaultOrderService)),
            (SiteIds.ALPHA, typeof(AlphaOrderService)),
            (SiteIds.BRAVO, typeof(BravoOrderService))
        })
        {
            var logCapture = new LogCapture();
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddProvider(logCapture));

            var holder = new SiteHolder { SiteId = siteId };
            services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));

            services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
            services.AddKeyedScoped<IOrderService, AlphaOrderService>(SiteIds.ALPHA);
            services.AddKeyedScoped<IOrderService, BravoOrderService>(SiteIds.BRAVO);
            services.AddSiteResolvedService<IOrderService>();

            using var provider = services.BuildServiceProvider(validateScopes: true);
            using var scope = provider.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

            var logger = scope.ServiceProvider.GetRequiredService<ILogger<FallbackResolutionTests>>();
            logger.LogInformation(
                "[TEST:BREAKPOINT] FallbackResolutionTests - Site {SiteId} resolved {Type}",
                siteId, service.GetType().Name);

            Assert.IsType(expectedType, service);
        }
    }
}
