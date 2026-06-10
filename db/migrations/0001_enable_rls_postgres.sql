-- Enable row-level security for all tables containing a tenant_id column.
-- Uses app.current_tenant_id (set by TenantRlsConnectionInterceptor / the Dapper
-- PostgreSqlTenantSessionContextSetter). The predicate compares BOTH sides as text
-- (tenant_id::text = current_setting(...)) so it works for uuid AND non-uuid tenant_id
-- columns alike: uuid::text = text and text::text = text are both valid, whereas a bare
-- "uuid = text" comparison has no operator and errors. current_setting(..., true) returns
-- NULL when the GUC is unset, and NULL never equals a real id, so the predicate fails
-- closed (zero rows) — no IS NULL escape branch is added (D-04).
-- FORCE ROW LEVEL SECURITY ensures the policy applies even to the table owner.
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
        EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', r.table_schema, r.table_name);
        EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', r.table_schema, r.table_name);
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.table_schema, r.table_name);
        EXECUTE format('CREATE POLICY tenant_isolation ON %I.%I USING (tenant_id::text = current_setting(''app.current_tenant_id'', true))', r.table_schema, r.table_name);
    END LOOP;
END $$;
