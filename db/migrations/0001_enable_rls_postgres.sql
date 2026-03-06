-- Enable row-level security for all tables containing a tenant_id column
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
        EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', r.table_schema, r.table_name);
        EXECUTE format('CREATE POLICY tenant_isolation ON %I.%I USING (tenant_id = current_setting(''app.current_tenant'')::uuid)', r.table_schema, r.table_name);
    END LOOP;
END $$;
