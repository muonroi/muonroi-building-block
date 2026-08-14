namespace Muonroi.Data.Dapper.PostgreSql.IntegrationTests.Rls;

/// <summary>
/// HARD-01 integration acceptance gate for PostgreSQL:
/// <list type="bullet">
///   <item>DDL present (0001 + 0002 applied by <see cref="PostgresContainerFixture"/>) →
///         <c>RlsStartupVerifier.StartingAsync</c> completes without throwing.</item>
///   <item>DDL dropped (policy <c>tenant_isolation</c> + FORCE RLS removed) →
///         <c>StartingAsync</c> throws <see cref="RlsObjectsMissingException"/>.</item>
/// </list>
/// </summary>
/// <remarks>
/// Uses a dedicated container fixture so DDL mutations in the "drop" test do not affect the
/// sibling isolation tests in <see cref="PostgresRlsIsolationTests"/> which run in a separate
/// container via their own fixture instance.
/// The tests connect via the superuser connection string to read catalog views — the verifier
/// opens a plain <see cref="NpgsqlConnection"/> (D-05) and catalog queries are role-agnostic
/// (RESEARCH Pitfall 4).
/// </remarks>
[Collection("PostgresRlsVerifier")]
public sealed class RlsStartupVerifierPostgreSqlTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public RlsStartupVerifierPostgreSqlTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Healthy path: migrations 0001 + 0002 are applied by the fixture.
    /// <c>RlsStartupVerifier.StartingAsync</c> must complete without throwing.
    /// </summary>
    [Fact(DisplayName = "RlsStartupVerifier_WhenDdlPresent_StartingAsync_DoesNotThrow (PostgreSql)")]
    public async Task WhenDdlPresent_StartingAsync_DoesNotThrow()
    {
        RlsStartupVerifier verifier = BuildVerifier(_fixture.SuperuserConnectionString);

        Func<Task> act = () => verifier.StartingAsync(CancellationToken.None);

        await act.Should().NotThrowAsync(
            "all required RLS objects are present after the fixture applies 0001/0002 migrations");
    }

    /// <summary>
    /// Missing-DDL path: drop the <c>tenant_isolation</c> policy on all tenant_id tables and
    /// disable FORCE ROW LEVEL SECURITY, then assert <c>StartingAsync</c> throws
    /// <see cref="RlsObjectsMissingException"/>. State is restored in a finally block so this
    /// test does not corrupt the fixture for other tests in the same collection.
    /// </summary>
    [Fact(DisplayName = "RlsStartupVerifier_WhenDdlDropped_StartingAsync_ThrowsRlsObjectsMissingException (PostgreSql)")]
    public async Task WhenDdlDropped_StartingAsync_ThrowsRlsObjectsMissingException()
    {
        // Drop the tenant_isolation policy + disable FORCE on all tenant_id tables.
        await DropPoliciesAsync(_fixture.SuperuserConnectionString);
        try
        {
            RlsStartupVerifier verifier = BuildVerifier(_fixture.SuperuserConnectionString);

            Func<Task> act = () => verifier.StartingAsync(CancellationToken.None);

            await act.Should().ThrowAsync<RlsObjectsMissingException>(
                "the catalog query must report unhealthy when tenant_isolation policies are absent");
        }
        finally
        {
            // Restore: re-apply the policy so other tests in this fixture are not corrupted.
            await RestorePoliciesAsync(_fixture.SuperuserConnectionString);
        }
    }

    /// <summary>
    /// Opt-out path: when <c>VerifyRlsObjectsOnStartup = false</c>, <c>StartingAsync</c>
    /// must return immediately without a DB round-trip — even if the DDL is missing.
    /// </summary>
    [Fact(DisplayName = "RlsStartupVerifier_WhenVerifyDisabled_StartingAsync_DoesNotThrow (PostgreSql)")]
    public async Task WhenVerifyDisabled_StartingAsync_DoesNotThrow()
    {
        // Drop the tenant_isolation policy + disable FORCE on all tenant_id tables.
        await DropPoliciesAsync(_fixture.SuperuserConnectionString);
        try
        {
            // verify = false → opt-out escape hatch (D-03)
            RlsStartupVerifier verifier = BuildVerifier(_fixture.SuperuserConnectionString, verify: false);

            Func<Task> act = () => verifier.StartingAsync(CancellationToken.None);

            await act.Should().NotThrowAsync(
                "VerifyRlsObjectsOnStartup = false must skip the DB check entirely");
        }
        finally
        {
            await RestorePoliciesAsync(_fixture.SuperuserConnectionString);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static RlsStartupVerifier BuildVerifier(string connectionString, bool verify = true)
    {
        // Inline IConnectionStringProvider that returns the fixture connection string under
        // the "default" key — matches the IDapper registration (DapperRlsServiceCollectionExtensions).
        var connProvider = new FixedConnectionStringProvider(connectionString);
        return new RlsStartupVerifier(
            provider: DapperRlsProvider.PostgreSql,
            verify: verify,
            connStrings: connProvider,
            log: null);
    }

    private static async Task DropPoliciesAsync(string connectionString)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        // Drop the tenant_isolation policy and disable FORCE RLS on all tenant_id tables.
        const string dropSql = @"
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT table_schema, table_name
        FROM information_schema.columns
        WHERE column_name = 'tenant_id'
          AND table_schema NOT IN ('pg_catalog','information_schema')
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.table_schema, r.table_name);
        EXECUTE format('ALTER TABLE %I.%I NO FORCE ROW LEVEL SECURITY', r.table_schema, r.table_name);
    END LOOP;
END $$;";
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = dropSql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RestorePoliciesAsync(string connectionString)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        // Recreate the tenant_isolation policy and re-enable FORCE RLS on all tenant_id tables,
        // mirroring what 0001_enable_rls_postgres.sql does.
        const string restoreSql = @"
DO $$
DECLARE r record;
BEGIN
    FOR r IN
        SELECT table_schema, table_name
        FROM information_schema.columns
        WHERE column_name = 'tenant_id'
          AND table_schema NOT IN ('pg_catalog','information_schema')
    LOOP
        EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', r.table_schema, r.table_name);
        EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', r.table_schema, r.table_name);
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.table_schema, r.table_name);
        EXECUTE format('CREATE POLICY tenant_isolation ON %I.%I USING (tenant_id::text = current_setting(''app.current_tenant_id'', true))', r.table_schema, r.table_name);
    END LOOP;
END $$;";
        await using NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = restoreSql;
        await cmd.ExecuteNonQueryAsync();
    }
}
