using Muonroi.Governance.ControlPlane;
using System.Security.Cryptography;

namespace Muonroi.Governance.Enterprise.Tests.ControlPlane;

public class RsaControlPlaneSignerTests
{
    [Fact]
    public void SignAndVerify_ShouldBeSuccessful()
    {
        // Arrange
        using var signer = MRsaControlPlaneSigner.CreateEphemeral();
        string payload = "test payload";

        // Act
        string signature = signer.Sign(payload);
        bool isValid = signer.Verify(payload, signature);

        // Assert
        Assert.NotEmpty(signature);
        Assert.True(isValid);
    }

    [Fact]
    public void Verify_WithInvalidSignature_ShouldReturnFalse()
    {
        // Arrange
        using var signer = MRsaControlPlaneSigner.CreateEphemeral();
        string payload = "test payload";

        // Act
        bool isValid = signer.Verify(payload, "INVALID_SIG_BASE64_==");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void ExportPublicKeyPem_ShouldReturnValidPem()
    {
        // Arrange
        using var signer = MRsaControlPlaneSigner.CreateEphemeral();

        // Act
        string pem = signer.ExportPublicKeyPem();

        // Assert
        Assert.Contains("BEGIN RSA PUBLIC KEY", pem);
    }
}
