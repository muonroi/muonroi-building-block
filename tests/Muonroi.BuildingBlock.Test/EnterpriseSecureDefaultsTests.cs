using System.Security;
using Muonroi.Governance.Policy;

namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class EnterpriseSecureDefaultsTests
{
    [Fact]
    public void IsSignedPolicyRequired_EnterpriseProduction_DefaultsToTrue()
    {
        LicenseConfigs configs = new()
        {
            EnforcementMode = LicenseEnforcementMode.Production
        };
        LicenseState enterprise = CreateEnterpriseState();

        bool required = MEnterpriseSecurityProfile.IsSignedPolicyRequired(configs, enterprise);

        Assert.True(required);
    }

    [Fact]
    public void IsSignedPolicyRequired_EnterpriseProduction_WithBypass_ReturnsFalse()
    {
        LicenseConfigs configs = new()
        {
            EnforcementMode = LicenseEnforcementMode.Production,
            Enterprise = new MEnterpriseSecurityConfigs
            {
                EnableSecureDefaults = true,
                AllowPolicyBypassInProduction = true
            }
        };
        LicenseState enterprise = CreateEnterpriseState();

        bool required = MEnterpriseSecurityProfile.IsSignedPolicyRequired(configs, enterprise);

        Assert.False(required);
    }

    [Fact]
    public void EnsureValid_EnterpriseProductionWithoutPolicy_FailClosed()
    {
        LicenseConfigs configs = new()
        {
            EnforcementMode = LicenseEnforcementMode.Production,
            ProjectSeed = "1234567890123456",
            FingerprintSalt = "tests"
        };

        LicenseGuard guard = CreateGuard(configs, CreateEnterpriseState());
        SecurityException ex = Assert.Throws<SecurityException>(() => guard.EnsureValid("api.list"));
        Assert.Contains("SEC_FAIL_CLOSED", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureValid_EnterpriseProductionPolicyBypass_Allows()
    {
        LicenseConfigs configs = new()
        {
            EnforcementMode = LicenseEnforcementMode.Production,
            ProjectSeed = "1234567890123456",
            FingerprintSalt = "tests",
            Enterprise = new MEnterpriseSecurityConfigs
            {
                AllowPolicyBypassInProduction = true
            }
        };

        LicenseGuard guard = CreateGuard(configs, CreateEnterpriseState());
        guard.EnsureValid("api.list");
    }

    [Fact]
    public void EnsureValid_EnterpriseProductionWithPolicy_Allows()
    {
        LicenseConfigs configs = new()
        {
            EnforcementMode = LicenseEnforcementMode.Production,
            ProjectSeed = "1234567890123456",
            FingerprintSalt = "tests"
        };
        LicenseState state = CreateEnterpriseState();
        LicensePolicy policy = new()
        {
            PolicyId = "pol-test",
            LicenseId = "ENT-001",
            IssuedAt = DateTimeOffset.UtcNow
        };
        PolicyEnforcer policyEnforcer = new(policy);

        LicenseGuard guard = CreateGuard(configs, state, policyEnforcer);
        guard.EnsureValid("api.list");
    }

    private static LicenseState CreateEnterpriseState()
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Enterprise,
            Payload = new LicensePayload()
        };
        state.Payload.LicenseId = "ENT-001";
        state.Payload.AllowedFeatures = ["*"];
        return state;
    }

    private static LicenseGuard CreateGuard(
        LicenseConfigs configs,
        LicenseState state,
        PolicyEnforcer? enforcer = null)
    {
        return new LicenseGuard(
            configs,
            state,
            new NoopFingerprintChainStore(),
            new HmacFingerprintSigner(state.Payload, configs),
            enforcer);
    }
}
