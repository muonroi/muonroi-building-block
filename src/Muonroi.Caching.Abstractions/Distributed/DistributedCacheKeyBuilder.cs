namespace Muonroi.Caching.Abstractions.Distributed;

/// <summary>
/// Builds cache keys with optional namespace and tenant id.
/// </summary>
public static class DistributedCacheKeyBuilder
{
    /// <summary>
    /// Builds a cache key using the provided key, namespace, and tenant id.
    /// </summary>
    /// <param name="key">Base cache key.</param>
    /// <param name="keyNamespace">Optional key namespace.</param>
    /// <param name="tenantId">Optional tenant id.</param>
    /// <returns>The composed cache key.</returns>
    public static string Build(string key, string? keyNamespace = null, string? tenantId = null)
    {
        MGuard.NotNull(key);

        string? resolvedTenantId = NormalizeTenantId(tenantId);
        if (string.IsNullOrWhiteSpace(keyNamespace))
        {
            return string.IsNullOrWhiteSpace(resolvedTenantId) ? key : string.Concat(resolvedTenantId, ":", key);
        }

        if (string.IsNullOrWhiteSpace(resolvedTenantId))
        {
            return string.Concat(keyNamespace.Trim(), ":", key);
        }

        return string.Concat(keyNamespace.Trim(), ":", resolvedTenantId, ":", key);
    }

    /// <summary>
    /// Normalizes a tenant id by trimming whitespace.
    /// </summary>
    /// <param name="tenantId">Tenant id to normalize.</param>
    /// <returns>Normalized tenant id or null when empty.</returns>
    public static string? NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }
}
