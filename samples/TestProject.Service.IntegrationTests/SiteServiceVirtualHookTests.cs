using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Contracts;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// SRVC-02: Verifies that per-site virtual method overrides execute the site-specific
/// implementation, not the base class. All assertions use captured log output
/// to prove which override ran.
/// </summary>
public sealed class SiteServiceVirtualHookTests
{
    /// <summary>
    /// Mutable holder so lambdas can close over a changeable site ID.
    /// </summary>
    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    /// <summary>
    /// Builds a ServiceProvider with all 3 keyed IOrderService implementations
    /// registered manually and AddSiteResolvedService dispatch factory.
    /// Mirrors the setup from ServiceResolutionTests.
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
    public async Task BravoSite_CreateAsync_ExecutesBravoOverride_LogsMarker()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Act — call CreateAsync and capture result
        var result = await service.CreateAsync("test-order", "integration test desc");

        // Emit structured log line with result marker + service type + site
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteServiceVirtualHookTests>>();
        logger.LogInformation(
            "CreateAsync result: {Message} from site {SiteId} via {Type}",
            result.Message, holder.SiteId, service.GetType().Name);

        // Assert — Bravo override ran (not base class), log contains Bravo marker
        Assert.True(logCapture.HasMessage("BRAVO-Created"),
            "Expected log to contain 'BRAVO-Created' from BravoOrderService.CreateAsync override");
        Assert.True(logCapture.HasMessage("BravoOrderService"),
            "Expected log to contain 'BravoOrderService'");
    }

    [Fact]
    public async Task AlphaSite_CreateAsync_ExecutesAlphaOverride_LogsMarker()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.ALPHA };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Act — call CreateAsync and capture result
        var result = await service.CreateAsync("test-order", "integration test desc");

        // Emit structured log line with result marker + service type + site
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteServiceVirtualHookTests>>();
        logger.LogInformation(
            "CreateAsync result: {Message} from site {SiteId} via {Type}",
            result.Message, holder.SiteId, service.GetType().Name);

        // Assert — Alpha override ran (not base class), log contains Alpha marker
        Assert.True(logCapture.HasMessage("ALPHA-Created"),
            "Expected log to contain 'ALPHA-Created' from AlphaOrderService.CreateAsync override");
        Assert.True(logCapture.HasMessage("AlphaOrderService"),
            "Expected log to contain 'AlphaOrderService'");
    }

    [Fact]
    public async Task DefaultSite_CreateAsync_ExecutesBaseImplementation_LogsMarker()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.DEFAULT };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Act — call CreateAsync and capture result
        var result = await service.CreateAsync("test-order", "integration test desc");

        // Emit structured log line with result marker + service type + site
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteServiceVirtualHookTests>>();
        logger.LogInformation(
            "CreateAsync result: {Message} from site {SiteId} via {Type}",
            result.Message, holder.SiteId, service.GetType().Name);

        // Assert — base implementation ran (no override), log contains base marker
        Assert.True(logCapture.HasMessage("Created"),
            "Expected log to contain 'Created' from base OrderServiceBase.CreateAsync");
        Assert.False(logCapture.HasMessage("ALPHA-Created"),
            "Expected base class 'Created' but not 'ALPHA-Created'");
        Assert.False(logCapture.HasMessage("BRAVO-Created"),
            "Expected base class 'Created' but not 'BRAVO-Created'");
        Assert.True(logCapture.HasMessage("DefaultOrderService"),
            "Expected log to contain 'DefaultOrderService'");
    }

    [Fact]
    public async Task BravoSite_GetListAsync_ReturnsBravoOverride_LogsCount()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderService>();

        // Act — call GetListAsync and capture result
        var result = await service.GetListAsync("LINER", 1, 10);

        // Emit structured log line with item count + service type + site
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SiteServiceVirtualHookTests>>();
        logger.LogInformation(
            "GetListAsync result: {Total} items from site {SiteId} via {Type}",
            result.Total, holder.SiteId, service.GetType().Name);

        // Assert — Bravo override returned 2 (base returns 1), log contains "2 items"
        Assert.True(logCapture.HasMessage("2 items"),
            "Expected log to contain '2 items' — Bravo.GetListAsync returns Total=2 vs base Total=1");
        Assert.True(logCapture.HasMessage("BravoOrderService"),
            "Expected log to contain 'BravoOrderService'");
        Assert.Equal(2, result.Total);
    }
}
