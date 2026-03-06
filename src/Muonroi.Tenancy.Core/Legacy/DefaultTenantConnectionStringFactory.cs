namespace Muonroi.Tenancy.Core.Legacy;

public class DefaultTenantConnectionStringFactory(string connectionString) : ITenantConnectionStringFactory
{
    public string GetConnectionString(string? tenantId) => connectionString;
}
