namespace Muonroi.Governance.Tests;

public class LicenseCapabilityResolverTests
{
    [Fact]
    public void Licensed_WithLegacyPremiumFeature_GrantsCapabilityAlias()
    {
        LicenseState state = CreateLicensed(FreeTierFeatures.Premium.AdvancedAuth);

        Assert.True(state.HasFeature(FreeTierFeatures.Premium.AdvancedAuth));
        Assert.True(state.HasFeature("auth.rbac_plus"));
    }

    [Fact]
    public void Licensed_WithCapabilityKey_GrantsLegacyFeatureAlias()
    {
        LicenseState state = CreateLicensed("auth.rbac_plus");

        Assert.True(state.HasFeature("auth.rbac_plus"));
        Assert.True(state.HasFeature(FreeTierFeatures.Premium.AdvancedAuth));
    }

    [Fact]
    public void Licensed_WithPayloadCapabilityKey_GrantsLegacyFeatureAlias()
    {
        LicenseState state = CreateLicensedWithPayload("transport.grpc");

        Assert.True(state.HasFeature("transport.grpc"));
        Assert.True(state.HasFeature(FreeTierFeatures.Premium.Grpc));
    }

    [Fact]
    public void Licensed_WithLegacyFeatureInPayload_GrantsCapabilityAlias()
    {
        LicenseState state = CreateLicensedWithPayload(FreeTierFeatures.Premium.AuditTrail);

        Assert.True(state.HasFeature(FreeTierFeatures.Premium.AuditTrail));
        Assert.True(state.HasFeature("audit.trail"));
    }

    [Fact]
    public void Licensed_PaidTier_AllowsCoreRuntimeActions_WithoutManualActionKeys()
    {
        LicenseState state = CreateLicensed(FreeTierFeatures.Premium.MessageBus);

        Assert.True(state.HasFeature("api.list"));
        Assert.True(state.HasFeature("db.savechanges"));
        Assert.True(state.HasFeature("http.request"));
        Assert.True(state.HasFeature("core.runtime"));
    }

    [Fact]
    public void FreeTier_DoesNotAutoGrantCoreRuntimeCapabilityOrApiActions()
    {
        LicenseState state = LicenseState.CreateFree();

        Assert.False(state.HasFeature("core.runtime"));
        Assert.False(state.HasFeature("api.list"));
        Assert.True(state.HasFeature(FreeTierFeatures.DbQuery));
    }

    [Fact]
    public void InvalidPaidLicense_RemainsRestrictedToFreeBaseline()
    {
        LicenseState state = new()
        {
            IsValid = false,
            Tier = LicenseTier.Licensed,
            Features = [FreeTierFeatures.Premium.MultiTenant, "auth.rbac_plus", "*"]
        };

        Assert.False(state.HasFeature(FreeTierFeatures.Premium.MultiTenant));
        Assert.False(state.HasFeature("auth.rbac_plus"));
        Assert.False(state.HasFeature("api.list"));
        Assert.True(state.HasFeature(FreeTierFeatures.DbSave));
    }

    [Fact]
    public void Licensed_WildcardEntitlement_StillGrantsAnyFeature()
    {
        LicenseState state = CreateLicensed("*");

        Assert.True(state.HasFeature("any.future.feature"));
        Assert.True(state.HasFeature("tenancy.strict"));
    }

    [Fact]
    public void Licensed_UnknownFeature_IsDenied()
    {
        LicenseState state = CreateLicensed(FreeTierFeatures.Premium.RuleEngine);

        Assert.False(state.HasFeature("unknown.feature"));
    }

    [Fact]
    public void Licensed_ServerValidationLegacyKey_MapsToAuditRemoteCapability()
    {
        LicenseState state = CreateLicensed("server-validation");

        Assert.True(state.HasFeature("server-validation"));
        Assert.True(state.HasFeature("audit.remote"));
    }

    private static LicenseState CreateLicensed(params string[] features)
    {
        return new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = features
        };
    }

    private static LicenseState CreateLicensedWithPayload(params string[] features)
    {
        return new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Payload = new LicensePayload
            {
                AllowedFeatures = features
            }
        };
    }
}
