namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Defines the contract for resolving tenant-specific connection strings.
/// </summary>
public interface ITenantConnectionStringFactory
{
    /// <summary>
    /// Gets the connection string for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The resolved connection string.</returns>
    string GetConnectionString(string? tenantId);
}
