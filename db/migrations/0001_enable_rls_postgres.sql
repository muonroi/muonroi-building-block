-- Enable row-level security for all tables containing a tenant_id column.
-- Uses app.current_tenant_id (set by TenantRlsConnectionInterceptor) without ::uuid cast
-- so it works with both UUID and non-UUID tenant identifiers.
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
        EXECUTE format('CREATE POLICY tenant_isolation ON %I.%I USING (tenant_id = current_setting(''app.current_tenant_id'', true))', r.table_schema, r.table_name);
    END LOOP;
END $$;
