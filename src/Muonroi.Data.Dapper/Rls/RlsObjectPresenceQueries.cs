using System.Data.Common;

namespace Muonroi.Data.Dapper.Rls;

/// <summary>
/// Internal helper that runs a per-provider catalog query on a plain <see cref="DbConnection"/>
/// to determine whether the required RLS DDL objects are present and enabled.
/// </summary>
/// <remarks>
/// All SQL uses LITERAL object names only — no interpolation of external/tenant values
/// (HARD-01 security requirement T-04-09). The query is executed via a raw ADO.NET
/// <see cref="DbCommand"/> (<c>CreateCommand</c> / <c>ExecuteScalarAsync</c>) and NEVER
/// through <c>IDapper</c> / <c>TenantRlsDapper</c> (D-05).
/// </remarks>
internal static class RlsObjectPresenceQueries
{
    /// <summary>
    /// Returns <see langword="true"/> when all required RLS DDL objects are present and enabled
    /// for the given <paramref name="provider"/>; <see langword="false"/> when any object is
    /// missing or disabled.
    /// </summary>
    /// <param name="provider">The configured Dapper RLS provider.</param>
    /// <param name="openConnection">
    /// A plain, already-opened <see cref="DbConnection"/>. Must NOT be routed through
    /// <c>IDapper</c>/<c>TenantRlsDapper</c> (D-05).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see langword="true"/> = healthy (all objects present, policies enabled).
    /// <see langword="false"/> = unhealthy (missing policy, missing TVF, or FORCE flag off).
    /// </returns>
    internal static async Task<bool> IsHealthyAsync(
        DapperRlsProvider provider,
        DbConnection openConnection,
        CancellationToken ct = default)
    {
        return provider switch
        {
            DapperRlsProvider.PostgreSql => await CheckPostgreSqlAsync(openConnection, ct).ConfigureAwait(false),
            DapperRlsProvider.MsSql => await CheckMsSqlAsync(openConnection, ct).ConfigureAwait(false),
            _ => false
        };
    }

    // -------------------------------------------------------------------------
    // PostgreSQL predicate
    //
    // Pass = policy 'tenant_isolation' exists AND pg_class.relforcerowsecurity = true
    // for every table with a 'tenant_id' column.
    //
    // Returns 0 (= healthy) when NO table with a tenant_id column is missing either
    // the tenant_isolation policy or the FORCE ROW LEVEL SECURITY flag.
    // Returns > 0 (= unhealthy) when at least one such table is missing either.
    //
    // Literal token 'tenant_isolation' from migration 0001_enable_rls_postgres.sql (Pitfall 1).
    // relforcerowsecurity comes from pg_class (not pg_policies).
    // -------------------------------------------------------------------------
    private static async Task<bool> CheckPostgreSqlAsync(DbConnection conn, CancellationToken ct)
    {
        const string sql = @"
SELECT count(*)
FROM information_schema.columns c
JOIN pg_class      pc ON pc.relname  = c.table_name
JOIN pg_namespace  ns ON ns.nspname  = c.table_schema AND ns.oid = pc.relnamespace
WHERE c.column_name = 'tenant_id'
  AND c.table_schema NOT IN ('pg_catalog','information_schema')
  AND (
        pc.relforcerowsecurity = false
     OR NOT EXISTS (
          SELECT 1 FROM pg_policies p
          WHERE p.schemaname = c.table_schema
            AND p.tablename  = c.table_name
            AND p.policyname = 'tenant_isolation')
      )";

        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        object? result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        long missingCount = Convert.ToInt64(result);
        return missingCount == 0;
    }

    // -------------------------------------------------------------------------
    // MSSQL predicate
    //
    // Pass = (1) dbo.fn_tenant_access inline TVF exists (OBJECT_ID type 'IF')
    //        AND (2) every table with a tenant_id column has an ENABLED
    //            <table>_TenantIsolation SECURITY POLICY (sys.security_policies.is_enabled=1).
    //
    // Requires both conditions — matching D-04 "every tenant_id table has an enabled policy"
    // (strongest check, resolves open-question #1 / Pitfall 2).
    //
    // Literal tokens 'dbo.fn_tenant_access', '_TenantIsolation' from migrations
    // 0001/0002_sqlserver_tenant_rls.sql (Pitfall 2: policy names are per-table derived).
    // -------------------------------------------------------------------------
    private static async Task<bool> CheckMsSqlAsync(DbConnection conn, CancellationToken ct)
    {
        const string sql = @"
DECLARE @fnExists bit =
  CASE WHEN OBJECT_ID('dbo.fn_tenant_access', 'IF') IS NOT NULL THEN 1 ELSE 0 END;

DECLARE @missing int = (
  SELECT count(*)
  FROM sys.tables t
  JOIN sys.columns c ON c.object_id = t.object_id AND c.name = 'tenant_id'
  WHERE NOT EXISTS (
      SELECT 1 FROM sys.security_policies sp
      WHERE sp.name = t.name + N'_TenantIsolation'
        AND sp.is_enabled = 1)
);

SELECT CASE WHEN @fnExists = 1 AND @missing = 0 THEN 1 ELSE 0 END AS healthy;";

        await using DbCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        object? result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result) == 1;
    }
}
