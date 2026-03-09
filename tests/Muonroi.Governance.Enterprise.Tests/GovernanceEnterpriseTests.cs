using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.License;

namespace Muonroi.Governance.Enterprise.Tests;


public class GovernanceEnterpriseTests
{
    [Fact]
    public void HmacFingerprintSigner_ComputeSignature_ShouldBeConsistent()
    {
        // Arrange
        LicensePayload payload = new() { LicenseId = "TEST_LICENSE", Signature = "SIG", ServerNonce = "NONCE" };
        LicenseConfigs configs = new() { ProjectSeed = "SEED", FingerprintSalt = "SALT" };
        HmacFingerprintSigner signer = new(payload, configs);

        LicenseActionContext context = new()
        {
            TenantId = "TenantA",
            ActionType = "ACTION",
            ActionName = "ActionA",
            PayloadHash = "HASH",
            Timestamp = DateTimeOffset.Parse("2026-03-08T00:00:00Z")
        };

        // Act
        string sig1 = signer.ComputeSignature("PREV", context, 1);
        string sig2 = signer.ComputeSignature("PREV", context, 1);

        // Assert
        Assert.Equal(sig1, sig2);
        Assert.NotEmpty(sig1);
    }
}
