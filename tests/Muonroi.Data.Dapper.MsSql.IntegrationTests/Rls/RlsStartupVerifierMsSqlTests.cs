namespace Muonroi.Data.Dapper.MsSql.IntegrationTests.Rls;

/// <summary>
/// HARD-01 integration acceptance gate for MSSQL:
/// <list type="bullet">
///   <item>DDL present (0002 applied by <see cref="MsSqlContainerFixture"/>) →
///         <c>RlsStartupVerifier.StartingAsync</c> completes without throwing.</item>
///   <item>DDL dropped (<c>dbo.fn_tenant_access</c> + all <c>*_TenantIsolation</c> policies removed) →
///         <c>StartingAsync</c> throws <see cref="RlsObjectsMissingException"/>.</item>
/// </list>
/// </summary>
/// <remarks>
/// Uses a dedicated container fixture so DDL mutations in the "drop" test do not affect the
/// sibling isolation tests in <see cref="MsSqlRlsIsolationTests"/> which run in a separate
/// container via their own fixture instance.
/// </remarks>
[Collection("MsSqlRlsVerifier")]
public sealed class RlsStartupVerifierMsSqlTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public RlsStartupVerifierMsSqlTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Healthy path: 0002 migration is applied by the fixture.
    /// <c>RlsStartupVerifier.StartingAsync</c> must complete without throwing.
    /// </summary>
    [Fact(DisplayName = "RlsStartupVerifier_WhenDdlPresent_StartingAsync_DoesNotThrow (MsSql)")]
    public async Task WhenDdlPresent_StartingAsync_DoesNotThrow()
    {
        RlsStartupVerifier verifier = BuildVerifier(_fixture.ConnectionString);

        Func<Task> act = () => verifier.StartingAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(
            "all required RLS objects are present after the fixture applies the 0002 migration");
    }

    /// <summary>
    /// Missing-DDL path: drop all <c>*_TenantIsolation</c> security policies and
    /// <c>dbo.fn_tenant_access</c>, then assert <c>StartingAsync</c> throws
    /// <see cref="RlsObjectsMissingException"/>. State is restored in a finally block.
    /// </summary>
    [Fact(DisplayName = "RlsStartupVerifier_WhenDdlDropped_StartingAsync_ThrowsRlsObjectsMissingException (MsSql)")]
    public async Task WhenDdlDropped_StartingAsync_ThrowsRlsObjectsMissingException()
    {
        await DropRlsObjectsAsync(_fixture.ConnectionString);
        try
        {
            RlsStartupVerifier verifier = BuildVerifier(_fixture.ConnectionString);

            Func<Task> act = () => verifier.StartingAsync(CancellationToken.None);

            await act.Should().ThrowAsync<RlsObjectsMissingException>(
                "the catalog query must report unhealthy when fn_tenant_access and TenantIsolation policies are absent");
        }
        finally
        {
            // Restore: re-apply the 0002 migration so subsequent tests in this fixture are not corrupted.
            await RestoreRlsObjectsAsync(_fixture.ConnectionString);
        }
    }

    /// <summary>
    /// Opt-out path: when <c>VerifyRlsObjectsOnStartup = false</c>, <c>StartingAsync</c>
    /// must return immediately without a DB round-trip — even if the DDL is missing.
    /// </summary>
    [Fact(DisplayName = "RlsStartupVerifier_WhenVerifyDisabled_StartingAsync_DoesNotThrow (MsSql)")]
    public async Task WhenVerifyDisabled_StartingAsync_DoesNotThrow()
    {
        await DropRlsObjectsAsync(_fixture.ConnectionString);
        try
        {
            // verify = false → opt-out escape hatch (D-03)
            RlsStartupVerifier verifier = BuildVerifier(_fixture.ConnectionString, verify: false);

            Func<Task> act = () => verifier.StartingAsync(CancellationToken.None);

            await act.Should().NotThrowAsync(
                "VerifyRlsObjectsOnStartup = false must skip the DB check entirely");
        }
        finally
        {
            await RestoreRlsObjectsAsync(_fixture.ConnectionString);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static RlsStartupVerifier BuildVerifier(string connectionString, bool verify = true)
    {
        var connProvider = new FixedConnectionStringProvider(connectionString);
        return new RlsStartupVerifier(
            provider: DapperRlsProvider.MsSql,
            verify: verify,
            connStrings: connProvider,
            log: null);
    }

    private static async Task DropRlsObjectsAsync(string connectionString)
    {
        await using SqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        // Drop all *_TenantIsolation SECURITY POLICies (required before dropping the function
        // due to SCHEMABINDING — same as 0002 Section 2).
        const string dropPoliciesSql = @"
DECLARE @dp_schema sysname, @dp_policy sysname, @dp_sql NVARCHAR(MAX);
DECLARE dp CURSOR FOR
    SELECT SCHEMA_NAME(sp.schema_id), sp.name
    FROM sys.security_policies sp
    WHERE sp.name LIKE N'%_TenantIsolation';
OPEN dp;
FETCH NEXT FROM dp INTO @dp_schema, @dp_policy;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @dp_sql = N'DROP SECURITY POLICY ' + QUOTENAME(@dp_schema) + N'.' + QUOTENAME(@dp_policy) + N';';
    EXEC sp_executesql @dp_sql;
    FETCH NEXT FROM dp INTO @dp_schema, @dp_policy;
END
CLOSE dp;
DEALLOCATE dp;";
        await ExecuteSqlAsync(conn, dropPoliciesSql);

        // Drop dbo.fn_tenant_access after policies are cleared.
        await ExecuteSqlAsync(conn,
            "IF OBJECT_ID('dbo.fn_tenant_access', 'IF') IS NOT NULL DROP FUNCTION dbo.fn_tenant_access;");
    }

    private static async Task RestoreRlsObjectsAsync(string connectionString)
    {
        await using SqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        // Re-apply the 0002 migration batches to restore dbo.fn_tenant_access and policies.
        string migrationSql = ReadMigration("0002_sqlserver_tenant_rls.sql");
        string[] batches = SplitOnGo(migrationSql);
        foreach (string batch in batches)
        {
            string trimmed = batch.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                await ExecuteSqlAsync(conn, trimmed);
        }
    }

    private static async Task ExecuteSqlAsync(SqlConnection conn, string sql)
    {
        await using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string ReadMigration(string fileName)
    {
        string path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "db", "migrations", fileName));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Migration file not found at resolved path: {path}", path);

        return File.ReadAllText(path);
    }

    private static string[] SplitOnGo(string sql)
    {
        string normalized = sql.Replace("\r\n", "\n");
        return System.Text.RegularExpressions.Regex.Split(
            normalized,
            @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
