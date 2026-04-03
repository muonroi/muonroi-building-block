namespace Muonroi.Governance.License;

/// <summary>
/// Represents the Anti Tampering Tenant Partition.
/// </summary>
public static class AntiTamperingTenantPartition
{
    /// <summary>
    /// The Host Partition.
    /// </summary>
    public const string HostPartition = "__host__";

    /// <summary>
    /// Executes the Normalize operation.
    /// </summary>
    public static string Normalize(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? HostPartition : tenantId.Trim();
    }
}
