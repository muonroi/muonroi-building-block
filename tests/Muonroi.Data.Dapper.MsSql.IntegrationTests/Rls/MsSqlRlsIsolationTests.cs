namespace Muonroi.Data.Dapper.MsSql.IntegrationTests.Rls;

/// <summary>
/// Phase 3 acceptance gate: the five engine-level RLS isolation proofs (TST-01..TST-05) run
/// against a live Testcontainers SQL Server 2022 CU14 instance via the shared
/// <see cref="MsSqlContainerFixture"/>. SQL Server RLS applies to sa/dbo so no separate
/// app-role user is required (unlike PostgreSQL where FORCE ROW LEVEL SECURITY only binds
/// for non-owner roles).
/// </summary>
[Collection("MsSqlRls")]
public sealed class MsSqlRlsIsolationTests : IClassFixture<MsSqlContainerFixture>
{
    private readonly MsSqlContainerFixture _fixture;

    public MsSqlRlsIsolationTests(MsSqlContainerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>Opens and returns a new SqlConnection on the fixture's connection string (sa).</summary>
    private async Task<SqlConnection> OpenConnectionAsync()
    {
        SqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Sets SESSION_CONTEXT N'TenantId' on the given connection via parameterized
    /// sp_set_session_context call (never string-interpolated — HOOK-04 injection safety).
    /// </summary>
    private static async Task SetSessionContextAsync(SqlConnection conn, Guid tenantId)
    {
        await using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "EXEC sp_set_session_context @key=N'TenantId', @value=@tid";
        SqlParameter p = cmd.CreateParameter();
        p.ParameterName = "@tid";
        p.Value = tenantId; // Guid → sql_variant; SQL Server handles UNIQUEIDENTIFIER comparison
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reads all tenant_id values visible to the current session context from rls_items.
    /// The FILTER predicate silently suppresses rows for which the predicate is not satisfied.
    /// </summary>
    private static async Task<List<Guid>> ReadTenantIdsAsync(SqlConnection conn)
    {
        List<Guid> result = new();
        await using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT tenant_id FROM rls_items";
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetGuid(0));
        }

        return result;
    }

    /// <summary>
    /// TST-01: Tenant A opening a connection with SESSION_CONTEXT N'TenantId' set to TenantAId
    /// reads zero rows where tenant_id = TenantBId. FILTER predicate enforced by SQL Server 2022 engine.
    /// </summary>
    [Fact]
    public async Task Tst01_TenantA_CannotRead_TenantBRows()
    {
        await using SqlConnection conn = await OpenConnectionAsync();
        await SetSessionContextAsync(conn, _fixture.TenantAId);

        List<Guid> tenantIds = await ReadTenantIdsAsync(conn);

        tenantIds.Should().Contain(_fixture.TenantAId, "tenant A must see its own rows");
        tenantIds.Should().NotContain(_fixture.TenantBId, "tenant A must not see tenant B rows");
    }

    /// <summary>
    /// TST-02: Tenant A with SESSION_CONTEXT N'TenantId' set to TenantAId cannot INSERT a row
    /// with tenant_id = TenantBId. BLOCK AFTER INSERT predicate raises SqlException Msg 33504.
    /// </summary>
    [Fact]
    public async Task Tst02_TenantA_CannotInsert_RowAsTenantB()
    {
        await using SqlConnection conn = await OpenConnectionAsync();
        await SetSessionContextAsync(conn, _fixture.TenantAId);

        Func<Task> act = async () =>
        {
            await using SqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO rls_items (tenant_id, name) VALUES (@b, 'cross-tenant')";
            SqlParameter p = cmd.CreateParameter();
            p.ParameterName = "@b";
            p.Value = _fixture.TenantBId;
            cmd.Parameters.Add(p);
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        };

        await act.Should()
            .ThrowAsync<SqlException>()
            .Where(e => e.Number == 33504,
                "BLOCK predicate violation must raise Msg 33504 (has a block predicate that conflicts with this operation)");
    }

    /// <summary>
    /// TST-03: With no SESSION_CONTEXT set at all (unset key returns NULL),
    /// TRY_CAST(NULL AS UNIQUEIDENTIFIER) = NULL → predicate is NULL for every row → zero rows.
    /// Fail-closed: no tenant context means no rows are visible.
    /// Note: do NOT call SetSessionContextAsync — the unset-key NULL behavior is the test intent
    /// (Critical Deviation #5 in PATTERNS.md vs the PG test which sets an empty-string GUC).
    /// </summary>
    [Fact]
    public async Task Tst03_NoContext_ReturnsZeroRows()
    {
        await using SqlConnection conn = await OpenConnectionAsync();
        // Intentionally do NOT call SetSessionContextAsync — SESSION_CONTEXT(N'TenantId')
        // returns NULL for an unset key; TRY_CAST(NULL AS UNIQUEIDENTIFIER) returns NULL;
        // NULL = @tenant_id is NULL (not TRUE) → FILTER predicate blocks all rows.

        await using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM rls_items";
        long count = Convert.ToInt64(await cmd.ExecuteScalarAsync());

        count.Should().Be(0, "an empty tenant context must return zero rows (fail-closed)");
    }

    /// <summary>
    /// TST-04: A physical connection returned to the pool and re-acquired by a new SqlConnection
    /// carries no stale SESSION_CONTEXT. ADO.NET calls sp_reset_connection on pool return which
    /// clears SESSION_CONTEXT (RESEARCH.md Pitfall 6). The setter re-applies on every open.
    /// Uses two sequential SqlConnection opens on the same connection string (same pool key).
    /// </summary>
    [Fact]
    public async Task Tst04_PoolReuse_DoesNotLeak_TenantContext()
    {
        // Connection 1: tenant A. After CloseAsync(), returns to pool → sp_reset_connection clears SESSION_CONTEXT.
        await using (SqlConnection conn1 = new(_fixture.ConnectionString))
        {
            await conn1.OpenAsync();
            await SetSessionContextAsync(conn1, _fixture.TenantAId);

            List<Guid> ids1 = await ReadTenantIdsAsync(conn1);
            ids1.Should().Contain(_fixture.TenantAId, "conn1 should see tenant A rows");
            ids1.Should().NotContain(_fixture.TenantBId, "conn1 must not see tenant B rows");

            await conn1.CloseAsync(); // returns to pool → sp_reset_connection clears context
        }

        // Connection 2: pool manager reuses the physical connection; SESSION_CONTEXT was cleared
        // by sp_reset_connection. Set tenant B context explicitly; verify only tenant B rows visible.
        await using (SqlConnection conn2 = new(_fixture.ConnectionString))
        {
            await conn2.OpenAsync();
            await SetSessionContextAsync(conn2, _fixture.TenantBId);

            List<Guid> ids2 = await ReadTenantIdsAsync(conn2);
            ids2.Should().Contain(_fixture.TenantBId, "conn2 should see tenant B rows");
            ids2.Should().NotContain(_fixture.TenantAId,
                "pool reuse must not leak TenantA context into TenantB connection");
        }
    }

    /// <summary>
    /// TST-05: With DapperRlsBypass.Enter() active, MsSqlTenantSessionContextSetter.ApplyAsync
    /// sets N'TenantBypass'=1 on the live SQL Server; the predicate OR branch fires and SELECT
    /// returns rows for both TenantA and TenantB (bypass scope). After scope disposal,
    /// DapperRlsBypass.IsActive is false.
    /// Proves the full D-04 pipeline end-to-end: Enter() → IsActive=true → setter bypass branch
    /// → N'TenantBypass'=1 → predicate OR branch → all-tenant rows visible.
    /// </summary>
    [Fact]
    public async Task Tst05_BypassScope_ReadsAcrossTenants()
    {
        await using SqlConnection conn = await OpenConnectionAsync();

        // Route through the real C# bypass seam (D-04/D-05): inside a DapperRlsBypass scope
        // the setter detects IsActive and issues sp_set_session_context N'TenantBypass'=1.
        // No bypassRoleName ctor arg — MSSQL uses the session-context flag (Critical Deviation #6).
        MsSqlTenantSessionContextSetter setter = new(log: null);

        using (DapperRlsBypass.Enter())
        {
            await setter.ApplyAsync(conn, _fixture.TenantAId.ToString());
            // setter's bypass branch fires → sp_set_session_context N'TenantBypass'=1
            // predicate OR branch: TRY_CAST(SESSION_CONTEXT(N'TenantBypass') AS INT) = 1 → access all rows

            List<Guid> allIds = await ReadTenantIdsAsync(conn);

            allIds.Should().Contain(_fixture.TenantAId, "bypass scope must see TenantA rows");
            allIds.Should().Contain(_fixture.TenantBId, "bypass scope must see TenantB rows across tenants");
        }

        DapperRlsBypass.IsActive.Should().BeFalse("bypass scope must be cleared after disposal");
    }
}
