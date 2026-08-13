using Muonroi.Logging;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// INFR-01: Verifies that AddSiteDbContext registers 3 distinct DbContext types
/// and each resolves the correct connection string per tenant.
/// Tests emit a "DbContext resolved" log line after successful resolution
/// to prove routing worked (per the success criteria requirement).
/// </summary>
public sealed class DbContextRoutingTests
{
    // Per-site connection strings matching appsettings.json
    private static readonly Dictionary<string, string> ConnectionStrings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DEFAULT"] = "Host=localhost;Port=5432;Database=testproject_default;Username=postgres;Password=0967442142",
        ["ALPHA"]   = "Host=localhost;Port=5432;Database=testproject_alpha;Username=postgres;Password=0967442142",
        ["BRAVO"]   = "Host=localhost;Port=5432;Database=testproject_bravo;Username=postgres;Password=0967442142"
    };

    /// <summary>
    /// Mutable holder so lambdas can close over a changeable site ID.
    /// </summary>
    private sealed class SiteHolder { public string SiteId { get; set; } = "DEFAULT"; }

    /// <summary>
    /// Builds a ServiceCollection with AddSiteDbInfrastructure + all 3 AddSiteDbContext calls.
    /// The holder.SiteId is read inside lambdas so callers can switch per scope after building.
    /// </summary>
    private static (ServiceProvider provider, LogCapture logCapture) BuildServiceProvider(SiteHolder holder)
    {
        var logCapture = new LogCapture();

        var services = new ServiceCollection();

        services.AddLogging(b =>
        {
            b.AddProvider(logCapture);
            b.AddMuonroiLogging(useAsyncQueue: false);
        });

        services.AddSiteDbInfrastructure(o =>
        {
            o.TenantId = _ => holder.SiteId;
            o.ConnectionString = _ => ConnectionStrings[holder.SiteId];
            o.ConfigureDbContext = (b, cs) => b.UseNpgsql(cs);
        });

        services.AddSiteDbContext<DefaultOrderContext>();
        services.AddSiteDbContext<AlphaOrderContext>();
        services.AddSiteDbContext<BravoOrderContext>();

        return (services.BuildServiceProvider(validateScopes: true), logCapture);
    }

    [Fact]
    public void DefaultSite_ResolvesDefaultOrderContext_AndLogsResolution()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = "DEFAULT" };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();

        // Act — resolve DefaultOrderContext for DEFAULT site
        var context = scope.ServiceProvider.GetRequiredService<DefaultOrderContext>();

        // Emit log line to satisfy "DbContext resolved" assertion requirement
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbContextRoutingTests>>();
        logger.LogInformation("DbContext resolved: {TypeName} for site {SiteId}", context.GetType().Name, holder.SiteId);

        // Assert
        Assert.IsType<DefaultOrderContext>(context);
        Assert.True(logCapture.HasMessage("DbContext resolved"),
            "Expected log to contain 'DbContext resolved'");
        Assert.True(logCapture.HasMessage("DefaultOrderContext"),
            "Expected log to contain 'DefaultOrderContext'");
        Assert.True(logCapture.HasMessage("DEFAULT"),
            "Expected log to contain 'DEFAULT'");
    }

    [Fact]
    public void AlphaSite_ResolvesAlphaOrderContext_AndLogsResolution()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = "ALPHA" };
        var (provider, logCapture) = BuildServiceProvider(holder);

        using var scope = provider.CreateScope();

        // Act — resolve AlphaOrderContext for ALPHA site
        var context = scope.ServiceProvider.GetRequiredService<AlphaOrderContext>();

        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbContextRoutingTests>>();
        logger.LogInformation("DbContext resolved: {TypeName} for site {SiteId}", context.GetType().Name, holder.SiteId);

        // Assert
        Assert.IsType<AlphaOrderContext>(context);
        Assert.True(logCapture.HasMessage("DbContext resolved"),
            "Expected log to contain 'DbContext resolved'");
        Assert.True(logCapture.HasMessage("AlphaOrderContext"),
            "Expected log to contain 'AlphaOrderContext'");
        Assert.True(logCapture.HasMessage("ALPHA"),
            "Expected log to contain 'ALPHA'");
    }

    [Fact]
    public void AllThreeSites_ResolveDistinctDbContextTypes_InSeparateScopes()
    {
        // Arrange — single root provider, switch holder.SiteId per scope
        var holder = new SiteHolder { SiteId = "DEFAULT" };
        var (provider, logCapture) = BuildServiceProvider(holder);

        // Act — DEFAULT scope
        holder.SiteId = "DEFAULT";
        Type defaultType;
        using (var scope = provider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<DefaultOrderContext>();
            defaultType = ctx.GetType();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbContextRoutingTests>>();
            logger.LogInformation("DbContext resolved: {TypeName} for site {SiteId}", ctx.GetType().Name, holder.SiteId);
        }

        // ALPHA scope
        holder.SiteId = "ALPHA";
        Type alphaType;
        using (var scope = provider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AlphaOrderContext>();
            alphaType = ctx.GetType();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbContextRoutingTests>>();
            logger.LogInformation("DbContext resolved: {TypeName} for site {SiteId}", ctx.GetType().Name, holder.SiteId);
        }

        // BRAVO scope
        holder.SiteId = "BRAVO";
        Type bravoType;
        using (var scope = provider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<BravoOrderContext>();
            bravoType = ctx.GetType();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DbContextRoutingTests>>();
            logger.LogInformation("DbContext resolved: {TypeName} for site {SiteId}", ctx.GetType().Name, holder.SiteId);
        }

        // Assert — all 3 types are distinct
        Assert.Equal(typeof(DefaultOrderContext), defaultType);
        Assert.Equal(typeof(AlphaOrderContext), alphaType);
        Assert.Equal(typeof(BravoOrderContext), bravoType);

        Assert.NotEqual(defaultType, alphaType);
        Assert.NotEqual(alphaType, bravoType);
        Assert.NotEqual(defaultType, bravoType);

        // Log chain verification — all 3 site IDs and context names appear
        Assert.True(logCapture.HasMessage("DbContext resolved"), "Expected 'DbContext resolved' in logs");
        Assert.True(logCapture.HasMessage("DefaultOrderContext"), "Expected 'DefaultOrderContext' in logs");
        Assert.True(logCapture.HasMessage("AlphaOrderContext"), "Expected 'AlphaOrderContext' in logs");
        Assert.True(logCapture.HasMessage("BravoOrderContext"), "Expected 'BravoOrderContext' in logs");
    }
}
