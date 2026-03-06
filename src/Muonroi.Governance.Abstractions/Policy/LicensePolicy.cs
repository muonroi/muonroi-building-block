using Muonroi.Governance.License;

namespace Muonroi.Governance.Policy;

/// <summary>
/// Represents a signed security policy that defines enforcement rules and quotas.
/// Policies are issued by the license server and verified by the client.
/// </summary>
public sealed class LicensePolicy
{
    public string PolicyId { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string LicenseId { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public PolicyEnforcementRules Enforcement { get; set; } = new();
    public Dictionary<string, FeatureQuota> FeatureQuotas { get; set; } = [];
    
    /// <summary>
    /// RSA-SHA256 signature of the policy data (excluding this field).
    /// </summary>
    public string Signature { get; set; } = string.Empty;
}

public sealed class PolicyEnforcementRules
{
    public bool EnforceOnDatabase { get; set; } = false;
    public bool EnableAntiTampering { get; set; } = false;
    public LicenseFailMode FailMode { get; set; } = LicenseFailMode.Soft;
    public int MaxApiRequestsPerMinute { get; set; } = 0; // 0 = unlimited
    public int MaxDbOperationsPerMinute { get; set; } = 0; // 0 = unlimited
}

public sealed class FeatureQuota
{
    public long MaxUsagePerDay { get; set; } = 0; // 0 = unlimited
    public int MaxConcurrentUsage { get; set; } = 0; // 0 = unlimited
}
