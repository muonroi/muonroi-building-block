namespace TestProject.Service.IntegrationTests;

/// <summary>
/// FAIL-03: Verifies that a broken Bravo DB connection does not cascade to Alpha or Default sites.
///
/// These are NEGATIVE tests — they intentionally break things to verify error handling.
/// Bravo's connection string points to a non-existent database, while Alpha and Default
/// use real PostgreSQL databases. The test suite proves per-site isolation.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Requires", "PostgreSQL")]
public sealed class FailureScenario_DbIsolationTests
{
    // Per-site connection strings:
    // DEFAULT + ALPHA point to real databases (created by Phase 60 scaffold)
    // BRAVO deliberately points to a non-existent database to trigger PostgreSQL error
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=testproject_default;Username=postgres;Password=0967442142";

    private const string AlphaConnectionString =
        "Host=localhost;Port=5432;Database=testproject_alpha;Username=postgres;Password=0967442142";

    private const string BravoBadConnectionString =
        "Host=localhost;Port=5432;Database=nonexistent_bravo_db;Username=postgres;Password=0967442142";

    /// <summary>
    /// Mutable holder so lambdas can close over a changeable site ID.
    /// </summary>
    private sealed class SiteHolder { public string SiteId { get; set; } = "DEFAULT"; }

    /// <summary>
    /// Builds a ServiceCollection with AddSiteDbInfrastructure and all 3 AddSiteDbContext calls.
    /// DEFAULT and ALPHA use real DBs; BRAVO gets a deliberately broken connection string.
    /// </summary>
    private static ServiceProvider BuildServiceProvider(SiteHolder holder)
    {
        var connectionStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DEFAULT"] = DefaultConnectionString,
            ["ALPHA"]   = AlphaConnectionString,
            ["BRAVO"]   = BravoBadConnectionString
        };

        var services = new ServiceCollection();

        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddSiteDbInfrastructure(o =>
        {
            o.TenantId = _ => holder.SiteId;
            o.ConnectionString = _ => connectionStrings[holder.SiteId];
            o.ConfigureDbContext = (b, cs) => b.UseNpgsql(cs);
        });

        services.AddSiteDbContext<DefaultOrderContext>();
        services.AddSiteDbContext<AlphaOrderContext>();
        services.AddSiteDbContext<BravoOrderContext>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    /// <summary>
    /// FAIL-03 Test 1: Bravo DbContext with a non-existent database throws a PostgreSQL error.
    ///
    /// When CanConnectAsync is called against "nonexistent_bravo_db", Npgsql returns false
    /// (PostgreSQL connection refused / database does not exist). We assert the result is false
    /// (no connection) — this proves the bad connection string is correctly isolated to Bravo.
    /// </summary>
    [Fact]
    public async Task BravoBadDb_CanConnectAsync_ReturnsFalseOrThrowsNpgsqlException()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = "BRAVO" };
        await using var provider = BuildServiceProvider(holder);

        await using var scope = provider.CreateAsyncScope();
        var bravoCtx = scope.ServiceProvider.GetRequiredService<BravoOrderContext>();

        // Act — CanConnectAsync either returns false or throws NpgsqlException for non-existent DB
        bool canConnect;
        try
        {
            canConnect = await bravoCtx.Database.CanConnectAsync();
        }
        catch (NpgsqlException ex)
        {
            // PostgreSQL error for non-existent DB — this is the expected failure path
            Assert.False(false, $"Got expected NpgsqlException for nonexistent_bravo_db: {ex.Message}");
            return;
        }
        catch (Exception ex) when (ex.Message.Contains("nonexistent_bravo_db", StringComparison.OrdinalIgnoreCase)
                                || ex.InnerException is NpgsqlException)
        {
            // Other PostgreSQL-related exception wrapping NpgsqlException
            return;
        }

        // If no exception, CanConnectAsync should return false for non-existent DB
        Assert.False(canConnect,
            "Expected CanConnectAsync to return false for non-existent database 'nonexistent_bravo_db'. " +
            "This proves Bravo's bad connection string is isolated to Bravo only.");
    }

    /// <summary>
    /// FAIL-03 Test 2: Alpha DbContext with a valid database can connect successfully.
    ///
    /// Alpha uses testproject_alpha (created by Phase 60 scaffold). CanConnectAsync should
    /// return true. This proves Alpha is unaffected by Bravo's broken connection.
    /// </summary>
    [Fact]
    public async Task AlphaGoodDb_CanConnectAsync_ReturnsTrue()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = "ALPHA" };
        await using var provider = BuildServiceProvider(holder);

        await using var scope = provider.CreateAsyncScope();
        var alphaCtx = scope.ServiceProvider.GetRequiredService<AlphaOrderContext>();

        // Act
        bool canConnect = await alphaCtx.Database.CanConnectAsync();

        // Assert — Alpha's real database should be connectable
        Assert.True(canConnect,
            "Expected CanConnectAsync=true for testproject_alpha. " +
            "Alpha DB must be accessible (created by Phase 60 scaffold).");
    }

    /// <summary>
    /// FAIL-03 Test 3: Default DbContext with a valid database can connect successfully.
    ///
    /// Default uses testproject_default (created by Phase 60 scaffold). CanConnectAsync should
    /// return true. This proves Default is unaffected by Bravo's broken connection.
    /// </summary>
    [Fact]
    public async Task DefaultGoodDb_CanConnectAsync_ReturnsTrue()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = "DEFAULT" };
        await using var provider = BuildServiceProvider(holder);

        await using var scope = provider.CreateAsyncScope();
        var defaultCtx = scope.ServiceProvider.GetRequiredService<DefaultOrderContext>();

        // Act
        bool canConnect = await defaultCtx.Database.CanConnectAsync();

        // Assert — Default's real database should be connectable
        Assert.True(canConnect,
            "Expected CanConnectAsync=true for testproject_default. " +
            "Default DB must be accessible (created by Phase 60 scaffold).");
    }

    /// <summary>
    /// FAIL-03 Test 4 (Concurrent): Bravo (bad DB) + Alpha (good) + Default (good) run concurrently.
    ///
    /// Fire all 3 tasks simultaneously. Bravo fails; Alpha and Default succeed.
    /// No cascade: Alpha and Default are not affected by Bravo's PostgreSQL error.
    ///
    /// Each task uses its own ServiceProvider to isolate the SiteHolder (concurrent-safe).
    /// </summary>
    [Fact]
    public async Task ConcurrentThreeSites_BravoFails_AlphaDefaultSucceed_NoCascade()
    {
        // Build 3 separate providers — one per site — to avoid AsyncLocal contention
        var defaultHolder = new SiteHolder { SiteId = "DEFAULT" };
        var alphaHolder   = new SiteHolder { SiteId = "ALPHA" };
        var bravoHolder   = new SiteHolder { SiteId = "BRAVO" };

        await using var defaultProvider = BuildServiceProvider(defaultHolder);
        await using var alphaProvider   = BuildServiceProvider(alphaHolder);
        await using var bravoProvider   = BuildServiceProvider(bravoHolder);

        bool? defaultResult = null;
        bool? alphaResult   = null;
        bool? bravoResult   = null;
        Exception? bravoException = null;

        // Fire all 3 concurrently
        Task defaultTask = Task.Run(async () =>
        {
            await using var scope = defaultProvider.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<DefaultOrderContext>();
            defaultResult = await ctx.Database.CanConnectAsync();
        });

        Task alphaTask = Task.Run(async () =>
        {
            await using var scope = alphaProvider.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AlphaOrderContext>();
            alphaResult = await ctx.Database.CanConnectAsync();
        });

        Task bravoTask = Task.Run(async () =>
        {
            try
            {
                await using var scope = bravoProvider.CreateAsyncScope();
                var ctx = scope.ServiceProvider.GetRequiredService<BravoOrderContext>();
                bravoResult = await ctx.Database.CanConnectAsync();
            }
            catch (NpgsqlException ex)
            {
                bravoException = ex;
                bravoResult = false;
            }
            catch (Exception ex) when (ex.InnerException is NpgsqlException)
            {
                bravoException = ex.InnerException;
                bravoResult = false;
            }
        });

        await Task.WhenAll(defaultTask, alphaTask, bravoTask);

        // Assert: Alpha and Default succeeded
        Assert.True(defaultResult,
            "DEFAULT must connect successfully (testproject_default). No cascade from Bravo failure.");
        Assert.True(alphaResult,
            "ALPHA must connect successfully (testproject_alpha). No cascade from Bravo failure.");

        // Assert: Bravo failed (either returned false or threw NpgsqlException)
        Assert.True(bravoResult == false || bravoException is not null,
            "BRAVO must fail to connect to nonexistent_bravo_db. " +
            "Expected CanConnectAsync=false or NpgsqlException.");

        // Critical: no cross-site cascade — both good sites succeeded despite Bravo's failure
        Assert.NotEqual(true, bravoResult);
    }
}
