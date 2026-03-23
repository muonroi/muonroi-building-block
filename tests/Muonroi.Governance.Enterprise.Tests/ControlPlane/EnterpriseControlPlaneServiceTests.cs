using Muonroi.Governance.ControlPlane;
using Muonroi.Governance.Policy;
using Muonroi.Governance.License;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Core.Abstractions.Interfaces;
using NSubstitute;

namespace Muonroi.Governance.Enterprise.Tests.ControlPlane;

public class EnterpriseControlPlaneServiceTests
{
    private readonly IMControlPlaneStore _store;
    private readonly IMControlPlaneSigner _signer;
    private readonly IMDateTimeService _dateTimeService;
    private readonly IMJsonSerializeService _jsonSerializeService;
    private readonly MEnterpriseControlPlaneService _service;

    public EnterpriseControlPlaneServiceTests()
    {
        _store = Substitute.For<IMControlPlaneStore>();
        _signer = Substitute.For<IMControlPlaneSigner>();
        _dateTimeService = Substitute.For<IMDateTimeService>();
        _jsonSerializeService = Substitute.For<IMJsonSerializeService>();
        _service = new MEnterpriseControlPlaneService(_store, _signer, _dateTimeService, _jsonSerializeService);
    }

    [Fact]
    public void IssueLicense_WithValidRequest_ShouldReturnResult()
    {
        // Arrange
        var request = new MIssueLicenseRequest
        {
            OrganizationName = "TestOrg",
            Tier = LicenseTier.Enterprise,
            RequestedBy = "Admin"
        };
        var registry = new MControlPlaneRegistry();
        _store.Load().Returns(registry);
        _dateTimeService.UtcNow().Returns(DateTime.UtcNow);
        _signer.Sign(Arg.Any<string>()).Returns("SIG");
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");

        // Act
        var result = _service.IssueLicense(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestOrg", result.License.OrganizationName);
        Assert.Equal(LicenseTier.Enterprise, result.License.Tier);
        _store.Received().Save(Arg.Any<MControlPlaneRegistry>());
    }

    [Fact]
    public void RevokeLicense_WithValidId_ShouldUpdateStatus()
    {
        // Arrange
        var licenseId = "lic_123";
        var record = new MControlPlaneLicenseRecord { LicenseId = licenseId, Status = MManagedLicenseStatus.Active };
        var registry = new MControlPlaneRegistry { Licenses = [record] };
        _store.Load().Returns(registry);
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");

        var request = new MRevokeLicenseRequest { LicenseId = licenseId, RequestedBy = "Admin" };

        // Act
        var result = _service.RevokeLicense(request);

        // Assert
        Assert.Equal(MManagedLicenseStatus.Revoked, result.Status);
        _store.Received().Save(Arg.Any<MControlPlaneRegistry>());
    }

    [Fact]
    public void CreatePolicyDraft_WithValidLicense_ShouldReturnBundle()
    {
        // Arrange
        var licenseId = "lic_123";
        var record = new MControlPlaneLicenseRecord { LicenseId = licenseId, Status = MManagedLicenseStatus.Active };
        var registry = new MControlPlaneRegistry { Licenses = [record] };
        _store.Load().Returns(registry);
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");

        var request = new MCreatePolicyDraftRequest
        {
            LicenseId = licenseId,
            Enforcement = new PolicyEnforcementRules(),
            RequestedBy = "Admin"
        };

        // Act
        var result = _service.CreatePolicyDraft(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(licenseId, result.LicenseId);
        Assert.Equal(MPolicyBundleStatus.Draft, result.Status);
        _store.Received().Save(Arg.Any<MControlPlaneRegistry>());
    }

    [Fact]
    public void ApprovePolicyBundle_WithValidDraft_ShouldUpdateStatus()
    {
        // Arrange
        var bundleId = "bundle_123";
        var bundle = new MControlPlanePolicyBundleRecord 
        { 
            BundleId = bundleId, 
            Status = MPolicyBundleStatus.Draft,
            Policy = new LicensePolicy()
        };
        var registry = new MControlPlaneRegistry { PolicyBundles = [bundle] };
        _store.Load().Returns(registry);
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");
        _signer.Sign(Arg.Any<string>()).Returns("SIG");

        var request = new MApprovePolicyBundleRequest { BundleId = bundleId, RequestedBy = "Admin" };

        // Act
        var result = _service.ApprovePolicyBundle(request);

        // Assert
        Assert.Equal(MPolicyBundleStatus.Approved, result.Status);
        Assert.Equal("SIG", result.Policy.Signature);
    }

    [Fact]
    public void ActivatePolicyBundle_WithApprovedBundle_ShouldUpdateStatus()
    {
        // Arrange
        var bundleId = "bundle_123";
        var licenseId = "lic_123";
        var bundle = new MControlPlanePolicyBundleRecord 
        { 
            BundleId = bundleId, 
            LicenseId = licenseId,
            Status = MPolicyBundleStatus.Approved,
            Policy = new LicensePolicy()
        };
        var registry = new MControlPlaneRegistry { PolicyBundles = [bundle] };
        _store.Load().Returns(registry);
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");

        var request = new MActivatePolicyBundleRequest { BundleId = bundleId, RequestedBy = "Admin" };

        // Act
        var result = _service.ActivatePolicyBundle(request);

        // Assert
        Assert.Equal(MPolicyBundleStatus.Activated, result.Status);
    }

    [Fact]
    public void RollbackPolicyBundle_WithValidTarget_ShouldUpdateStatus()
    {
        // Arrange
        var licenseId = "lic_123";
        var license = new MControlPlaneLicenseRecord { LicenseId = licenseId, Status = MManagedLicenseStatus.Active };
        var current = new MControlPlanePolicyBundleRecord 
        { 
            BundleId = "b2", 
            LicenseId = licenseId, 
            Status = MPolicyBundleStatus.Activated, 
            Version = 2,
            Policy = new LicensePolicy()
        };
        var target = new MControlPlanePolicyBundleRecord 
        { 
            BundleId = "b1", 
            LicenseId = licenseId, 
            Status = MPolicyBundleStatus.Superseded, 
            Version = 1,
            Policy = new LicensePolicy()
        };
        var registry = new MControlPlaneRegistry { Licenses = [license], PolicyBundles = [current, target] };
        _store.Load().Returns(registry);
        _jsonSerializeService.Serialize(Arg.Any<object>()).Returns("{}");

        var request = new MRollbackPolicyBundleRequest { LicenseId = licenseId, TargetVersion = 1, RequestedBy = "Admin" };


        // Act
        var result = _service.RollbackPolicyBundle(request);

        // Assert
        Assert.Equal(MPolicyBundleStatus.Activated, result.Status);
        Assert.Equal(1, result.Version);
        Assert.Equal(MPolicyBundleStatus.RolledBack, current.Status);
    }
}
