using Muonroi.Pdf.Enterprise;

namespace Muonroi.Pdf.Tests.Enterprise;

/// <summary>
/// Tests for <see cref="AlwaysAllowFeatureGate"/> (allow path) and
/// exception behavior via a stub <see cref="DenyAllFeatureGate"/>.
/// </summary>
public sealed class FeatureGateTests
{
    // -----------------------------------------------------------------------
    // AlwaysAllowFeatureGate — allow paths
    // -----------------------------------------------------------------------

    [Fact]
    public void AlwaysAllow_IsEnabled_ReturnsTrue_ForDesigner()
    {
        var gate = AlwaysAllowFeatureGate.Instance;
        gate.IsEnabled(CapabilityKeys.PdfDesigner).Should().BeTrue();
    }

    [Fact]
    public void AlwaysAllow_IsEnabled_ReturnsTrue_ForRegistry()
    {
        var gate = AlwaysAllowFeatureGate.Instance;
        gate.IsEnabled(CapabilityKeys.PdfRegistry).Should().BeTrue();
    }

    [Fact]
    public void AlwaysAllow_IsEnabled_ReturnsTrue_ForCanary()
    {
        var gate = AlwaysAllowFeatureGate.Instance;
        gate.IsEnabled(CapabilityKeys.PdfCanary).Should().BeTrue();
    }

    [Fact]
    public void AlwaysAllow_IsEnabled_ReturnsTrue_ForArbitraryKey()
    {
        var gate = AlwaysAllowFeatureGate.Instance;
        gate.IsEnabled("some.unknown.key").Should().BeTrue();
    }

    [Fact]
    public void AlwaysAllow_EnsureFeatureOrThrow_DoesNotThrow_ForDesigner()
    {
        var gate = AlwaysAllowFeatureGate.Instance;
        var act = () => gate.EnsureFeatureOrThrow(CapabilityKeys.PdfDesigner);
        act.Should().NotThrow();
    }

    [Fact]
    public void AlwaysAllow_EnsureFeatureOrThrow_DoesNotThrow_ForRegistry()
    {
        var gate = AlwaysAllowFeatureGate.Instance;
        var act = () => gate.EnsureFeatureOrThrow(CapabilityKeys.PdfRegistry);
        act.Should().NotThrow();
    }

    [Fact]
    public void AlwaysAllow_EnsureFeatureOrThrow_DoesNotThrow_ForCanary()
    {
        var gate = AlwaysAllowFeatureGate.Instance;
        var act = () => gate.EnsureFeatureOrThrow(CapabilityKeys.PdfCanary);
        act.Should().NotThrow();
    }

    [Fact]
    public void AlwaysAllow_Instance_IsSingleton()
    {
        var a = AlwaysAllowFeatureGate.Instance;
        var b = AlwaysAllowFeatureGate.Instance;
        a.Should().BeSameAs(b);
    }

    // -----------------------------------------------------------------------
    // DenyAllFeatureGate — negative paths (FeatureNotLicensedException thrown)
    // -----------------------------------------------------------------------

    [Fact]
    public void DenyAll_IsEnabled_ReturnsFalse_ForDesigner()
    {
        var gate = new DenyAllFeatureGate();
        gate.IsEnabled(CapabilityKeys.PdfDesigner).Should().BeFalse();
    }

    [Fact]
    public void DenyAll_EnsureFeatureOrThrow_ThrowsFeatureNotLicensedException_ForDesigner()
    {
        var gate = new DenyAllFeatureGate();
        var act = () => gate.EnsureFeatureOrThrow(CapabilityKeys.PdfDesigner);
        act.Should()
           .Throw<FeatureNotLicensedException>()
           .Which.CapabilityKey.Should().Be(CapabilityKeys.PdfDesigner);
    }

    [Fact]
    public void DenyAll_EnsureFeatureOrThrow_MessageContainsKeyName()
    {
        const string key = CapabilityKeys.PdfRegistry;
        var gate = new DenyAllFeatureGate();
        var act = () => gate.EnsureFeatureOrThrow(key);
        act.Should()
           .Throw<FeatureNotLicensedException>()
           .WithMessage($"*{key}*");
    }

    [Fact]
    public void DenyAll_EnsureFeatureOrThrow_ThrowsFeatureNotLicensedException_ForCanary()
    {
        var gate = new DenyAllFeatureGate();
        var act = () => gate.EnsureFeatureOrThrow(CapabilityKeys.PdfCanary);
        act.Should()
           .Throw<FeatureNotLicensedException>()
           .Which.CapabilityKey.Should().Be(CapabilityKeys.PdfCanary);
    }

    [Fact]
    public void DenyAll_EnsureFeatureOrThrow_IsInvalidOperationException()
    {
        // FeatureNotLicensedException must inherit InvalidOperationException
        var gate = new DenyAllFeatureGate();
        var act = () => gate.EnsureFeatureOrThrow(CapabilityKeys.PdfDesigner);
        act.Should().Throw<InvalidOperationException>();
    }

    // -----------------------------------------------------------------------
    // Stub used only in tests — deny everything
    // -----------------------------------------------------------------------

    private sealed class DenyAllFeatureGate : IFeatureGate
    {
        public bool IsEnabled(string capabilityKey) => false;

        public void EnsureFeatureOrThrow(string capabilityKey)
            => throw new FeatureNotLicensedException(capabilityKey);
    }
}
