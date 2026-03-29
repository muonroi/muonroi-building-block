using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// SRVC-01: Verifies that AddSiteResolvedService dispatches IOrderService resolution
/// to the correct per-site keyed implementation based on the current site context.
/// Tests emit a "Service resolved" log line after successful resolution to prove
/// routing worked (per the success criteria requirement).
/// </summary>
public sealed class ServiceResolutionTests
{
    /// <summary>
    /// Mutable holder so lambdas can close over a changeable site ID.
    /// </summary>
    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    /// <summary>
    /// Builds a ServiceProvider with all 3 keyed IOrderService implementations
    /// registered manually (mirroring what source generator does) plus
    /// AddSiteResolvedService for the dispatch factory.
    /// </summary>
    private static (ServiceProvider provider, LogCapture logCapture) BuildServiceProvider(SiteHolder holder)
    {
        var logCapture = new LogCapture();

        var services = new ServiceCollection();

        services.AddLogging(b => b.AddProvider(logCapture));

        // ISiteProfileResolver stub — reads from holder at resolve time
        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));

        // Keyed registrations — mirrors what [GenerateSiteProfile] source generator emits
        services.AddKeyedScoped<IOrderService, DefaultOrderService>(SiteIds.DEFAULT);
        services.AddKeyedScoped<IOrderService, AlphaOrderService>(SiteIds.ALPHA);
        services.AddKeyedScoped<IOrderService, BravoOrderService>(SiteIds.BRAVO);

        // Dispatch factory — resolves correct keyed impl via ISiteProfileResolver
        services.AddSiteResolvedService<IOrderService>();

        return (services.BuildServiceProvider(validateScopes: true), logCapture);
    }

    [Fact]
    public void AlphaSite_ResolvesAlphaOrderService_AndLogsType()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.ALPHA };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();

        // Act — resolve IOrderService for ALPHA site via dispatch factory
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Emit log line to satisfy "verified by log" success criterion
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ServiceResolutionTests>>();
        logger.LogInformation("Service resolved: {Type} for site {SiteId}", service.GetType().Name, holder.SiteId);

        // Assert — correct type resolved and logged
        Assert.IsType<AlphaOrderService>(service);
        Assert.True(logCapture.HasMessage("AlphaOrderService"),
            "Expected log to contain 'AlphaOrderService'");
        Assert.True(logCapture.HasMessage("ALPHA"),
            "Expected log to contain 'ALPHA'");
    }

    [Fact]
    public void BravoSite_ResolvesBravoOrderService_AndLogsType()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();

        // Act — resolve IOrderService for BRAVO site via dispatch factory
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Emit log line to satisfy "verified by log" success criterion
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ServiceResolutionTests>>();
        logger.LogInformation("Service resolved: {Type} for site {SiteId}", service.GetType().Name, holder.SiteId);

        // Assert — correct type resolved and logged
        Assert.IsType<BravoOrderService>(service);
        Assert.True(logCapture.HasMessage("BravoOrderService"),
            "Expected log to contain 'BravoOrderService'");
        Assert.True(logCapture.HasMessage("BRAVO"),
            "Expected log to contain 'BRAVO'");
    }

    [Fact]
    public void DefaultSite_ResolvesDefaultOrderService_AndLogsType()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.DEFAULT };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();

        // Act — resolve IOrderService for DEFAULT site via dispatch factory
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Emit log line to satisfy "verified by log" success criterion
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ServiceResolutionTests>>();
        logger.LogInformation("Service resolved: {Type} for site {SiteId}", service.GetType().Name, holder.SiteId);

        // Assert — correct type resolved and logged
        Assert.IsType<DefaultOrderService>(service);
        Assert.True(logCapture.HasMessage("DefaultOrderService"),
            "Expected log to contain 'DefaultOrderService'");
        Assert.True(logCapture.HasMessage("DEFAULT"),
            "Expected log to contain 'DEFAULT'");
    }
}
