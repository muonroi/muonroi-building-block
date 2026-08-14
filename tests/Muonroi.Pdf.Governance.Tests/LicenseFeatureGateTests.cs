namespace Muonroi.Pdf.Governance.Tests;

/// <summary>
/// LIC-01 coverage: verifies LicenseFeatureGate delegates to ILicenseGuard.HasFeature,
/// throws FeatureNotLicensedException on denial, and AddPdfEnterprise binds the real gate.
/// Also verifies Task 1 registry registrations via LicenseTier.Licensed state.
/// </summary>
public sealed class LicenseFeatureGateTests
{
    // ─── hand-written fakes (no mocking lib needed) ───────────────────────────

    private sealed class FakeLicenseGuard : ILicenseGuard
    {
        private readonly HashSet<string> _allowedFeatures;

        public FakeLicenseGuard(params string[] allowedFeatures)
        {
            _allowedFeatures = new HashSet<string>(allowedFeatures, StringComparer.OrdinalIgnoreCase);
        }

        public bool HasFeature(string featureName)
            => _allowedFeatures.Contains(featureName);

        // Required interface members — not exercised by these tests
        public LicenseState Current => throw new NotImplementedException();
        public LicenseTier Tier => throw new NotImplementedException();
        public bool IsFreeMode => throw new NotImplementedException();
        public void EnsureValid(string actionType, string? actionName, string? payloadHash, string? correlationId)
            => throw new NotImplementedException();
        public void EnsureFeature(string featureName) => throw new NotImplementedException();
        public void RecordAction(LicenseActionContext context) => throw new NotImplementedException();
        public string GetChainToken() => throw new NotImplementedException();
        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
            => throw new NotImplementedException();
    }

    // ─── Test 1 (LIC-01): unlicensed pdf.designer throws FeatureNotLicensedException ──

    [Fact]
    public void EnsureFeatureOrThrow_UnlicensedCapability_ThrowsFeatureNotLicensedException()
    {
        var guard = new FakeLicenseGuard(); // nothing allowed
        var gate = new LicenseFeatureGate(guard);

        FeatureNotLicensedException ex = Assert.Throws<FeatureNotLicensedException>(
            () => gate.EnsureFeatureOrThrow(CapabilityKeys.PdfDesigner));

        Assert.Equal(CapabilityKeys.PdfDesigner, ex.CapabilityKey);
    }

    // ─── Test 2: licensed pdf.registry passes ─────────────────────────────────

    [Fact]
    public void IsEnabled_LicensedCapability_ReturnsTrueAndDoesNotThrow()
    {
        var guard = new FakeLicenseGuard(CapabilityKeys.PdfRegistry);
        var gate = new LicenseFeatureGate(guard);

        Assert.True(gate.IsEnabled(CapabilityKeys.PdfRegistry));
        gate.EnsureFeatureOrThrow(CapabilityKeys.PdfRegistry); // must not throw
    }

    // ─── Test 3: AddPdfEnterprise resolves LicenseFeatureGate (not AlwaysAllowFeatureGate) ──

    [Fact]
    public void AddPdfEnterprise_ResolvesLicenseFeatureGate_NotAlwaysAllowFeatureGate()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILicenseGuard>(new FakeLicenseGuard());
        services.AddPdfEnterprise();

        using ServiceProvider sp = services.BuildServiceProvider();
        IFeatureGate gate = sp.GetRequiredService<IFeatureGate>();

        Assert.IsType<LicenseFeatureGate>(gate);
        Assert.IsNotType<AlwaysAllowFeatureGate>(gate);
    }

    // ─── Test 4: Task 1 registry coverage — LicenseTier.Licensed + pdf.designer ──
    // Proves LicenseCapabilityResolver.HasAccess resolves pdf.* for a Licensed-tier state.
    // Without CapabilityKeys/FeatureToCapability additions, HasFeature returns false for Licensed tenants
    // (Enterprise short-circuits at line 121 of LicenseCapabilityResolver.cs, but Licensed does not).

    [Fact]
    public void LicenseState_Licensed_WithPdfDesignerFeature_HasFeatureReturnsTrue_AndPdfRegistryReturnsFalse()
    {
        LicenseState state = new()
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = [CapabilityKeys.PdfDesigner]
        };

        Assert.True(state.HasFeature(CapabilityKeys.PdfDesigner),
            "LicenseTier.Licensed with pdf.designer in Features[] should return true — requires CapabilityKeys/FeatureToCapability registrations from Task 1");

        Assert.False(state.HasFeature(CapabilityKeys.PdfRegistry),
            "pdf.registry was NOT in Features[], so HasFeature should return false");
    }
}
