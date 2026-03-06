using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Tenancy.Core;

public class DefaultTenantConnectionStringFactory(string connectionString) : ITenantConnectionStringFactory
{
    public string GetConnectionString(string? tenantId)
    {
        return connectionString;
    }
}
