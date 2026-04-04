namespace Muonroi.Governance.Operations;

/// <summary>
/// Represents the IMUpgrade Compatibility Service.
/// </summary>
public interface IMUpgradeCompatibilityService
{
    /// <summary>
    /// Executes the Evaluate operation.
    /// </summary>
    MUpgradeCompatibilityResult Evaluate(MUpgradeCompatibilityRequest request);
    /// <summary>
    /// Executes the Evaluate From Files operation.
    /// </summary>
    MUpgradeCompatibilityResult EvaluateFromFiles(MUpgradeCompatibilityFileRequest request);
}
