using Muonroi.Governance.ControlPlane;

namespace Muonroi.BuildingBlock.Test;

public class MEnterpriseControlPlaneServiceTests
{
    [Fact]
    public void IssueLicense_CreatesSignedPayload_AndPersists()
    {
        using ControlPlaneHarness harness = new();

        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed,
            RequestedBy = "qa-user"
        };
        MIssueLicenseResult result = harness.Service.IssueLicense(request);

        Assert.NotNull(result.Payload);
        Assert.False(string.IsNullOrWhiteSpace(result.Payload.Signature));
        Assert.True(harness.Service.VerifyLicenseSignature(result.Payload));
        Assert.NotEmpty(result.License.LicenseKey);
        Assert.Equal(MManagedLicenseStatus.Active, result.License.Status);
        Assert.NotNull(harness.Service.GetLicense(result.License.LicenseId));
    }

    [Fact]
    public void RevokeLicense_TransitionsToRevoked_AndRecordsReason()
    {
        using ControlPlaneHarness harness = new();
        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed
        };
        MIssueLicenseResult issued = harness.Service.IssueLicense(request);

        MRevokeLicenseRequest licenseRequest = new()
        {
            LicenseId = issued.License.LicenseId,
            Reason = "contract-terminated",
            RequestedBy = "ops-admin"
        };
        MControlPlaneLicenseRecord revoked = harness.Service.RevokeLicense(licenseRequest);

        Assert.Equal(MManagedLicenseStatus.Revoked, revoked.Status);
        Assert.Equal("contract-terminated", revoked.RevokedReason);
        Assert.NotNull(revoked.RevokedAt);
    }

    [Fact]
    public void AssignTenants_NormalizesDistinctTenantList()
    {
        using ControlPlaneHarness harness = new();
        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed
        };
        MIssueLicenseResult issued = harness.Service.IssueLicense(request);

        MAssignTenantsRequest tenantsRequest = new()
        {
            LicenseId = issued.License.LicenseId,
            TenantIds = ["tenant-b", "tenant-a", "tenant-a", " tenant-b "],
            RequestedBy = "ops-admin"
        };
        MControlPlaneLicenseRecord updated = harness.Service.AssignTenants(tenantsRequest);

        Assert.Equal(["tenant-a", "tenant-b"], updated.TenantAssignments);
        Assert.Equal("tenant-a", updated.Payload.TenantId);
    }

    [Fact]
    public void CreatePolicyDraft_AssignsIncrementalVersion()
    {
        using ControlPlaneHarness harness = new();
        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed
        };
        MIssueLicenseResult issued = harness.Service.IssueLicense(request);

        MCreatePolicyDraftRequest draftRequest = new()
        {
            LicenseId = issued.License.LicenseId,
            RequestedBy = "policy-author"
        };
        MControlPlanePolicyBundleRecord draftV1 = harness.Service.CreatePolicyDraft(draftRequest);
        MCreatePolicyDraftRequest policyDraftRequest = new()
        {
            LicenseId = issued.License.LicenseId,
            RequestedBy = "policy-author"
        };
        MControlPlanePolicyBundleRecord draftV2 = harness.Service.CreatePolicyDraft(policyDraftRequest);

        Assert.Equal(1, draftV1.Version);
        Assert.Equal(2, draftV2.Version);
        Assert.Equal(MPolicyBundleStatus.Draft, draftV1.Status);
        Assert.Equal(MPolicyBundleStatus.Draft, draftV2.Status);
    }

    [Fact]
    public void ApprovePolicyBundle_AddsPolicySignature()
    {
        using ControlPlaneHarness harness = new();
        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed
        };
        MIssueLicenseResult issued = harness.Service.IssueLicense(request);
        MCreatePolicyDraftRequest draftRequest = new()
        {
            LicenseId = issued.License.LicenseId,
            RequestedBy = "policy-author"
        };
        MControlPlanePolicyBundleRecord draft = harness.Service.CreatePolicyDraft(draftRequest);

        MApprovePolicyBundleRequest bundleRequest = new()
        {
            BundleId = draft.BundleId,
            RequestedBy = "policy-approver"
        };
        MControlPlanePolicyBundleRecord approved = harness.Service.ApprovePolicyBundle(bundleRequest);

        Assert.Equal(MPolicyBundleStatus.Approved, approved.Status);
        Assert.NotNull(approved.ApprovedAt);
        Assert.False(string.IsNullOrWhiteSpace(approved.Policy.Signature));
        Assert.True(harness.Service.VerifyPolicyBundleSignature(approved));
    }

    [Fact]
    public void ActivatePolicyBundle_SupersedesPreviousActiveBundle()
    {
        using ControlPlaneHarness harness = new();
        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed
        };
        MIssueLicenseResult issued = harness.Service.IssueLicense(request);

        MCreatePolicyDraftRequest draftRequest = new()
        {
            LicenseId = issued.License.LicenseId
        };
        MControlPlanePolicyBundleRecord draftV1 = harness.Service.CreatePolicyDraft(draftRequest);
        MApprovePolicyBundleRequest bundleRequest = new()
        {
            BundleId = draftV1.BundleId
        };
        MControlPlanePolicyBundleRecord approvedV1 = harness.Service.ApprovePolicyBundle(bundleRequest);
        MActivatePolicyBundleRequest policyBundleRequest = new()
        {
            BundleId = approvedV1.BundleId
        };
        MControlPlanePolicyBundleRecord activeV1 = harness.Service.ActivatePolicyBundle(policyBundleRequest);

        MCreatePolicyDraftRequest policyDraftRequest = new()
        {
            LicenseId = issued.License.LicenseId
        };
        MControlPlanePolicyBundleRecord draftV2 = harness.Service.CreatePolicyDraft(policyDraftRequest);
        MApprovePolicyBundleRequest approvePolicyBundleRequest = new()
        {
            BundleId = draftV2.BundleId
        };
        MControlPlanePolicyBundleRecord approvedV2 = harness.Service.ApprovePolicyBundle(approvePolicyBundleRequest);
        MActivatePolicyBundleRequest activatePolicyBundleRequest = new()
        {
            BundleId = approvedV2.BundleId
        };
        MControlPlanePolicyBundleRecord activeV2 = harness.Service.ActivatePolicyBundle(activatePolicyBundleRequest);

        Assert.Equal(MPolicyBundleStatus.Activated, activeV2.Status);
        IReadOnlyList<MControlPlanePolicyBundleRecord> all = harness.Service.GetPolicyBundles(issued.License.LicenseId);
        MControlPlanePolicyBundleRecord previous = all.Single(x => x.BundleId == activeV1.BundleId);
        Assert.Equal(MPolicyBundleStatus.Superseded, previous.Status);
    }

    [Fact]
    public void RollbackPolicyBundle_ActivatesTargetVersion_AndMarksCurrentRolledBack()
    {
        using ControlPlaneHarness harness = new();
        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed
        };
        MIssueLicenseResult issued = harness.Service.IssueLicense(request);

        MCreatePolicyDraftRequest draftRequest = new()
        {
            LicenseId = issued.License.LicenseId
        };
        MControlPlanePolicyBundleRecord draftV1 = harness.Service.CreatePolicyDraft(draftRequest);
        MApprovePolicyBundleRequest bundleRequest = new()
        {
            BundleId = draftV1.BundleId
        };
        MControlPlanePolicyBundleRecord approvedV1 = harness.Service.ApprovePolicyBundle(bundleRequest);
        MActivatePolicyBundleRequest policyBundleRequest = new()
        {
            BundleId = approvedV1.BundleId
        };
        harness.Service.ActivatePolicyBundle(policyBundleRequest);

        MCreatePolicyDraftRequest policyDraftRequest = new()
        {
            LicenseId = issued.License.LicenseId
        };
        MControlPlanePolicyBundleRecord draftV2 = harness.Service.CreatePolicyDraft(policyDraftRequest);
        MApprovePolicyBundleRequest approvePolicyBundleRequest = new()
        {
            BundleId = draftV2.BundleId
        };
        MControlPlanePolicyBundleRecord approvedV2 = harness.Service.ApprovePolicyBundle(approvePolicyBundleRequest);
        MActivatePolicyBundleRequest activatePolicyBundleRequest = new()
        {
            BundleId = approvedV2.BundleId
        };
        MControlPlanePolicyBundleRecord activeV2 = harness.Service.ActivatePolicyBundle(activatePolicyBundleRequest);

        MRollbackPolicyBundleRequest rollbackPolicyBundleRequest = new()
        {
            LicenseId = issued.License.LicenseId,
            TargetVersion = 1,
            Reason = "hotfix rollback",
            RequestedBy = "release-manager"
        };
        MControlPlanePolicyBundleRecord rolledBackToV1 = harness.Service.RollbackPolicyBundle(rollbackPolicyBundleRequest);

        Assert.Equal(1, rolledBackToV1.Version);
        Assert.Equal(MPolicyBundleStatus.Activated, rolledBackToV1.Status);
        IReadOnlyList<MControlPlanePolicyBundleRecord> all = harness.Service.GetPolicyBundles(issued.License.LicenseId);
        MControlPlanePolicyBundleRecord sourceV2 = all.Single(x => x.BundleId == activeV2.BundleId);
        Assert.Equal(MPolicyBundleStatus.RolledBack, sourceV2.Status);
        Assert.Equal("hotfix rollback", sourceV2.RollbackReason);
    }

    [Fact]
    public void AuditTrail_ContainsSignedEntries_ForAllOperations()
    {
        using ControlPlaneHarness harness = new();
        MIssueLicenseRequest request = new()
        {
            OrganizationName = "Muonroi QA",
            Tier = LicenseTier.Licensed
        };
        MIssueLicenseResult issued = harness.Service.IssueLicense(request);
        MAssignTenantsRequest tenantsRequest = new()
        {
            LicenseId = issued.License.LicenseId,
            TenantIds = ["tenant-a"]
        };
        harness.Service.AssignTenants(tenantsRequest);

        MCreatePolicyDraftRequest draftRequest = new()
        {
            LicenseId = issued.License.LicenseId
        };
        MControlPlanePolicyBundleRecord draft = harness.Service.CreatePolicyDraft(draftRequest);
        MApprovePolicyBundleRequest bundleRequest = new()
        {
            BundleId = draft.BundleId
        };
        harness.Service.ApprovePolicyBundle(bundleRequest);
        MActivatePolicyBundleRequest policyBundleRequest = new()
        {
            BundleId = draft.BundleId
        };
        harness.Service.ActivatePolicyBundle(policyBundleRequest);
        MRevokeLicenseRequest licenseRequest = new()
        {
            LicenseId = issued.License.LicenseId
        };
        harness.Service.RevokeLicense(licenseRequest);

        IReadOnlyList<MControlPlaneAuditRecord> audit = harness.Service.GetAuditTrail(50);
        Assert.True(audit.Count >= 6);
        Assert.All(audit, entry => Assert.True(harness.Service.VerifyAuditRecordSignature(entry)));
    }

    private sealed class ControlPlaneHarness : IDisposable
    {
        private readonly string _root;
        private readonly MRsaControlPlaneSigner _signer;

        public MEnterpriseControlPlaneService Service { get; }

        public ControlPlaneHarness()
        {
            _root = Path.Combine(Path.GetTempPath(), "muonroi-control-plane-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);

            string registryPath = Path.Combine(_root, "control-plane-registry.json");
            _signer = MRsaControlPlaneSigner.CreateEphemeral("test-key");
            MFileControlPlaneStore store = new(registryPath);
            Service = new MEnterpriseControlPlaneService(store, _signer);
        }

        public void Dispose()
        {
            _signer.Dispose();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}


