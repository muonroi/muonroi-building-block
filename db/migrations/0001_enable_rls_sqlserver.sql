-- Enable row-level security for all tables containing a tenant_id column
IF OBJECT_ID('dbo.fn_tenant_access', 'IF') IS NOT NULL
    DROP FUNCTION dbo.fn_tenant_access;
GO
CREATE FUNCTION dbo.fn_tenant_access(@tenant_id UNIQUEIDENTIFIER)
RETURNS TABLE WITH SCHEMABINDING AS
    RETURN SELECT 1 AS fn_result
    WHERE @tenant_id = CAST(SESSION_CONTEXT(N'tenant_id') AS UNIQUEIDENTIFIER);
GO

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
    SET @sql = N'IF EXISTS (SELECT 1 FROM sys.security_policies WHERE name = N''' + @policy + N''')
                    DROP SECURITY POLICY ' + QUOTENAME(@policy) + N';
                CREATE SECURITY POLICY ' + QUOTENAME(@policy) + N'
                ADD FILTER PREDICATE dbo.fn_tenant_access(tenant_id) ON ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) + N'
                WITH (STATE = ON);';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM tbl INTO @schema, @table;
END
CLOSE tbl;
DEALLOCATE tbl;
GO
