using System.IO;

namespace Muonroi.Data.Dapper.PostgreSql.IntegrationTests.Fixtures;

/// <summary>
/// xUnit async fixture that boots a real PostgreSQL 16 container via Testcontainers, applies the
/// shipped RLS migrations (0001 then 0002), provisions an <c>rls_items</c> table with row-level
/// security, seeds two tenants' rows, and exposes a non-owner <c>app_rls</c> connection string for
/// the isolation assertions.
/// </summary>
/// <remarks>
/// The superuser connection is used only for DDL/seed inside <see cref="InitializeAsync"/>.
/// All isolation tests (TST-01..05) MUST connect as <c>app_rls</c> (a non-owner, non-BYPASSRLS
/// role) so that FORCE ROW LEVEL SECURITY and the tenant_isolation policy actually bind
/// (RESEARCH.md Pitfall 4).
/// </remarks>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private const string AppRolePassword = "change_me_in_deployment";

    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    /// <summary>Superuser connection string — used only for DDL and seeding in <see cref="InitializeAsync"/>.</summary>
    public string SuperuserConnectionString => _pg.GetConnectionString();

    /// <summary>Non-owner <c>app_rls</c> connection string used by all isolation assertions.</summary>
    public string AppRoleConnectionString { get; private set; } = string.Empty;

    /// <summary>Tenant A identifier; seeded with one <c>rls_items</c> row.</summary>
    public Guid TenantAId { get; private set; }

    /// <summary>Tenant B identifier; seeded with one <c>rls_items</c> row.</summary>
    public Guid TenantBId { get; private set; }

    /// <summary>
    /// Creates a shared-pool <see cref="NpgsqlDataSource"/> over the <c>app_rls</c> connection
    /// string. Used by TST-04 to prove that returning a physical connection to the pool and
    /// re-acquiring it does not leak the previous tenant's GUC (Npgsql issues DISCARD ALL).
    /// The caller owns and must dispose the returned data source.
    /// </summary>
    public NpgsqlDataSource CreateAppRoleDataSource() => NpgsqlDataSource.Create(AppRoleConnectionString);

    public async Task InitializeAsync()
    {
        await _pg.StartAsync().ConfigureAwait(false);

        TenantAId = Guid.NewGuid();
        TenantBId = Guid.NewGuid();

        await using NpgsqlConnection superuser = new(SuperuserConnectionString);
        await superuser.OpenAsync().ConfigureAwait(false);

        // 1. Apply shipped migrations in order: 0001 (ENABLE/FORCE RLS + USING policy),
        //    then 0002 (roles app_rls/app_rls_bypass + WITH CHECK + DML grants).
        await ExecuteSqlAsync(superuser, ReadMigration("0001_enable_rls_postgres.sql")).ConfigureAwait(false);
        await ExecuteSqlAsync(superuser, ReadMigration("0002_postgres_rls_writecheck_roles.sql")).ConfigureAwait(false);

        // 2. Create the rls_items table AFTER the migrations (the 0001/0002 DO-blocks only
        //    cover tables that existed at migration time), then apply RLS + the policy + grants
        //    explicitly so the policy binds for the freshly-created table.
        const string tableDdl = @"
            CREATE TABLE IF NOT EXISTS rls_items (
                id        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
                tenant_id uuid NOT NULL,
                name      text NOT NULL
            );
            ALTER TABLE rls_items ENABLE ROW LEVEL SECURITY;
            ALTER TABLE rls_items FORCE ROW LEVEL SECURITY;
            DROP POLICY IF EXISTS tenant_isolation ON rls_items;
            CREATE POLICY tenant_isolation ON rls_items
                USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            GRANT SELECT, INSERT, UPDATE, DELETE ON rls_items TO app_rls;
            GRANT SELECT, INSERT, UPDATE, DELETE ON rls_items TO app_rls_bypass;";
        await ExecuteSqlAsync(superuser, tableDdl).ConfigureAwait(false);

        // 3. Seed one row per tenant (parameterized — no string interpolation).
        await using (NpgsqlCommand seed = superuser.CreateCommand())
        {
            seed.CommandText =
                "INSERT INTO rls_items (tenant_id, name) VALUES (@a, 'item-a'), (@b, 'item-b')";
            seed.Parameters.AddWithValue("@a", TenantAId);
            seed.Parameters.AddWithValue("@b", TenantBId);
            await seed.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        // 4. Build the app_rls connection string from the superuser one, swapping credentials.
        AppRoleConnectionString = BuildAppRoleConnectionString(SuperuserConnectionString);

        await superuser.CloseAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _pg.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task ExecuteSqlAsync(NpgsqlConnection conn, string sql)
    {
        await using NpgsqlCommand cmd = conn.CreateCommand();
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

    private static string BuildAppRoleConnectionString(string superuserConnectionString)
    {
        // Use the builder so we are agnostic to which key Testcontainers emits
        // (Username vs User ID) and so the password is set correctly.
        var builder = new NpgsqlConnectionStringBuilder(superuserConnectionString)
        {
            Username = "app_rls",
            Password = AppRolePassword,
        };
        return builder.ConnectionString;
    }
}
