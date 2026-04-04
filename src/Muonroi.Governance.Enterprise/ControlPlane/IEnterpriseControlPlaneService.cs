namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the IMEnterprise Control Plane Service.
/// </summary>
public interface IMEnterpriseControlPlaneService
{
    /// <summary>
    /// Executes the Issue License operation.
    /// </summary>
    MIssueLicenseResult IssueLicense(MIssueLicenseRequest request);
    /// <summary>
    /// Executes the Revoke License operation.
    /// </summary>
    MControlPlaneLicenseRecord RevokeLicense(MRevokeLicenseRequest request);
    /// <summary>
    /// Executes the Assign Tenants operation.
    /// </summary>
    MControlPlaneLicenseRecord AssignTenants(MAssignTenantsRequest request);
    /// <summary>
    /// Executes the Create Policy Draft operation.
    /// </summary>
    MControlPlanePolicyBundleRecord CreatePolicyDraft(MCreatePolicyDraftRequest request);
    /// <summary>
    /// Executes the Approve Policy Bundle operation.
    /// </summary>
    MControlPlanePolicyBundleRecord ApprovePolicyBundle(MApprovePolicyBundleRequest request);
    /// <summary>
    /// Executes the Activate Policy Bundle operation.
    /// </summary>
    MControlPlanePolicyBundleRecord ActivatePolicyBundle(MActivatePolicyBundleRequest request);
    /// <summary>
    /// Executes the Rollback Policy Bundle operation.
    /// </summary>
    MControlPlanePolicyBundleRecord RollbackPolicyBundle(MRollbackPolicyBundleRequest request);
    /// <summary>
    /// Executes the Get License operation.
    /// </summary>
    MControlPlaneLicenseRecord? GetLicense(string licenseId);
    /// <summary>
    /// Executes the Get Policy Bundles operation.
    /// </summary>
    IReadOnlyList<MControlPlanePolicyBundleRecord> GetPolicyBundles(string licenseId);
    /// <summary>
    /// Executes the Get Active Policy Bundle operation.
    /// </summary>
    MControlPlanePolicyBundleRecord? GetActivePolicyBundle(string licenseId);
    /// <summary>
    /// Executes the Get Audit Trail operation.
    /// </summary>
    IReadOnlyList<MControlPlaneAuditRecord> GetAuditTrail(int take = 100);
    /// <summary>
    /// Executes the Verify License Signature operation.
    /// </summary>
    bool VerifyLicenseSignature(LicensePayload payload);
    /// <summary>
    /// Executes the Verify Policy Bundle Signature operation.
    /// </summary>
    bool VerifyPolicyBundleSignature(MControlPlanePolicyBundleRecord bundle);
    /// <summary>
    /// Executes the Verify Audit Record Signature operation.
    /// </summary>
    bool VerifyAuditRecordSignature(MControlPlaneAuditRecord auditRecord);
}


