namespace Muonroi.Tenancy.Core.Legacy;

public class MappingTenantConnectionStringFactory(
    IOptions<TenantConnectionStringsOptions> options,
    string defaultConnectionString) : ITenantConnectionStringFactory
{
    private readonly Dictionary<string, string> _map = options.Value.ConnectionStrings ?? [];

    public string GetConnectionString(string? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId) && _map.TryGetValue(tenantId, out string? conn)) return conn;
        return defaultConnectionString;
    }
}
