namespace Muonroi.Governance.Enterprise.Tests;

using System;
using Muonroi.Governance.License;
using Xunit;

public class GovernanceEnterpriseTests
{
    [Fact]
    public void HmacFingerprintSigner_ComputeSignature_ShouldBeConsistent()
    {
        // Arrange
        var payload = new LicensePayload { LicenseId = "TEST_LICENSE", Signature = "SIG", ServerNonce = "NONCE" };
        var configs = new LicenseConfigs { ProjectSeed = "SEED", FingerprintSalt = "SALT" };
        var signer = new HmacFingerprintSigner(payload, configs);
        
        var context = new LicenseActionContext
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
