using Muonroi.Governance.Abstractions.Integrity;
using Muonroi.Governance.Enterprise.License;

namespace Muonroi.Governance.Tests;

public sealed class CodeIntegrityVerifierTests
{
    [Fact]
    public void VerifyIntegrity_ReturnsFalse_WhenRuntimeManifestMismatchesProof()
    {
        LicenseState state = new()
        {
            ActivationProof = new ActivationProof
            {
                LicenseId = "LIC-INT-1",
                AllowedAssemblyHashes =
                [
                    new AssemblyManifestEntry
                    {
                        AssemblyName = "Muonroi.Core",
                        Version = "1.0.0",
                        Sha256Hash = "expected",
                        PublicKeyToken = "abcd"
                    }
                ]
            }
        };

        CodeIntegrityVerifier verifier = new(new FixedAssemblyHashCollector(
        [
            new AssemblyManifestEntry
            {
                AssemblyName = "Muonroi.Core",
                Version = "1.0.0",
                Sha256Hash = "actual",
                PublicKeyToken = "abcd"
            }
        ]));

        Assert.False(verifier.VerifyIntegrity(state));
    }

    [Fact]
    public void VerifyIntegrity_ReturnsTrue_WhenProofHasNoManifest()
    {
        LicenseState state = new()
        {
            ActivationProof = new ActivationProof
            {
                LicenseId = "LIC-INT-2",
                AllowedAssemblyHashes = []
            }
        };

        CodeIntegrityVerifier verifier = new(new FixedAssemblyHashCollector([]));

        Assert.True(verifier.VerifyIntegrity(state));
    }

    private sealed class FixedAssemblyHashCollector(IReadOnlyList<AssemblyManifestEntry> entries) : IAssemblyHashCollector
    {
        public IReadOnlyList<AssemblyManifestEntry> Collect()
        {
            return entries;
        }
    }
}
