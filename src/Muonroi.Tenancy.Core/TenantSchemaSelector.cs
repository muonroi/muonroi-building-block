namespace Muonroi.Tenancy.Core;

/// <summary>
/// Resolves tenant schema names and can inject schema hints into connection strings when separate-schema mode is used.
/// </summary>
public sealed class TenantSchemaSelector(IOptions<MultiTenantOptions> multiTenantOptions)
{
    public string ResolveSchema(string? tenantId)
    {
        if (multiTenantOptions.Value.Strategy != TenantIsolationStrategy.SeparateSchema)
        {
            return "dbo";
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return "dbo";
        }

        string schema = tenantId.Trim().ToLowerInvariant()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(schema) ? "dbo" : schema;
    }

    public string ApplyToConnectionString(string connectionString, string? tenantId)
    {
        if (multiTenantOptions.Value.Strategy != TenantIsolationStrategy.SeparateSchema ||
            string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        string schema = ResolveSchema(tenantId);
        if (connectionString.Contains("SearchPath=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Search Path=", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
        {
            return $"{connectionString.TrimEnd(';')};SearchPath={schema}";
        }

        return connectionString;
    }
}
