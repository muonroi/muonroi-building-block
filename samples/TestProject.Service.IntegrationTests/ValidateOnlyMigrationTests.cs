namespace TestProject.Service.IntegrationTests;

/// <summary>
/// INFR-04: Verifies that MigrationStrategy.ValidateOnly throws MInternalException
/// when pending migrations are detected, and logs "Validated" when no pending migrations exist.
///
/// To guarantee a "pending migrations" scenario, we use a test-only DbContext
/// (PendingMigrationContext) that has a defined EF migration class but which has never
/// been applied to the database. ValidateOnly must detect this and throw.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Requires", "PostgreSQL")]
public sealed class ValidateOnlyMigrationTests
{
    private const string DefaultConnStr =
        "Host=localhost;Port=5432;Database=testproject_default;Username=postgres;Password=0967442142";

    // -----------------------------------------------------------------------
    // Test-only DbContext + Migration to guarantee pending migrations
    // -----------------------------------------------------------------------

    /// <summary>
    /// Minimal test DbContext that has exactly one EF migration defined (InitialCreate_Test).
    /// Since that migration is never applied to any database, ValidateOnly will always report it
    /// as pending and throw MInternalException.
    /// </summary>
    [DbContext(typeof(PendingMigrationContext))]
    private sealed class PendingMigrationContext(DbContextOptions<PendingMigrationContext> options) : DbContext(options)
    {
    }

    /// <summary>
    /// A fake EF migration class that is recognized by EF's migration history infrastructure.
    /// The migration ID prefix (numbers) must sort chronologically. This migration
    /// intentionally uses a unique ID that will never appear in __EFMigrationsHistory.
    /// Up/Down are no-ops — we only care that EF reports it as pending.
    /// </summary>
    [DbContext(typeof(PendingMigrationContext))]
    [Migration("20990101000000_NeverApplied")]
    private sealed class NeverApplied_Migration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — we only need EF to DETECT this as pending, not apply it.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static ServiceProvider BuildProvider(
        LogCapture logCapture,
        MigrationStrategy strategy,
        IReadOnlyList<Type> contextTypes)
    {
        var services = new ServiceCollection();

        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

        services.AddLogging(b =>
        {
            b.AddProvider(logCapture);
            b.AddMuonroiLogging(useAsyncQueue: false);
        });

        services.AddSiteDbInfrastructure(o =>
        {
            o.TenantId = _ => "DEFAULT";
            o.ConnectionString = _ => DefaultConnStr;
            o.ConfigureDbContext = (b, cs) => b.UseNpgsql(cs);
        });

        // Register PendingMigrationContext if needed
        if (contextTypes.Contains(typeof(PendingMigrationContext)))
        {
            services.AddScoped(sp =>
            {
                var builder = new DbContextOptionsBuilder<PendingMigrationContext>();
                builder.UseNpgsql(DefaultConnStr);
                return builder.Options;
            });

            services.AddScoped(sp =>
            {
                var opts = sp.GetRequiredService<DbContextOptions<PendingMigrationContext>>();
                return new PendingMigrationContext(opts);
            });
        }

        // Register real DbContexts if needed
        if (contextTypes.Contains(typeof(DefaultOrderContext)))
            services.AddSiteDbContext<DefaultOrderContext>();
        if (contextTypes.Contains(typeof(AlphaOrderContext)))
            services.AddSiteDbContext<AlphaOrderContext>();
        if (contextTypes.Contains(typeof(BravoOrderContext)))
            services.AddSiteDbContext<BravoOrderContext>();

        services.AddSiteMigrationRunner(o =>
        {
            o.Strategy = strategy;
            o.MaxParallelism = 1;
            o.DbContextTypeProvider = () => contextTypes;
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ValidateOnly_WithPendingMigration_ThrowsMInternalException()
    {
        // Arrange — PendingMigrationContext has migration "20990101000000_NeverApplied" which
        // will never be in __EFMigrationsHistory, so GetPendingMigrationsAsync returns it as pending.
        var logCapture = new LogCapture();
        await using var provider = BuildProvider(
            logCapture,
            MigrationStrategy.ValidateOnly,
            contextTypes: [typeof(PendingMigrationContext)]);

        var hostedService = provider.GetRequiredService<IHostedService>();

        // Act + Assert — ValidateOnly must throw MInternalException
        var ex = await Assert.ThrowsAsync<MInternalException>(
            () => hostedService.StartAsync(CancellationToken.None));

        // Assert message content
        Assert.Contains("Pending migrations detected", ex.Message,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("PendingMigrationContext", ex.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateOnly_WithPendingMigration_ExceptionMessageIncludesMigrationName()
    {
        // Arrange
        var logCapture = new LogCapture();
        await using var provider = BuildProvider(
            logCapture,
            MigrationStrategy.ValidateOnly,
            contextTypes: [typeof(PendingMigrationContext)]);

        var hostedService = provider.GetRequiredService<IHostedService>();

        // Act + Assert
        var ex = await Assert.ThrowsAsync<MInternalException>(
            () => hostedService.StartAsync(CancellationToken.None));

        // The exception message should include the migration name or count
        Assert.True(
            ex.Message.Contains("NeverApplied", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("1 pending migration", StringComparison.OrdinalIgnoreCase),
            $"Expected exception message to reference migration name or count. Actual:\n{ex.Message}");
    }

    [Fact]
    public async Task ValidateOnly_NoContexts_DoesNotThrow()
    {
        // Arrange — no context types → "No site DbContext types discovered" log, no exception
        var logCapture = new LogCapture();
        await using var provider = BuildProvider(
            logCapture,
            MigrationStrategy.ValidateOnly,
            contextTypes: []);

        var hostedService = provider.GetRequiredService<IHostedService>();

        // Act — should NOT throw (runner exits early when no types are discovered)
        var ex = await Record.ExceptionAsync(() => hostedService.StartAsync(CancellationToken.None));

        Assert.Null(ex);
        Assert.True(
            logCapture.HasMessage("No site DbContext types discovered"),
            $"Expected 'No site DbContext types discovered' log. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));
    }

    [Fact]
    public async Task ValidateOnly_AfterAutoMigrate_LogsValidatedForEachType()
    {
        // Arrange — run AutoMigrate first to ensure real DBs have no pending migrations,
        // then run ValidateOnly on the same real contexts — should NOT throw and should log "Validated".
        var setupCapture = new LogCapture();
        await using (var setupProvider = BuildProvider(
            setupCapture,
            MigrationStrategy.AutoMigrate,
            contextTypes: [typeof(DefaultOrderContext), typeof(AlphaOrderContext), typeof(BravoOrderContext)]))
        {
            var setupService = setupProvider.GetRequiredService<IHostedService>();
            await setupService.StartAsync(CancellationToken.None);
        }

        // Now run ValidateOnly — no pending migrations on already-migrated DBs
        var logCapture = new LogCapture();
        await using var provider = BuildProvider(
            logCapture,
            MigrationStrategy.ValidateOnly,
            contextTypes: [typeof(DefaultOrderContext), typeof(AlphaOrderContext), typeof(BravoOrderContext)]);

        var hostedService = provider.GetRequiredService<IHostedService>();

        // Act — should NOT throw
        var ex = await Record.ExceptionAsync(() => hostedService.StartAsync(CancellationToken.None));

        Assert.Null(ex);

        // Assert — "Validated {TypeName}" for each context
        Assert.True(
            logCapture.HasMessage("Validated DefaultOrderContext"),
            $"Expected 'Validated DefaultOrderContext'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));

        Assert.True(
            logCapture.HasMessage("Validated AlphaOrderContext"),
            $"Expected 'Validated AlphaOrderContext'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));

        Assert.True(
            logCapture.HasMessage("Validated BravoOrderContext"),
            $"Expected 'Validated BravoOrderContext'. Actual:\n" +
            string.Join("\n", logCapture.Entries.Select(e => e.Message)));
    }
}
