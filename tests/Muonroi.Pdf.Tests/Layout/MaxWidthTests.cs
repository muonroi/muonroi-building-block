using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// Phase 8.11 wave 8.11b — max-width / min-width parsing + clamp.
/// Guard against AngleSharp's empty-string-for-non-cascaded-properties returning 0f from ParseLength.
/// </summary>
public sealed class MaxWidthTests
{
    private static BoxTreeBuilder Builder() => new();

    [Fact]
    public void Parse_NoMaxWidthSet_SentinelMinusOne()
    {
        var node = new FakeStyledNode("div", new() { ["display"] = "block" });
        var box = Builder().Build(node);

        box.MaxWidth.Should().Be(-1f, "no max-width in cascade → sentinel, no clamp");
        box.MinWidth.Should().Be(-1f, "no min-width in cascade → sentinel, no clamp");
    }

    [Fact]
    public void Parse_MaxWidthAuto_SentinelMinusOne()
    {
        var node = new FakeStyledNode("div", new()
        {
            ["display"] = "block",
            ["max-width"] = "auto"
        });
        var box = Builder().Build(node);

        box.MaxWidth.Should().Be(-1f);
    }

    [Fact]
    public void Parse_MaxWidthNone_SentinelMinusOne()
    {
        var node = new FakeStyledNode("div", new()
        {
            ["display"] = "block",
            ["max-width"] = "none"
        });
        var box = Builder().Build(node);

        box.MaxWidth.Should().Be(-1f);
    }

    [Fact]
    public void Parse_MaxWidthExplicitPx_StoredAsPoints()
    {
        var node = new FakeStyledNode("div", new()
        {
            ["display"] = "block",
            ["max-width"] = "100px"
        });
        var box = Builder().Build(node);

        // ParseLength: 1px ≈ 0.75pt (96px/in vs 72pt/in), so 100px ≈ 75pt
        box.MaxWidth.Should().BeApproximately(75f, 0.5f);
    }

    [Fact]
    public void Parse_MinWidthExplicitPx_StoredAsPoints()
    {
        var node = new FakeStyledNode("div", new()
        {
            ["display"] = "block",
            ["min-width"] = "50px"
        });
        var box = Builder().Build(node);

        box.MinWidth.Should().BeApproximately(37.5f, 0.5f);
    }
}
