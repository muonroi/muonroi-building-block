namespace Muonroi.Governance.License;

/// <summary>
/// Defines the level of license enforcement applied to the application.
/// </summary>
public enum LicenseEnforcementMode
{
    /// <summary>
    /// Development mode: Relaxed checks, developer-friendly warnings.
    /// </summary>
    Development,

    /// <summary>
    /// Production mode: Strict enforcement of license rules and anti-tampering.
    /// </summary>
    Production,

    /// <summary>
    /// Free mode: Basic features allowed, security checks are minimal.
    /// </summary>
    Free
}
