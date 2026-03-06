namespace Muonroi.Governance.Operations;

public interface IMUpgradeCompatibilityService
{
    MUpgradeCompatibilityResult Evaluate(MUpgradeCompatibilityRequest request);
    MUpgradeCompatibilityResult EvaluateFromFiles(MUpgradeCompatibilityFileRequest request);
}
