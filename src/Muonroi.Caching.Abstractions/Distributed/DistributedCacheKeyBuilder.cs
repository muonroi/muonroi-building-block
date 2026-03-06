namespace Muonroi.Caching.Abstractions.Distributed;

public static class DistributedCacheKeyBuilder
{
    public static string Build(string key, string? keyNamespace = null, string? tenantId = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        string? resolvedTenantId = NormalizeTenantId(tenantId ?? TenantContext.CurrentTenantId);
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

    public static string? NormalizeTenantId(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }
}
