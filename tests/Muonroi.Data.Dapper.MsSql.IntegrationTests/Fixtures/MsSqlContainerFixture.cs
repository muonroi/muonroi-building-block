using System.IO;

namespace Muonroi.Data.Dapper.MsSql.IntegrationTests.Fixtures;

/// <summary>
/// xUnit async fixture that boots a real SQL Server 2022 CU14 container via Testcontainers,
/// creates an <c>rls_items</c> table, applies the shipped 0002 MSSQL RLS migration,
/// seeds two tenants' rows, and exposes the connection string for isolation assertions.
/// </summary>
/// <remarks>
/// SQL Server RLS applies to ALL users including <c>sa</c>/<c>dbo</c> (per Microsoft docs:
/// "Security policies apply to all users, including dbo users in the database").
/// No separate app-role user is required for MSSQL — unlike PostgreSQL where
/// FORCE ROW LEVEL SECURITY only binds for non-owner roles.
/// </remarks>
public sealed class MsSqlContainerFixture : IAsyncLifetime
{
    // ACCEPT_EULA is auto-injected by MsSqlBuilder — no manual env var needed.
    // SQL Server 2022 CU14 sidesteps the SQL Server 2019 SESSION_CONTEXT parallel-plan bug
    // (RESEARCH.md Pitfall 4 / Pitfall 6).
    private readonly MsSqlContainer _sql = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04")
        .Build();

    /// <summary>Connection string (sa/dbo) used by all isolation assertions.</summary>
    public string ConnectionString => _sql.GetConnectionString();

    /// <summary>Tenant A identifier; seeded with one <c>rls_items</c> row.</summary>
    public Guid TenantAId { get; private set; }

    /// <summary>Tenant B identifier; seeded with one <c>rls_items</c> row.</summary>
    public Guid TenantBId { get; private set; }

    public async Task InitializeAsync()
    {
        await _sql.StartAsync().ConfigureAwait(false);

        TenantAId = Guid.NewGuid();
        TenantBId = Guid.NewGuid();

        await using SqlConnection conn = new(_sql.GetConnectionString());
        await conn.OpenAsync().ConfigureAwait(false);

        // 1. Create the rls_items table FIRST — the 0002 migration cursor discovers tables
        //    with a tenant_id column via sys.tables/sys.columns; the table must exist before
        //    the migration runs so the SECURITY POLICY is applied to it (CR-01 / TST-01..05).
        await ExecuteSqlAsync(conn, @"
            CREATE TABLE rls_items (
                id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                tenant_id UNIQUEIDENTIFIER NOT NULL,
                name      NVARCHAR(200)    NOT NULL
            );").ConfigureAwait(false);

        // 2. Seed rows BEFORE applying the migration. SQL Server RLS BLOCK predicates apply to
        //    ALL users including sa/dbo, so seeding AFTER the policy is active would fail
        //    (BLOCK AFTER INSERT would fire for uncontextualised sa connection).
        //    Seeding before the migration ensures the rows exist, and the FILTER predicate
        //    (created by the migration in step 3) will govern which rows each tenant can read.
        await using (SqlCommand seedA = conn.CreateCommand())
        {
            seedA.CommandText = "INSERT INTO rls_items (tenant_id, name) VALUES (@t, 'item-a')";
            SqlParameter pA = seedA.CreateParameter();
            pA.ParameterName = "@t";
            pA.Value = TenantAId;
            seedA.Parameters.Add(pA);
            await seedA.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (SqlCommand seedB = conn.CreateCommand())
        {
            seedB.CommandText = "INSERT INTO rls_items (tenant_id, name) VALUES (@t, 'item-b')";
            SqlParameter pB = seedB.CreateParameter();
            pB.ParameterName = "@t";
            pB.Value = TenantBId;
            seedB.Parameters.Add(pB);
            await seedB.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // 3. Apply the 0002 MSSQL RLS migration LAST — after table creation and seeding.
        //    The file uses GO batch separators which SqlClient cannot execute in a single batch;
        //    split on GO and run each batch separately. Empty batches are skipped.
        string migrationSql = ReadMigration("0002_sqlserver_tenant_rls.sql");
        string[] batches = SplitOnGo(migrationSql);
        foreach (string batch in batches)
        {
            string trimmed = batch.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                await ExecuteSqlAsync(conn, trimmed).ConfigureAwait(false);
        }

        await conn.CloseAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _sql.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task ExecuteSqlAsync(SqlConnection conn, string sql)
    {
        await using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static string ReadMigration(string fileName)
    {
        // From the test binary output (bin/Debug/net8.0) walk up to the muonroi-building-block
        // root and into db/migrations:
        //   net8.0 -> Debug -> bin -> <project> -> tests -> muonroi-building-block
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "db", "migrations", fileName));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Migration file not found at resolved path: {path}", path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Splits a T-SQL script on GO batch terminators.
    /// GO must appear on its own line (case-insensitive). This mirrors how SSMS and sqlcmd
    /// process batch separators — SqlClient cannot execute multi-batch scripts directly.
    /// </summary>
    private static string[] SplitOnGo(string sql)
    {
        // Split on lines that are exactly "GO" (trimmed, case-insensitive).
        // Use Environment.NewLine and \n to handle both CRLF and LF files.
        string normalized = sql.Replace("\r\n", "\n");
        return System.Text.RegularExpressions.Regex.Split(
            normalized,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
