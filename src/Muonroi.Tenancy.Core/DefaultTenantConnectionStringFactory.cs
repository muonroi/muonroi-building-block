

namespace Muonroi.Tenancy.Core;

/// <summary>
/// Returns a single shared connection string for all tenants.
/// </summary>
/// <param name="connectionString">The shared connection string.</param>
public class DefaultTenantConnectionStringFactory(string connectionString) : ITenantConnectionStringFactory
{
    /// <inheritdoc />
    public string GetConnectionString(string? tenantId)
    {
        return connectionString;
    }
}
