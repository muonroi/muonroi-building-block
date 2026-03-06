namespace Muonroi.Governance.License;

internal sealed class NoopLicenseGuardEnhancer : ILicenseGuardEnhancer
{
    public void OnStartup(LicenseConfigs configs, LicenseState state) { }
    public void OnEnsureValid(string actionType, LicenseState state) { }
    public void OnRecordAction(LicenseActionContext context, LicenseState state) { }
}
