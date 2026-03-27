namespace Muonroi.Tenancy.Core;

/// <summary>
/// Resolves tenant schema names and can inject schema hints into connection strings when separate-schema mode is used.
/// </summary>
/// <param name="multiTenantOptions">Multi-tenant options.</param>
public sealed class TenantSchemaSelector(IOptions<MultiTenantOptions> multiTenantOptions)
{
    /// <summary>
    /// Resolves the schema name for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The resolved schema name.</returns>
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

    /// <summary>
    /// Applies schema hints to a connection string when separate-schema mode is enabled.
    /// </summary>
    /// <param name="connectionString">The base connection string.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The updated connection string.</returns>
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
