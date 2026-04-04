using Muonroi.Governance.License;
using Muonroi.Governance.Policy;

namespace Muonroi.Governance.Operations;

/// <summary>
/// Represents the MUpgrade Compatibility Severity.
/// </summary>
public enum MUpgradeCompatibilitySeverity
{
    /// <summary>
    /// Represents the Info value.
    /// </summary>
    Info = 0,
    /// <summary>
    /// Represents the Warning value.
    /// </summary>
    Warning = 1,
    /// <summary>
    /// Represents the Blocking value.
    /// </summary>
    Blocking = 2
}

/// <summary>
/// Represents the MUpgrade Compatibility Issue.
/// </summary>
public sealed class MUpgradeCompatibilityIssue
{
    /// <summary>
    /// Gets or sets the Code.
    /// </summary>
    public string Code { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Severity.
    /// </summary>
    public MUpgradeCompatibilitySeverity Severity { get; init; }
    /// <summary>
    /// Gets or sets the Message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    /// <summary>
    /// Gets or sets the Path.
    /// </summary>
    public string? Path { get; init; }
}

/// <summary>
/// Represents the MUpgrade License Config Snapshot.
/// </summary>
public sealed class MUpgradeLicenseConfigSnapshot
{
    /// <summary>
    /// Gets or sets the Mode.
    /// </summary>
    public string? Mode { get; set; }
    /// <summary>
    /// Gets or sets the Require Signed Policy.
    /// </summary>
    public bool RequireSignedPolicy { get; set; }
    /// <summary>
    /// Gets or sets the Enforce On Database.
    /// </summary>
    public bool EnforceOnDatabase { get; set; }
    /// <summary>
    /// Gets or sets the Enforce On Middleware.
    /// </summary>
    public bool EnforceOnMiddleware { get; set; }
    /// <summary>
    /// Gets or sets the Enable Chain.
    /// </summary>
    public bool EnableChain { get; set; }
    /// <summary>
    /// Gets or sets the Enable Server Validation.
    /// </summary>
    public bool EnableServerValidation { get; set; }
    /// <summary>
    /// Gets or sets the Enable Anti Tampering.
    /// </summary>
    public bool EnableAntiTampering { get; set; }
    /// <summary>
    /// Gets or sets the Compliance Enabled.
    /// </summary>
    public bool ComplianceEnabled { get; set; }
    /// <summary>
    /// Gets or sets the Enterprise Secure Defaults Enabled.
    /// </summary>
    public bool EnterpriseSecureDefaultsEnabled { get; set; }
}

/// <summary>
/// Represents the MUpgrade Compatibility Request.
/// </summary>
public sealed class MUpgradeCompatibilityRequest
{
    /// <summary>
    /// Gets or sets the Baseline Package Version.
    /// </summary>
    public string? BaselinePackageVersion { get; set; }
    /// <summary>
    /// Gets or sets the Target Package Version.
    /// </summary>
    public string? TargetPackageVersion { get; set; }
    /// <summary>
    /// Gets or sets the Baseline License.
    /// </summary>
    public LicensePayload? BaselineLicense { get; set; }
    /// <summary>
    /// Gets or sets the Target License.
    /// </summary>
    public LicensePayload? TargetLicense { get; set; }
    /// <summary>
    /// Gets or sets the Baseline Policy.
    /// </summary>
    public LicensePolicy? BaselinePolicy { get; set; }
    /// <summary>
    /// Gets or sets the Target Policy.
    /// </summary>
    public LicensePolicy? TargetPolicy { get; set; }
    /// <summary>
    /// Gets or sets the Baseline Config.
    /// </summary>
    public MUpgradeLicenseConfigSnapshot? BaselineConfig { get; set; }
    /// <summary>
    /// Gets or sets the Target Config.
    /// </summary>
    public MUpgradeLicenseConfigSnapshot? TargetConfig { get; set; }
    /// <summary>
    /// Gets or sets the Treat Warnings As Blocking.
    /// </summary>
    public bool TreatWarningsAsBlocking { get; set; }
}

/// <summary>
/// Represents the MUpgrade Compatibility File Request.
/// </summary>
public sealed class MUpgradeCompatibilityFileRequest
{
    /// <summary>
    /// Gets or sets the Baseline Package Version.
    /// </summary>
    public string? BaselinePackageVersion { get; set; }
    /// <summary>
    /// Gets or sets the Target Package Version.
    /// </summary>
    public string? TargetPackageVersion { get; set; }
    /// <summary>
    /// Gets or sets the Baseline License Path.
    /// </summary>
    public string? BaselineLicensePath { get; set; }
    /// <summary>
    /// Gets or sets the Target License Path.
    /// </summary>
    public string? TargetLicensePath { get; set; }
    /// <summary>
    /// Gets or sets the Baseline Policy Path.
    /// </summary>
    public string? BaselinePolicyPath { get; set; }
    /// <summary>
    /// Gets or sets the Target Policy Path.
    /// </summary>
    public string? TargetPolicyPath { get; set; }
    /// <summary>
    /// Gets or sets the Baseline Appsettings Path.
    /// </summary>
    public string? BaselineAppsettingsPath { get; set; }
    /// <summary>
    /// Gets or sets the Target Appsettings Path.
    /// </summary>
    public string? TargetAppsettingsPath { get; set; }
    /// <summary>
    /// Gets or sets the Treat Warnings As Blocking.
    /// </summary>
    public bool TreatWarningsAsBlocking { get; set; }
}

/// <summary>
/// Represents the MUpgrade Compatibility Result.
/// </summary>
public sealed class MUpgradeCompatibilityResult
{
    /// <summary>
    /// Gets or sets the Is Compatible.
    /// </summary>
    public bool IsCompatible { get; init; }
    /// <summary>
    /// Gets or sets the Has Warnings.
    /// </summary>
    public bool HasWarnings { get; init; }
    /// <summary>
    /// Gets or sets the Issues.
    /// </summary>
    public IReadOnlyList<MUpgradeCompatibilityIssue> Issues { get; init; } = [];
}
