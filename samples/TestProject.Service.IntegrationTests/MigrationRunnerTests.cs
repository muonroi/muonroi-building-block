using Microsoft.Extensions.Hosting;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using TestProject.Service.Sites.Default;
using TestProject.Service.Sites.Alpha;
using TestProject.Service.Sites.Bravo;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// INFR-03: Verifies SiteMigrationRunner discovers all 3 site DbContext types via
/// DbContextTypeProvider, logs the discovery message, and logs successful migration
/// for each type using AutoMigrate strategy.
///
/// Tests use real PostgreSQL (localhost:5432) — databases testproject_default, testproject_alpha,
/// and testproject_bravo must exist (created by Phase 60 SQL seed).
/// All 3 contexts are pointed at testproject_default for migration testing because the goal is
/// verifying DISCOVERY + LOG OUTPUT, not per-schema correctness.
/// </summary>
public sealed class MigrationRunnerTests
{
    private const string DefaultConnStr = "Data Source=:memory:";

    private static readonly Type[] ThreeContextTypes =
    [
        typeof(DefaultOrderContext),
        typeof(AlphaOrderContext),
        typeof(BravoOrderContext)
    ];

    /// <summary>
    /// Builds a ServiceCollection with all 3 DbContexts + SiteMigrationRunner registered,
    /// using the provided LogCapture. Returns the built ServiceProvider.
    /// </summary>
    private static ServiceProvider BuildProvider(
        LogCapture logCapture,
        MigrationStrategy strategy = MigrationStrategy.AutoMigrate)
    {
        var services = new ServiceCollection();

        // IMLog<T> depends on ISystemExecutionContextAccessor (part of Muonroi.Core.Abstractions)
        // which is not registered by AddMuonroiLogging alone — register directly for tests.
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

        services.AddLogging(b =>
        {
            b.AddProvider(logCapture);
            b.AddMuonroiLogging();
        });

        // Infrastructure: all 3 DbContexts point at SQLite memory for migration tests
        services.AddSiteDbInfrastructure(o =>
        {
            o.TenantId = _ => "DEFAULT";
            o.ConnectionString = _ => DefaultConnStr;
            o.ConfigureDbContext = (b, cs) => 
            {
                var conn = new Microsoft.Data.Sqlite.SqliteConnection(cs);
                conn.Open(); // Keep connection open for memory DB to persist
                b.UseSqlite(conn);
            };
        });

        services.AddSiteDbContext<DefaultOrderContext>();
        services.AddSiteDbContext<AlphaOrderContext>();
        services.AddSiteDbContext<BravoOrderContext>();

        services.AddSiteMigrationRunner(o =>
        {
            o.Strategy = strategy;
            o.MaxParallelism = 1; // sequential for deterministic log ordering in tests
            o.DbContextTypeProvider = () => ThreeContextTypes;
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task StartAsync_Discovers3DbContextTypes_LogsDiscovery()
    {
        // Arrange
        var logCapture = new LogCapture();
        await using var provider = BuildProvider(logCapture);

        var hostedService = provider.GetRequiredService<IHostedService>();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert — log must contain the discovery message with count = 3
        Assert.True(
            logCapture.HasMessage("Discovered 3 site DbContext type(s)"),
            $"Expected log to contain 'Discovered 3 site DbContext type(s)'. Actual log entries:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));
    }

    [Fact]
    public async Task StartAsync_AutoMigrate_LogsMigratedForEachDbContextType()
    {
        // Arrange
        var logCapture = new LogCapture();
        await using var provider = BuildProvider(logCapture, MigrationStrategy.AutoMigrate);

        var hostedService = provider.GetRequiredService<IHostedService>();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert — "Migrated {TypeName} successfully" for each of the 3 context types
        Assert.True(
            logCapture.HasMessage("Migrated DefaultOrderContext successfully"),
            $"Expected 'Migrated DefaultOrderContext successfully'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));

        Assert.True(
            logCapture.HasMessage("Migrated AlphaOrderContext successfully"),
            $"Expected 'Migrated AlphaOrderContext successfully'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));

        Assert.True(
            logCapture.HasMessage("Migrated BravoOrderContext successfully"),
            $"Expected 'Migrated BravoOrderContext successfully'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));
    }

    [Fact]
    public async Task StartAsync_AutoMigrate_LogsCompletionSummary()
    {
        // Arrange
        var logCapture = new LogCapture();
        await using var provider = BuildProvider(logCapture, MigrationStrategy.AutoMigrate);

        var hostedService = provider.GetRequiredService<IHostedService>();

        // Act
        await hostedService.StartAsync(CancellationToken.None);

        // Assert — completion summary: "Completed — AutoMigrate for 3 DbContext(s)"
        Assert.True(
            logCapture.HasMessage("Completed"),
            $"Expected log to contain 'Completed'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));

        Assert.True(
            logCapture.HasMessage("AutoMigrate for 3"),
            $"Expected log to contain 'AutoMigrate for 3'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));
    }
}
