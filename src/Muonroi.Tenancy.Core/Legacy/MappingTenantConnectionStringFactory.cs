namespace Muonroi.Tenancy.Core.Legacy;

/// <summary>
/// Resolves tenant-specific connection strings from configuration with a default fallback.
/// </summary>
/// <param name="options">Tenant connection string options.</param>
/// <param name="defaultConnectionString">Fallback connection string.</param>
public class MappingTenantConnectionStringFactory(
    IOptions<TenantConnectionStringsOptions> options,
    string defaultConnectionString) : ITenantConnectionStringFactory
{
    private readonly Dictionary<string, string> _map = options.Value.ConnectionStrings ?? [];

    /// <inheritdoc />
    public string GetConnectionString(string? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId) && _map.TryGetValue(tenantId, out string? conn)) return conn;
        return defaultConnectionString;
    }
}
