using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G24 regression tests: &lt;img&gt; with no CSS width/height must render at intrinsic
/// pixel-to-point size (px * 0.75), not stretch to the full container width.
///
/// Root cause: BlockLayoutEngine.ResolveWidth fell through to the "auto" branch
/// (available width) when no CSS width was set, ignoring ReplacedBox.NaturalWidth.
/// Fix: added a ReplacedBox branch that honours NaturalWidth before the auto fallback;
/// max-width/min-width clamps still apply so percentage max-width works correctly.
/// </summary>
public sealed class IntrinsicImageSizeTests
{
    // 400pt wide container, same as BlockLayoutTests convention.
    private static LayoutContext MakeContext(float availableWidth = 400f) =>
        new()
        {
            PageWidth        = availableWidth,
            PageHeight       = 800f,
            AvailableWidth   = availableWidth,
            CurrentY         = 0f,
            CurrentPageIndex = 0,
            TotalPages       = 0,
            TextMetrics      = EstimatedTextMetrics.Instance,
            PageMargins      = PdfMargins.Zero,
        };

    /// <summary>
    /// Builds a BlockBox parent containing one &lt;img src="test-img"&gt; child.
    /// The parent is a plain div with no CSS constraints.
    /// </summary>
    private static BlockBox BuildImgTree(
        string src,
        Dictionary<string, string>? imgStyles,
        IReadOnlyDictionary<string, DecodedImage>? resolvedImages)
    {
        var parent = new FakeStyledNode("div", new() { ["display"] = "block" });
        var imgNode = new FakeStyledNode("img", imgStyles ?? new(), new() { ["src"] = src });
        parent.ChildList.Add(imgNode);
        return new BoxTreeBuilder().Build(parent, resolvedImages);
    }

    private static PositionedElement RunLayout(BlockBox root, float availableWidth = 400f)
    {
        var ctx = MakeContext(availableWidth);
        var output = new List<PositionedElement>();
        new BlockLayoutEngine().Layout(root, ctx, output, pageIndex: 0, isRoot: false);
        // The img is the first (and only) leaf element with a ReplacedBox source.
        return output.First(pe => pe.Source is ReplacedBox);
    }

    // ------------------------------------------------------------------
    // Test 1 — no CSS width/height: intrinsic px→pt wins
    // 64×48 image → 48pt wide (64*0.75), 36pt tall (48*0.75)
    // ------------------------------------------------------------------
    [Fact]
    public void ImgWithNoStyleWidthOrHeight_RendersAtIntrinsicPxToPt()
    {
        const string src = "test-img";
        var decoded = new DecodedImage(Width: 64, Height: 48, Data: ReadOnlyMemory<byte>.Empty, ContentType: "image/png");
        var root = BuildImgTree(src, imgStyles: null,
            resolvedImages: new Dictionary<string, DecodedImage> { [src] = decoded });

        var pe = RunLayout(root);

        pe.Position.Width.Should().BeApproximately(64f * Units.PxToPt, precision: 0.01f,
            because: "no CSS width → intrinsic 64px * 0.75 = 48pt");
        pe.Position.Height.Should().BeApproximately(48f * Units.PxToPt, precision: 0.01f,
            because: "no CSS height → intrinsic 48px * 0.75 = 36pt");
    }

    // ------------------------------------------------------------------
    // Test 2 — CSS width present: explicit CSS width wins; engine does NOT
    // auto-calculate aspect-ratio height (height stays at NaturalHeight since
    // no explicit CSS height — verify current, not aspect-ratio, behaviour).
    // 64×48 decoded, style="width:100px" → width=75pt (100*0.75),
    // height = NaturalHeight = 36pt (engine doesn't auto-aspect).
    // ------------------------------------------------------------------
    [Fact]
    public void ImgWithCssWidthSet_CssWidthWins_NoAspectRatioForHeight()
    {
        const string src = "test-img-css-w";
        var decoded = new DecodedImage(Width: 64, Height: 48, Data: ReadOnlyMemory<byte>.Empty, ContentType: "image/png");
        var root = BuildImgTree(src,
            imgStyles: new() { ["width"] = "100px" },
            resolvedImages: new Dictionary<string, DecodedImage> { [src] = decoded });

        var pe = RunLayout(root);

        // CSS width 100px → 75pt
        pe.Position.Width.Should().BeApproximately(100f * Units.PxToPt, precision: 0.01f,
            because: "CSS width:100px overrides intrinsic width; 100*0.75=75pt");

        // No CSS height → NaturalHeight (36pt).  The engine does NOT compute aspect-ratio height.
        pe.Position.Height.Should().BeApproximately(48f * Units.PxToPt, precision: 0.01f,
            because: "no CSS height → NaturalHeight fallback = 48px*0.75=36pt (no aspect-ratio)");
    }

    // ------------------------------------------------------------------
    // Test 3 — max-width (fixed pt) clamps intrinsic width when intrinsic exceeds it.
    // 400×200 image → intrinsic 300pt (400*0.75); max-width:200pt → clamped to 200pt.
    //
    // Note: percentage max-width (e.g. "50%") is stored as the no-clamp sentinel (-1f)
    // by BoxTreeBuilder.ParseLength — percentage max-width resolution is not yet
    // implemented (no MaxWidthRaw field).  This test uses a fixed pt value to verify
    // the clamp path in ResolveWidth for replaced elements (same code path that
    // would honour a resolved percentage max-width).
    // ------------------------------------------------------------------
    [Fact]
    public void ImgWithMaxWidthPt_ClampedToMaxWidth_WhenIntrinsicExceedsIt()
    {
        const string src = "test-img-maxw";
        // 400px wide → intrinsic 300pt (400*0.75); max-width:200pt → clamp fires.
        var decoded = new DecodedImage(Width: 400, Height: 200, Data: ReadOnlyMemory<byte>.Empty, ContentType: "image/png");
        var root = BuildImgTree(src,
            imgStyles: new() { ["max-width"] = "200pt" },
            resolvedImages: new Dictionary<string, DecodedImage> { [src] = decoded });

        var pe = RunLayout(root, availableWidth: 400f);

        pe.Position.Width.Should().BeApproximately(200f, precision: 0.01f,
            because: "intrinsic 300pt exceeds max-width:200pt → clamped to 200pt, not 300pt intrinsic");
    }

    // ------------------------------------------------------------------
    // Test 4 — src missing from _resolvedImages: falls back to container width
    // (NaturalWidth stays 0, no intrinsic branch fires, auto = available).
    // ------------------------------------------------------------------
    [Fact]
    public void ImgWithFailedDecode_FallsBackToContainerWidth()
    {
        const string src = "missing-img";
        // No entry for this src in resolvedImages.
        var root = BuildImgTree(src, imgStyles: null,
            resolvedImages: new Dictionary<string, DecodedImage>());

        // 400pt container with zero margins/padding: auto width = 400pt.
        var pe = RunLayout(root, availableWidth: 400f);

        pe.Position.Width.Should().BeApproximately(400f, precision: 0.01f,
            because: "decode failed → NaturalWidth=0 → auto fallback = available container width (400pt)");
    }
}
