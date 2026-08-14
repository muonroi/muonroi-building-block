namespace Muonroi.Governance.Tests;

public sealed class LicenseRuntimeStatusTests
{
    [Fact]
    public void ProofTierAccessor_ReturnsProofTier_WhenRuntimeHealthy()
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            ActivationProof = new ActivationProof
            {
                LicenseId = "LIC-1",
                Tier = LicenseTier.Enterprise
            }
        };
        LicenseRuntimeStatus runtimeStatus = new();
        ProofTierAccessor accessor = new(state, runtimeStatus);

        Assert.Equal(LicenseTier.Enterprise, accessor.GetEffectiveTier());
    }

    [Fact]
    public void ProofTierAccessor_FallsBackToPayloadTier_WhenProofMissing()
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Licensed
        };
        ProofTierAccessor accessor = new(state, new LicenseRuntimeStatus());

        Assert.Equal(LicenseTier.Licensed, accessor.GetEffectiveTier());
    }

    [Fact]
    public void ProofTierAccessor_RequireMinimumTier_Throws_WhenTierInsufficient()
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Free
        };
        ProofTierAccessor accessor = new(state, new LicenseRuntimeStatus());

        LicenseException ex = Assert.Throws<LicenseException>(
            () => accessor.RequireMinimumTier(LicenseTier.Licensed, "decision-table.web"));
        Assert.Contains("requires tier", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LicenseGuard_UsesRuntimeDowngradeToBlockPremiumFeatures()
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Features = ["*"],
            Payload = new LicensePayload
            {
                LicenseId = "LIC-2",
                AllowedFeatures = ["*"]
            }
        };

        LicenseRuntimeStatus runtimeStatus = new();
        runtimeStatus.DowngradeToFree("revoked");
        LicenseGuard guard = new(
            new LicenseConfigs { FailMode = LicenseFailMode.Hard },
            state,
            new NoopFingerprintChainStore(),
            new NoopFingerprintSigner(),
            runtimeStatus);

        MInternalException ex = Assert.Throws<MInternalException>(
            () => guard.EnsureValid(FreeTierFeatures.Premium.RuleEngine));
        Assert.Contains("not included", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
