namespace Muonroi.Governance.License;

public enum LicenseMode
{
    Offline,
    Online
}

public enum LicenseFailMode
{
    Soft,
    Hard
}

public enum LicenseChainStorage
{
    None,
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
