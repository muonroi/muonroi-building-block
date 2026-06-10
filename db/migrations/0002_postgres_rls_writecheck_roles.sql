-- Migration 0002: Close the PostgreSQL RLS write-path gap and provision RLS roles.
--
-- What this does:
--   1. Provisions two roles: app_rls (normal app role) and app_rls_bypass (BYPASSRLS group role).
--   2. Extends the existing tenant_isolation policy (created by 0001) with a WITH CHECK
--      predicate, byte-identical to its USING clause, on every table that has a tenant_id column.
--      0001's CREATE POLICY ... USING (...) covers reads but leaves INSERT/UPDATE writes
--      unchecked; WITH CHECK closes that cross-tenant write gap (ISO-02).
--   3. Grants SELECT/INSERT/UPDATE/DELETE on all tenant_id tables to both roles.
--
-- Prerequisites:
--   - Migration 0001_enable_rls_postgres.sql MUST be applied first (it creates the
--     tenant_isolation policy this migration ALTERs). This migration does NOT edit 0001.
--
-- Idempotency:
--   - Re-runnable. Role creation is guarded by DO $$ IF NOT EXISTS (SELECT FROM pg_roles)
--     (PostgreSQL has no CREATE ROLE IF NOT EXISTS). ALTER POLICY, repeated GRANT, and
--     GRANT role-membership are all no-ops when already applied.
--
-- GUC reused: app.current_tenant_id (the same session GUC set by the EF
--   TenantRlsConnectionInterceptor and the Dapper PostgreSqlTenantSessionContextSetter).
--   One policy set serves both code paths.
--
-- DEPLOYMENT WARNING:
--   - The application connection string MUST connect as app_rls (a non-owner,
--     non-BYPASSRLS role) so FORCE ROW LEVEL SECURITY and the policy actually bind.
--     Do NOT connect as the table owner or a superuser — RLS would be bypassed.
--   - The application connection string MUST NOT include "No Reset On Close=true".
--     Disabling Npgsql's DISCARD ALL on pool return would leak app.current_tenant_id
--     and any SET ROLE across requests, breaking tenant isolation.

-- SECTION 2 — Role provisioning (idempotent).
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_rls') THEN
        CREATE ROLE app_rls LOGIN PASSWORD 'change_me_in_deployment';
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'app_rls_bypass') THEN
        CREATE ROLE app_rls_bypass BYPASSRLS NOLOGIN;
    END IF;
END $$;

-- Allow app_rls to SET ROLE app_rls_bypass for sanctioned BypassTenant() scopes (BYP-01).
GRANT app_rls_bypass TO app_rls;

-- SECTION 3 — Extend tenant_isolation with WITH CHECK on every tenant_id table.
-- The WITH CHECK predicate is byte-identical to 0001's USING predicate (no cast,
-- no null-tolerant escape branch — fail-closed preserved per D-04/ISO-01: an unset
-- GUC yields NULL, the equality fails, and zero rows pass the check).
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT table_schema, table_name
        FROM information_schema.columns
        WHERE column_name = 'tenant_id'
          AND table_schema NOT IN ('pg_catalog','information_schema')
    LOOP
        EXECUTE format('ALTER POLICY tenant_isolation ON %I.%I WITH CHECK (tenant_id = current_setting(''app.current_tenant_id'', true))', r.table_schema, r.table_name);
    END LOOP;
END $$;

-- SECTION 4 — Grant DML on all tenant_id tables to both roles.
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT table_schema, table_name
        FROM information_schema.columns
        WHERE column_name = 'tenant_id'
          AND table_schema NOT IN ('pg_catalog','information_schema')
    LOOP
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON %I.%I TO app_rls', r.table_schema, r.table_name);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON %I.%I TO app_rls_bypass', r.table_schema, r.table_name);
    END LOOP;
END $$;
