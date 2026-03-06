namespace Muonroi.Governance.ControlPlane;

public interface IMEnterpriseControlPlaneService
{
    MIssueLicenseResult IssueLicense(MIssueLicenseRequest request);
    MControlPlaneLicenseRecord RevokeLicense(MRevokeLicenseRequest request);
    MControlPlaneLicenseRecord AssignTenants(MAssignTenantsRequest request);
    MControlPlanePolicyBundleRecord CreatePolicyDraft(MCreatePolicyDraftRequest request);
    MControlPlanePolicyBundleRecord ApprovePolicyBundle(MApprovePolicyBundleRequest request);
    MControlPlanePolicyBundleRecord ActivatePolicyBundle(MActivatePolicyBundleRequest request);
    MControlPlanePolicyBundleRecord RollbackPolicyBundle(MRollbackPolicyBundleRequest request);
    MControlPlaneLicenseRecord? GetLicense(string licenseId);
    IReadOnlyList<MControlPlanePolicyBundleRecord> GetPolicyBundles(string licenseId);
    MControlPlanePolicyBundleRecord? GetActivePolicyBundle(string licenseId);
    IReadOnlyList<MControlPlaneAuditRecord> GetAuditTrail(int take = 100);
    bool VerifyLicenseSignature(LicensePayload payload);
    bool VerifyPolicyBundleSignature(MControlPlanePolicyBundleRecord bundle);
    bool VerifyAuditRecordSignature(MControlPlaneAuditRecord auditRecord);
}


