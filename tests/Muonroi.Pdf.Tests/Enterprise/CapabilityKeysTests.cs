namespace Muonroi.Pdf.Tests.Enterprise;

/// <summary>
/// Sanity tests that <see cref="CapabilityKeys"/> constants match the exact string
/// literals mandated by the capability-key naming specification.
/// </summary>
public sealed class CapabilityKeysTests
{
    [Fact]
    public void PdfDesigner_EqualsLiteral()
    {
        CapabilityKeys.PdfDesigner.Should().Be("pdf.designer");
    }

    [Fact]
    public void PdfRegistry_EqualsLiteral()
    {
        CapabilityKeys.PdfRegistry.Should().Be("pdf.registry");
    }

    [Fact]
    public void PdfCanary_EqualsLiteral()
    {
        CapabilityKeys.PdfCanary.Should().Be("pdf.canary");
    }

    [Fact]
    public void AllKeys_AreLowercase()
    {
        CapabilityKeys.PdfDesigner.Should().Be(CapabilityKeys.PdfDesigner.ToLowerInvariant());
        CapabilityKeys.PdfRegistry.Should().Be(CapabilityKeys.PdfRegistry.ToLowerInvariant());
        CapabilityKeys.PdfCanary.Should().Be(CapabilityKeys.PdfCanary.ToLowerInvariant());
    }

    [Fact]
    public void AllKeys_FollowDomainDotFeaturePattern()
    {
        // Each key must contain exactly one dot, splitting <domain> from <feature>
        static void AssertPattern(string key)
        {
            key.Should().MatchRegex(@"^[a-z]+\.[a-z_]+$",
                because: $"key '{key}' must follow <domain>.<feature> naming convention");
        }

        AssertPattern(CapabilityKeys.PdfDesigner);
        AssertPattern(CapabilityKeys.PdfRegistry);
        AssertPattern(CapabilityKeys.PdfCanary);
    }

    [Fact]
    public void AllKeys_HavePdfDomainPrefix()
    {
        CapabilityKeys.PdfDesigner.Should().StartWith("pdf.");
        CapabilityKeys.PdfRegistry.Should().StartWith("pdf.");
        CapabilityKeys.PdfCanary.Should().StartWith("pdf.");
    }

    [Fact]
    public void AllKeys_AreDistinct()
    {
        var keys = new[]
        {
            CapabilityKeys.PdfDesigner,
            CapabilityKeys.PdfRegistry,
            CapabilityKeys.PdfCanary,
        };

        keys.Should().OnlyHaveUniqueItems();
    }
}
