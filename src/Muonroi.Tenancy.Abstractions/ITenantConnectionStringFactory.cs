namespace Muonroi.Tenancy.Abstractions;

/// <summary>
/// Defines the contract for creating connection strings for specific tenants.
/// </summary>
public interface ITenantConnectionStringFactory
{
    /// <summary>
    /// Gets the connection string for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The unique identifier of the tenant.</param>
    /// <returns>The connection string for the tenant.</returns>
    string GetConnectionString(string? tenantId);
}
