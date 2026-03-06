namespace Muonroi.Governance.License;

public static class AuditTrailTenantPartition
{
    public const string HostPartition = "__host__";

    public static string Normalize(string? tenantId)
    {
        return string.IsNullOrWhiteSpace(tenantId) ? HostPartition : tenantId.Trim();
    }
}
