using Muonroi.Governance.License;

namespace Muonroi.Governance.Policy;

/// <summary>
/// Represents a signed security policy that defines enforcement rules and quotas.
/// Policies are issued by the license server and verified by the client.
/// </summary>
public sealed class LicensePolicy
{
    /// <summary>
    /// Gets or sets the Policy Id.
    /// </summary>
    public string PolicyId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";
    /// <summary>
    /// Gets or sets the License Id.
    /// </summary>
    public string LicenseId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the Issued At.
    /// </summary>
    public DateTimeOffset IssuedAt { get; set; }
    /// <summary>
    /// Gets or sets the Expires At.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    /// <summary>
    /// Executes the Enforcement operation.
    /// </summary>
    public PolicyEnforcementRules Enforcement { get; set; } = new();
    /// <summary>
    /// Gets or sets the Feature Quotas.
    /// </summary>
    public Dictionary<string, FeatureQuota> FeatureQuotas { get; set; } = [];
    
    /// <summary>
    /// RSA-SHA256 signature of the policy data (excluding this field).
    /// </summary>
    public string Signature { get; set; } = string.Empty;
}

/// <summary>
/// Represents the Policy Enforcement Rules.
/// </summary>
public sealed class PolicyEnforcementRules
{
    /// <summary>
    /// Gets or sets the Enforce On Database.
    /// </summary>
    public bool EnforceOnDatabase { get; set; } = false;
    /// <summary>
    /// Gets or sets the Enable Anti Tampering.
    /// </summary>
    public bool EnableAntiTampering { get; set; } = false;
    /// <summary>
    /// Gets or sets the Fail Mode.
    /// </summary>
    public LicenseFailMode FailMode { get; set; } = LicenseFailMode.Soft;
    /// <summary>
    /// Gets or sets the Max Api Requests Per Minute.
    /// </summary>
    public int MaxApiRequestsPerMinute { get; set; } = 0; // 0 = unlimited
    /// <summary>
    /// Gets or sets the Max Db Operations Per Minute.
    /// </summary>
    public int MaxDbOperationsPerMinute { get; set; } = 0; // 0 = unlimited
}

/// <summary>
/// Represents the Feature Quota.
/// </summary>
public sealed class FeatureQuota
{
    /// <summary>
    /// Gets or sets the Max Usage Per Day.
    /// </summary>
    public long MaxUsagePerDay { get; set; } = 0; // 0 = unlimited
    /// <summary>
    /// Gets or sets the Max Concurrent Usage.
    /// </summary>
    public int MaxConcurrentUsage { get; set; } = 0; // 0 = unlimited
}
