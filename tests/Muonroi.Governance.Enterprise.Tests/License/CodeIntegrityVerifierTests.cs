namespace Muonroi.Governance.Enterprise.Tests.License;

public class CodeIntegrityVerifierTests
{
    private readonly IAssemblyHashCollector _collector;
    private readonly CodeIntegrityVerifier _verifier;

    public CodeIntegrityVerifierTests()
    {
        _collector = Substitute.For<IAssemblyHashCollector>();
        _verifier = new CodeIntegrityVerifier(_collector);
    }

    [Fact]
    public void VerifyIntegrity_WithNoProof_ShouldReturnTrue()
    {
        // Arrange
        var state = new LicenseState { ActivationProof = null };

        // Act
        var result = _verifier.VerifyIntegrity(state);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyIntegrity_WithMatchedHashes_ShouldReturnTrue()
    {
        // Arrange
        var entry = new AssemblyManifestEntry { AssemblyName = "Lib", Version = "1.0", Sha256Hash = "HASH" };
        var state = new LicenseState
        {
            ActivationProof = new ActivationProof
            {
                AllowedAssemblyHashes = [entry]
            }
        };
        _collector.Collect().Returns([entry]);

        // Act
        var result = _verifier.VerifyIntegrity(state);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyIntegrity_WithMismatchedHash_ShouldReturnFalse()
    {
        // Arrange
        var approved = new AssemblyManifestEntry { AssemblyName = "Lib", Version = "1.0", Sha256Hash = "APPROVED" };
        var runtime = new AssemblyManifestEntry { AssemblyName = "Lib", Version = "1.0", Sha256Hash = "TAMPERED" };
        var state = new LicenseState
        {
            ActivationProof = new ActivationProof
            {
                AllowedAssemblyHashes = [approved]
            }
        };
        _collector.Collect().Returns([runtime]);

        // Act
        var result = _verifier.VerifyIntegrity(state);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyIntegrity_WithMissingApproval_ShouldReturnFalse()
    {
        // Arrange
        var runtime = new AssemblyManifestEntry { AssemblyName = "Lib", Version = "1.0", Sha256Hash = "HASH" };
        var state = new LicenseState
        {
            ActivationProof = new ActivationProof
            {
                AllowedAssemblyHashes = []
            }
        };
        // wait, if AllowedAssemblyHashes is empty it returns true early. Let's add one entry that doesn't match.
        state.ActivationProof.AllowedAssemblyHashes = [new AssemblyManifestEntry { AssemblyName = "Other", Version = "1.0", Sha256Hash = "H" }];
        _collector.Collect().Returns([runtime]);

        // Act
        var result = _verifier.VerifyIntegrity(state);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyIntegrity_WithThrowOnFailure_ShouldThrow()
    {
        // Arrange
        var approved = new AssemblyManifestEntry { AssemblyName = "Lib", Version = "1.0", Sha256Hash = "APPROVED" };
        var runtime = new AssemblyManifestEntry { AssemblyName = "Lib", Version = "1.0", Sha256Hash = "TAMPERED" };
        var state = new LicenseState
        {
            ActivationProof = new ActivationProof
            {
                AllowedAssemblyHashes = [approved]
            }
        };
        _collector.Collect().Returns([runtime]);

        // Act & Assert
        Assert.Throws<MInternalException>(() => _verifier.VerifyIntegrity(state, true));
    }
}
