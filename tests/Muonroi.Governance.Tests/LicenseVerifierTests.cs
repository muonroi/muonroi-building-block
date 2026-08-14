namespace Muonroi.Governance.Tests;

public class LicenseVerifierTests
{
    private static LicenseVerifier CreateVerifier()
    {
        LicenseConfigs configs = new();
        IMJsonSerializeService json = Substitute.For<IMJsonSerializeService>();
        return new LicenseVerifier(configs, null, json);
    }

    [Fact]
    public void Verify_Returns_Free_When_Payload_Is_Null()
    {
        LicenseVerifier verifier = CreateVerifier();

        LicenseState result = verifier.Verify(null, null, "fingerprint-123");

        result.IsValid.Should().BeTrue();
        result.Tier.Should().Be(LicenseTier.Free);
    }

    [Fact]
    public void Verify_Returns_Free_When_LicenseId_Is_FREE()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload payload = new() { LicenseId = "FREE" };

        LicenseState result = verifier.Verify(payload, null, "fingerprint-123");

        result.IsValid.Should().BeTrue();
        result.Tier.Should().Be(LicenseTier.Free);
    }

    [Fact]
    public void Verify_Returns_Invalid_When_NotBefore_Is_In_Future()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload payload = new()
        {
            LicenseId = "LIC-001",
            NotBefore = DateTimeOffset.UtcNow.AddDays(1)
        };

        LicenseState result = verifier.Verify(payload, null, "fingerprint-123");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("not active yet");
    }

    [Fact]
    public void Verify_Returns_Invalid_When_Expired()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload payload = new()
        {
            LicenseId = "LIC-001",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        LicenseState result = verifier.Verify(payload, null, "fingerprint-123");

        result.IsValid.Should().BeFalse();
        result.IsExpired.Should().BeTrue();
        result.Error.Should().Contain("expired");
    }

    [Fact]
    public void Verify_Returns_Invalid_When_Fingerprint_Mismatch()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload payload = new()
        {
            LicenseId = "LIC-001",
            Fingerprint = "expected-fp"
        };

        LicenseState result = verifier.Verify(payload, null, "different-fp");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("fingerprint mismatch");
    }

    [Fact]
    public void Verify_Returns_Invalid_When_HardwareId_Mismatch()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload payload = new()
        {
            LicenseId = "LIC-001",
            HardwareId = "expected-hw"
        };

        LicenseState result = verifier.Verify(payload, null, "different-hw");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Hardware binding mismatch");
    }

    [Fact]
    public void Verify_Returns_Invalid_When_Signature_Is_Missing()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload payload = new()
        {
            LicenseId = "LIC-001"
            // No signature
        };

        LicenseState result = verifier.Verify(payload, null, "fingerprint-123");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Signature invalid");
    }

    [Fact]
    public async Task VerifyAsync_Delegates_To_Verify()
    {
        LicenseVerifier verifier = CreateVerifier();

        LicenseState result = await verifier.VerifyAsync(null, null, "fp");

        result.IsValid.Should().BeTrue();
        result.Tier.Should().Be(LicenseTier.Free);
    }

    [Fact]
    public void Verify_Two_Arg_Overload_Passes_Null_Proof()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload payload = new() { LicenseId = "FREE" };

        LicenseState result = verifier.Verify(payload, "fp");

        result.IsValid.Should().BeTrue();
        result.Tier.Should().Be(LicenseTier.Free);
    }

    [Fact]
    public void Verify_Uses_ActivationProof_Payload_When_Available()
    {
        LicenseVerifier verifier = CreateVerifier();
        LicensePayload proofPayload = new() { LicenseId = "FREE" };
        ActivationProof proof = new()
        {
            SignedLicensePayload = proofPayload
        };

        LicenseState result = verifier.Verify(null, proof, "fp");

        result.IsValid.Should().BeTrue();
        result.Tier.Should().Be(LicenseTier.Free);
    }
}
