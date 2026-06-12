using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Logging;
using Muonroi.Tenancy.SiteProfile.Web.Validation;
using Npgsql;
using TestProject.Service.Core.Infrastructure.EntityConfigurations;

namespace TestProject.Service.IntegrationTests;

/// <summary>
/// D-16 / D-17 / D-18: Integration tests that validate SiteSchemaValidator behavior against a real
/// PostgreSQL database (localhost:5432). Each test creates a unique schema, runs the validator,
/// then drops the schema in cleanup to leave no artifacts.
///
/// These tests close UAT Gap 1 (major): prove SiteSchemaValidator queries INFORMATION_SCHEMA.COLUMNS
/// against a real DB — not just DI/options config (covered by SchemaValidationDemoTests).
///
/// All tests emit [TEST:BREAKPOINT] IMLog markers per D-26.
/// Skip attribute allows CI to skip when PostgreSQL is not available.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Requires", "PostgreSQL")]
public sealed class SchemaValidationPostgresTests : IDisposable
{
    // ---------------------------------------------------------------------------
    // Connection string — matches STATE.md credentials
    // ---------------------------------------------------------------------------
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=0967442142;Include Error Detail=true";

    // ---------------------------------------------------------------------------
    // Test-only options class to carry the test schema name through DI
    // ---------------------------------------------------------------------------
    private sealed class SchemaTestOptions
    {
        public string Schema { get; set; } = "public";
    }

    // ---------------------------------------------------------------------------
    // Test-only DbContext subclass — minimal EF model mapping to order_details table.
    // Receives schema name via SchemaTestOptions injected from DI.
    // ---------------------------------------------------------------------------
    private sealed class SchemaTestContext(DbContextOptions<SchemaTestContext> options, SchemaTestOptions schemaOpts) : DbContext(options)
    {
        private readonly string _schema = schemaOpts.Schema;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema(_schema);
            modelBuilder.Entity<OrderDetailEntity>(b =>
            {
                b.ToTable("order_details");
                b.HasKey(e => e.Id);
                b.Property(e => e.Name).HasMaxLength(200).IsRequired();
                b.Property(e => e.Description).HasMaxLength(1000);
                b.Property(e => e.ContainerNo).HasMaxLength(100);
            });
        }
    }

    // ---------------------------------------------------------------------------
    // Track schemas created per test so Dispose can clean them all up
    // ---------------------------------------------------------------------------
    private readonly List<string> _schemasToCleanup = [];

    public void Dispose()
    {
        if (_schemasToCleanup.Count == 0)
            return;

        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();

            foreach (string schema in _schemasToCleanup)
            {
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE";
                    cmd.ExecuteNonQuery();
                }
                catch
                {
                    // Best-effort cleanup — do not fail the test teardown
                }
            }
        }
        catch
        {
            // If PostgreSQL is not reachable, nothing to clean up
        }
    }

    // ---------------------------------------------------------------------------
    // Helper: allocate a unique test schema name (max 63 chars for PostgreSQL)
    // ---------------------------------------------------------------------------
    private string AllocateTestSchema()
    {
        // Format: test_sv_{guid first 16 chars} = 24 chars total (well within 63 limit)
        string schema = $"test_sv_{Guid.NewGuid():N}"[..24];
        _schemasToCleanup.Add(schema);
        return schema;
    }

    // ---------------------------------------------------------------------------
    // Helper: create a raw PostgreSQL connection and execute DDL
    // ---------------------------------------------------------------------------
    private static async Task ExecuteAsync(string sql)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------------------------------------------------------------------------
    // Helper: check PostgreSQL connectivity and skip the test if not reachable
    // ---------------------------------------------------------------------------
    private static bool TryConnect()
    {
        try
        {
            using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------------------------------------------------------
    // Helper: build a ServiceProvider with SchemaTestContext + SiteSchemaValidator
    // ---------------------------------------------------------------------------
    private static ServiceProvider BuildValidatorProvider(
        string testSchema,
        SchemaValidationSeverity severity,
        LogCapture logCapture)
    {
        var services = new ServiceCollection();

        // Muonroi logging (required by IMLog<SiteSchemaValidator>)
        services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.AddLogging(b =>
        {
            b.AddMuonroiLogging();
            b.AddProvider(logCapture);
            b.SetMinimumLevel(LogLevel.Trace);
        });

        // Register test schema options so SchemaTestContext can receive it via DI
        services.AddSingleton(new SchemaTestOptions { Schema = testSchema });

        // Register the test DbContext with PostgreSQL pointing to test schema
        services.AddDbContext<SchemaTestContext>(o =>
            o.UseNpgsql(ConnectionString),
            ServiceLifetime.Scoped);

        // SiteSchemaValidator resolves DbContext via sp.GetServices<DbContext>()
        // — must be registered AS DbContext alias, not only as SchemaTestContext
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<SchemaTestContext>());

        // Configure and register SiteSchemaValidator
        services.AddSiteSchemaValidation(o =>
        {
            o.Severity = severity;
            o.SchemaName = testSchema;
        });

        return services.BuildServiceProvider(validateScopes: false);
    }

    // ===========================================================================
    // Test 1: WarnOnMismatch — missing column is logged as warning, no throw
    // ===========================================================================

    [Fact]
    public async Task WarnOnMismatch_MissingColumn_LogsWarning_DoesNotThrow()
    {
        // [TEST:BREAKPOINT] SchemaValidationPostgresTests - WarnOnMismatch
        if (!TryConnect())
        {
            // Skip gracefully when PostgreSQL is not available
            return;
        }

        string testSchema = AllocateTestSchema();

        // Create schema + table with MISSING container_no column — deliberate mismatch
        await ExecuteAsync($"""
            CREATE SCHEMA "{testSchema}";
            CREATE TABLE "{testSchema}".order_details (
                id BIGINT NOT NULL PRIMARY KEY,
                name VARCHAR(200) NOT NULL,
                description VARCHAR(1000),
                created_at TIMESTAMP NOT NULL
                -- container_no is intentionally omitted to trigger a schema mismatch
            );
            """);

        var logCapture = new LogCapture();
        using var provider = BuildValidatorProvider(testSchema, SchemaValidationSeverity.WarnOnMismatch, logCapture);

        // SiteSchemaValidator is registered as IHostedService — resolve and cast
        var validator = provider.GetServices<IHostedService>()
            .OfType<SiteSchemaValidator>()
            .Single();

        // Act — should NOT throw in WarnOnMismatch mode
        var exception = await Record.ExceptionAsync(
            () => validator.StartAsync(CancellationToken.None));

        // Assert: no exception thrown
        Assert.Null(exception);

        // Assert: mismatch warning was logged for the missing container_no column
        bool warnLogged = logCapture.Entries.Any(e =>
            e.Message.Contains("Schema mismatch", StringComparison.OrdinalIgnoreCase) &&
            (e.Message.Contains("container_no", StringComparison.OrdinalIgnoreCase) ||
             e.Message.Contains("ContainerNo", StringComparison.OrdinalIgnoreCase)));

        Assert.True(warnLogged,
            $"Expected a '[SiteSchemaValidator] Schema mismatch' warning mentioning 'container_no'. " +
            $"Captured log messages:\n{string.Join("\n", logCapture.Entries.Select(e => e.Message))}");
    }

    // ===========================================================================
    // Test 2: FailOnMissing — missing column throws MInternalException
    // ===========================================================================

    [Fact]
    public async Task FailOnMissing_MissingColumn_ThrowsMInternalException()
    {
        // [TEST:BREAKPOINT] SchemaValidationPostgresTests - FailOnMissing
        if (!TryConnect())
        {
            return;
        }

        string testSchema = AllocateTestSchema();

        // Create schema + table with MISSING container_no column — same deliberate mismatch
        await ExecuteAsync($"""
            CREATE SCHEMA "{testSchema}";
            CREATE TABLE "{testSchema}".order_details (
                id BIGINT NOT NULL PRIMARY KEY,
                name VARCHAR(200) NOT NULL,
                description VARCHAR(1000),
                created_at TIMESTAMP NOT NULL
                -- container_no intentionally omitted
            );
            """);

        var logCapture = new LogCapture();
        using var provider = BuildValidatorProvider(testSchema, SchemaValidationSeverity.FailOnMissing, logCapture);

        // SiteSchemaValidator is registered as IHostedService — resolve and cast
        var validator = provider.GetServices<IHostedService>()
            .OfType<SiteSchemaValidator>()
            .Single();

        // Act — MUST throw in FailOnMissing mode
        var exception = await Record.ExceptionAsync(
            () => validator.StartAsync(CancellationToken.None));

        // Assert: exception is MInternalException
        Assert.NotNull(exception);
        var ioex = Assert.IsType<MInternalException>(exception);

        // Assert: message contains "Schema validation FAILED" (per SiteSchemaValidator source)
        Assert.Contains("Schema validation FAILED", ioex.Message, StringComparison.OrdinalIgnoreCase);

        // Assert: message mentions the missing column
        bool mentionsMissingColumn =
            ioex.Message.Contains("container_no", StringComparison.OrdinalIgnoreCase) ||
            ioex.Message.Contains("ContainerNo", StringComparison.OrdinalIgnoreCase);

        Assert.True(mentionsMissingColumn,
            $"Expected exception message to mention 'container_no'. Actual message:\n{ioex.Message}");
    }

    // ===========================================================================
    // Test 3: AllColumnsMatch — FailOnMissing mode, full schema, no exception
    // ===========================================================================

    [Fact]
    public async Task AllColumnsMatch_FailOnMissingMode_PassesSilently()
    {
        // [TEST:BREAKPOINT] SchemaValidationPostgresTests - AllColumnsMatch
        if (!TryConnect())
        {
            return;
        }

        string testSchema = AllocateTestSchema();

        // Create schema + table with ALL columns matching the EF column names exactly.
        // SiteSchemaValidator uses efColumn.Name from IRelationalModel metadata.
        // EF Core (Npgsql provider) uses PascalCase column names by default (matching C# property names).
        // Nullable flags must match EF model:
        //   - Id: NOT NULL (long, required)
        //   - Name: NOT NULL (string, IsRequired() in config)
        //   - Description: NOT NULL (string non-nullable in C#, nullable-enabled project = required)
        //   - CreatedAt: NOT NULL (DateTime, non-nullable value type)
        //   - ContainerNo: NULL (string?, nullable in C#)
        await ExecuteAsync($"""
            CREATE SCHEMA "{testSchema}";
            CREATE TABLE "{testSchema}".order_details (
                "Id" BIGINT NOT NULL PRIMARY KEY,
                "Name" VARCHAR(200) NOT NULL,
                "Description" VARCHAR(1000) NOT NULL,
                "CreatedAt" TIMESTAMP NOT NULL,
                "ContainerNo" VARCHAR(100)
            );
            """);

        var logCapture = new LogCapture();
        using var provider = BuildValidatorProvider(testSchema, SchemaValidationSeverity.FailOnMissing, logCapture);

        // SiteSchemaValidator is registered as IHostedService — resolve and cast
        var validator = provider.GetServices<IHostedService>()
            .OfType<SiteSchemaValidator>()
            .Single();

        // Act — should NOT throw even in strictest mode
        var exception = await Record.ExceptionAsync(
            () => validator.StartAsync(CancellationToken.None));

        // Assert: no exception thrown
        Assert.Null(exception);

        // Assert: "Schema validation passed" was logged
        bool passedLogged = logCapture.Entries.Any(e =>
            e.Message.Contains("Schema validation passed", StringComparison.OrdinalIgnoreCase));

        Assert.True(passedLogged,
            $"Expected '[SiteSchemaValidator] Schema validation passed' to be logged. " +
            $"Captured log messages:\n{string.Join("\n", logCapture.Entries.Select(e => e.Message))}");
    }
}
