-- Migration 0002: Correct MSSQL RLS predicate key, add TRY_CAST fail-safety, bypass flag, and full BLOCK set.
--
-- What this does:
--   1. Drops all existing <table>_TenantIsolation SECURITY POLICies (clears SCHEMABINDING
--      dependency before the function is dropped — Pitfall 1 / SCHEMABINDING constraint).
--   2. Drops and recreates dbo.fn_tenant_access with:
--        - TRY_CAST instead of CAST (CAST('' AS UNIQUEIDENTIFIER) throws Msg 8169; TRY_CAST
--          returns NULL → fail-closed silently for empty or unset session context — D-06).
--        - Corrected key N'TenantId' (PascalCase, matching the Phase 1 setter) — D-02.
--        - Bypass OR branch: TRY_CAST(SESSION_CONTEXT(N'TenantBypass') AS INT) = 1 — D-04.
--   3. Recreates one SECURITY POLICY per tenant_id table with:
--        - ADD FILTER PREDICATE  (read isolation)
--        - ADD BLOCK PREDICATE … AFTER INSERT  (cross-tenant insert blocked — Msg 33504)
--        - ADD BLOCK PREDICATE … AFTER UPDATE  (cross-tenant update-destination blocked)
--        - ADD BLOCK PREDICATE … BEFORE UPDATE (cross-tenant update-source blocked)
--        - ADD BLOCK PREDICATE … BEFORE DELETE (cross-tenant delete blocked)
--
-- Prerequisites:
--   - Migration 0001_enable_rls_sqlserver.sql MUST be applied first (it created the initial
--     dbo.fn_tenant_access and <table>_TenantIsolation policies that this migration supersedes).
--     This migration does NOT edit 0001.
--
--   REGRESSION NOTE: 0001 used SESSION_CONTEXT key N'tenant_id' (snake_case), but the Phase 1
--   MsSqlTenantSessionContextSetter always writes N'TenantId' (PascalCase). As a result, the
--   0001 policy matched nothing — every query returned zero rows regardless of tenant context.
--   This migration corrects the key to N'TenantId' and adds write-path BLOCK predicates.
--
-- Idempotency:
--   - Re-runnable. Section 2 drops all _TenantIsolation policies before the function is
--     recreated (SCHEMABINDING constraint: the function cannot be dropped while any policy
--     references it). Section 4 drops and recreates each policy inside the cursor loop, so
--     re-running against a database where 0002 was already applied raises no error.
--
-- DEPLOYMENT WARNING:
--   - Migrations must be applied in order: 0001 first, then 0002. Re-applying 0001 after 0002
--     would restore the broken N'tenant_id' key policy; do not re-run 0001 after 0002.
--   - The tenant_id column type must be UNIQUEIDENTIFIER and stable before this migration runs.
--     SCHEMABINDING prevents ALTER TABLE on tenant_id while the policy and function exist;
--     to change the column type, drop the policy and function first, alter, then re-apply 0002.
--   - The application setter must NOT use the read_only flag in sp_set_session_context.
--     Re-setting such a key on a pooled connection raises Msg 15664 on the 2nd+ acquisition (D-07).


-- =============================================================================
-- SECTION 2: Drop all _TenantIsolation SECURITY POLICies (SCHEMABINDING unlock)
-- Must run BEFORE the function DROP in Section 3.
-- =============================================================================

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
DEALLOCATE dp;
GO


-- =============================================================================
-- SECTION 3: Drop and recreate dbo.fn_tenant_access (corrected predicate function)
-- Safe to run here because all policies were dropped in Section 2.
-- =============================================================================

IF OBJECT_ID('dbo.fn_tenant_access', 'IF') IS NOT NULL
    DROP FUNCTION dbo.fn_tenant_access;
GO

CREATE FUNCTION dbo.fn_tenant_access(@tenant_id UNIQUEIDENTIFIER)
RETURNS TABLE WITH SCHEMABINDING AS
    RETURN SELECT 1 AS fn_result
    WHERE
        @tenant_id = TRY_CAST(SESSION_CONTEXT(N'TenantId') AS UNIQUEIDENTIFIER)
        OR TRY_CAST(SESSION_CONTEXT(N'TenantBypass') AS INT) = 1;
GO


-- =============================================================================
-- SECTION 4: Recreate SECURITY POLICY per tenant_id table (FILTER + full BLOCK set)
-- Discovery query reused verbatim from 0001_enable_rls_sqlserver.sql.
-- =============================================================================

DECLARE @schema sysname, @table sysname, @policy sysname, @sql NVARCHAR(MAX);

DECLARE tbl CURSOR FOR
    SELECT s.name, t.name
    FROM sys.tables t
    JOIN sys.schemas s ON t.schema_id = s.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'tenant_id';

OPEN tbl;
FETCH NEXT FROM tbl INTO @schema, @table;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @policy = @table + '_TenantIsolation';
    SET @sql =
        N'CREATE SECURITY POLICY ' + QUOTENAME(@policy) + N'
            ADD FILTER PREDICATE dbo.fn_tenant_access(tenant_id)
                ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N',
            ADD BLOCK PREDICATE dbo.fn_tenant_access(tenant_id)
                ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N' AFTER INSERT,
            ADD BLOCK PREDICATE dbo.fn_tenant_access(tenant_id)
                ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N' AFTER UPDATE,
            ADD BLOCK PREDICATE dbo.fn_tenant_access(tenant_id)
                ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N' BEFORE UPDATE,
            ADD BLOCK PREDICATE dbo.fn_tenant_access(tenant_id)
                ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N' BEFORE DELETE
        WITH (STATE = ON);';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM tbl INTO @schema, @table;
END
CLOSE tbl;
DEALLOCATE tbl;
GO
