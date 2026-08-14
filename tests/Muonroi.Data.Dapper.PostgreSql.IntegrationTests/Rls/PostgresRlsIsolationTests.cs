namespace Muonroi.Data.Dapper.PostgreSql.IntegrationTests.Rls;

/// <summary>
/// Phase 2 acceptance gate: the five engine-level RLS isolation proofs (TST-01..TST-05) run
/// against a live Testcontainers PostgreSQL 16 instance via the shared
/// <see cref="PostgresContainerFixture"/>. Every assertion connects as the non-owner
/// <c>app_rls</c> role so the tenant_isolation policy actually binds.
/// </summary>
[Collection("PostgresRls")]
public sealed class PostgresRlsIsolationTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public PostgresRlsIsolationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<NpgsqlConnection> OpenAppRoleConnectionAsync()
    {
        NpgsqlConnection conn = new(_fixture.AppRoleConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    private static async Task SetGucAsync(NpgsqlConnection conn, Guid tenantId)
    {
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false)";
        cmd.Parameters.AddWithValue("@tid", tenantId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SetGucRawAsync(NpgsqlConnection conn, string value)
    {
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tid, false)";
        cmd.Parameters.AddWithValue("@tid", value);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<Guid>> ReadTenantIdsAsync(NpgsqlConnection conn)
    {
        List<Guid> result = new();
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT tenant_id FROM rls_items";
        await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(reader.GetGuid(0));
        }

        return result;
    }

    [Fact]
    public async Task Tst01_TenantA_CannotRead_TenantBRows()
    {
        await using NpgsqlConnection conn = await OpenAppRoleConnectionAsync();
        await SetGucAsync(conn, _fixture.TenantAId);

        List<Guid> tenantIds = await ReadTenantIdsAsync(conn);

        tenantIds.Should().Contain(_fixture.TenantAId, "tenant A must see its own rows");
        tenantIds.Should().NotContain(_fixture.TenantBId, "tenant A must not see tenant B rows");
    }

    [Fact]
    public async Task Tst02_TenantA_CannotInsert_RowAsTenantB()
    {
        await using NpgsqlConnection conn = await OpenAppRoleConnectionAsync();
        await SetGucAsync(conn, _fixture.TenantAId);

        Func<Task> act = async () =>
        {
            await using NpgsqlCommand cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO rls_items (tenant_id, name) VALUES (@b, 'cross-tenant')";
            cmd.Parameters.AddWithValue("@b", _fixture.TenantBId);
            await cmd.ExecuteNonQueryAsync();
        };

        await act.Should()
            .ThrowAsync<PostgresException>()
            .Where(e => e.SqlState == "42501", "WITH CHECK violation must raise insufficient_privilege (42501)");
    }

    [Fact]
    public async Task Tst03_NoContext_ReturnsZeroRows()
    {
        await using NpgsqlConnection conn = await OpenAppRoleConnectionAsync();
        // Replicate "no tenant context": empty-string GUC, mirroring what the Phase 1 setter
        // sends for a null/whitespace tenant id. The policy's NULLIF(...,'')::uuid yields NULL,
        // the equality is NULL for every row, and the table fails closed.
        await SetGucRawAsync(conn, string.Empty);

        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM rls_items";
        long count = (long)(await cmd.ExecuteScalarAsync())!;

        count.Should().Be(0, "an empty tenant context must return zero rows (fail-closed)");
    }

    [Fact]
    public async Task Tst04_PoolReuse_DoesNotLeak_TenantContext()
    {
        await using NpgsqlDataSource dataSource = _fixture.CreateAppRoleDataSource();

        // Connection 1: tenant A, sees only tenant A rows. Returns to pool -> DISCARD ALL.
        await using (NpgsqlConnection conn1 = await dataSource.OpenConnectionAsync())
        {
            await SetGucAsync(conn1, _fixture.TenantAId);
            List<Guid> tenantAIds = await ReadTenantIdsAsync(conn1);
            tenantAIds.Should().Contain(_fixture.TenantAId);
            tenantAIds.Should().NotContain(_fixture.TenantBId);
        }

        // Connection 2: reuses the pooled physical connection. If DISCARD ALL did not reset the
        // GUC, tenant A's context would persist and tenant B's query would leak tenant A rows.
        await using (NpgsqlConnection conn2 = await dataSource.OpenConnectionAsync())
        {
            await SetGucAsync(conn2, _fixture.TenantBId);
            List<Guid> tenantBIds = await ReadTenantIdsAsync(conn2);
            tenantBIds.Should().Contain(_fixture.TenantBId);
            tenantBIds.Should().NotContain(_fixture.TenantAId, "pool reuse must not leak the prior tenant's GUC");
        }
    }

    [Fact]
    public async Task Tst05_BypassScope_ReadsAcrossTenants()
    {
        await using NpgsqlConnection conn = await OpenAppRoleConnectionAsync();

        // Route through the real C# bypass seam (D-06): inside a DapperRlsBypass scope the
        // setter detects IsActive and issues SET ROLE app_rls_bypass on the live connection.
        PostgreSqlTenantSessionContextSetter setter = new(bypassRoleName: "app_rls_bypass");

        using (DapperRlsBypass.Enter())
        {
            await setter.ApplyAsync(conn, _fixture.TenantAId.ToString());

            List<Guid> tenantIds = await ReadTenantIdsAsync(conn);

            tenantIds.Should().Contain(_fixture.TenantAId, "BYPASSRLS role must see tenant A rows");
            tenantIds.Should().Contain(_fixture.TenantBId, "BYPASSRLS role must see tenant B rows across tenants");
        }

        DapperRlsBypass.IsActive.Should().BeFalse("the bypass scope must clear on disposal");
    }
}
