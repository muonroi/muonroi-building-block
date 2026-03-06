using Muonroi.Governance.License;
using Muonroi.Governance.Policy;

namespace Muonroi.Governance.Operations;

public enum MUpgradeCompatibilitySeverity
{
    Info = 0,
    Warning = 1,
    Blocking = 2
}

public sealed class MUpgradeCompatibilityIssue
{
    public string Code { get; init; } = string.Empty;
    public MUpgradeCompatibilitySeverity Severity { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Path { get; init; }
}

public sealed class MUpgradeLicenseConfigSnapshot
{
    public string? Mode { get; set; }
    public bool RequireSignedPolicy { get; set; }
    public bool EnforceOnDatabase { get; set; }
    public bool EnforceOnMiddleware { get; set; }
    public bool EnableChain { get; set; }
    public bool EnableServerValidation { get; set; }
    public bool EnableAntiTampering { get; set; }
    public bool ComplianceEnabled { get; set; }
    public bool EnterpriseSecureDefaultsEnabled { get; set; }
}

public sealed class MUpgradeCompatibilityRequest
{
    public string? BaselinePackageVersion { get; set; }
    public string? TargetPackageVersion { get; set; }
    public LicensePayload? BaselineLicense { get; set; }
    public LicensePayload? TargetLicense { get; set; }
    public LicensePolicy? BaselinePolicy { get; set; }
    public LicensePolicy? TargetPolicy { get; set; }
    public MUpgradeLicenseConfigSnapshot? BaselineConfig { get; set; }
    public MUpgradeLicenseConfigSnapshot? TargetConfig { get; set; }
    public bool TreatWarningsAsBlocking { get; set; }
}

public sealed class MUpgradeCompatibilityFileRequest
{
    public string? BaselinePackageVersion { get; set; }
    public string? TargetPackageVersion { get; set; }
    public string? BaselineLicensePath { get; set; }
    public string? TargetLicensePath { get; set; }
    public string? BaselinePolicyPath { get; set; }
    public string? TargetPolicyPath { get; set; }
    public string? BaselineAppsettingsPath { get; set; }
    public string? TargetAppsettingsPath { get; set; }
    public bool TreatWarningsAsBlocking { get; set; }
}

public sealed class MUpgradeCompatibilityResult
{
    public bool IsCompatible { get; init; }
    public bool HasWarnings { get; init; }
    public IReadOnlyList<MUpgradeCompatibilityIssue> Issues { get; init; } = [];
}
