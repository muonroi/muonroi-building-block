namespace Muonroi.Governance.License;

/// <summary>
/// Extension point for enterprise-grade license enforcement behaviors.
/// OSS tier uses no-op implementation. Enterprise tier injects full enhancer.
/// </summary>
public interface ILicenseGuardEnhancer
{
    /// <summary>Called on LicenseGuard construction. Run startup integrity checks.</summary>
    void OnStartup(LicenseConfigs configs, LicenseState state);

    /// <summary>Called before each EnsureValid().</summary>
    void OnEnsureValid(string actionType, LicenseState state);

    /// <summary>Called on RecordAction().</summary>
    void OnRecordAction(LicenseActionContext context, LicenseState state);
}
