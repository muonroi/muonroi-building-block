namespace Muonroi.Governance.License;

/// <summary>
/// Represents the License Mode.
/// </summary>
public enum LicenseMode
{
    /// <summary>
    /// Represents the Offline value.
    /// </summary>
    Offline,
    /// <summary>
    /// Represents the Online value.
    /// </summary>
    Online
}

/// <summary>
/// Controls which factors the runtime fingerprint is derived from.
/// </summary>
public enum LicenseFingerprintScope
{
    /// <summary>
    /// Default. Fingerprint includes machine hardware + OS + ApplicationName + ProjectSeed + FingerprintSalt.
    /// Binds a license to a specific project AND a specific machine.
    /// </summary>
    MachineAndProject,

    /// <summary>
    /// Fingerprint includes only ApplicationName + ProjectSeed + FingerprintSalt (no hardware/OS).
    /// Binds a license to a project but lets it run on any machine (dev / UAT / prod).
    /// </summary>
    ProjectOnly
}

/// <summary>
/// Represents the License Fail Mode.
/// </summary>
public enum LicenseFailMode
{
    /// <summary>
    /// Represents the Soft value.
    /// </summary>
    Soft,
    /// <summary>
    /// Represents the Hard value.
    /// </summary>
    Hard
}

/// <summary>
/// Represents the License Chain Storage.
/// </summary>
public enum LicenseChainStorage
{
    /// <summary>
    /// Represents the None value.
    /// </summary>
    None,
    /// <summary>
    /// Represents the File value.
    /// </summary>
    File
}

/// <summary>
/// License tier determines the feature set available to the application.
/// </summary>
public enum LicenseTier
{
    /// <summary>
    /// Free tier - No license required. Basic features only.
    /// </summary>
    Free = 0,

    /// <summary>
    /// Licensed tier - Valid license with standard features.
    /// </summary>
    Licensed = 1,

    /// <summary>
    /// Enterprise tier - Full feature set with AllowedFeatures=["*"].
    /// </summary>
    Enterprise = 2
}
