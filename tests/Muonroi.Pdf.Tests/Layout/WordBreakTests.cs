using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Helpers;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// Phase 12.4 regression: long unbreakable tokens (e.g. container numbers like
/// "ONEE0000002" or seal IDs like "ONES_EAL12133") must wrap at character boundaries
/// when CSS specifies word-break:break-all/break-word or overflow-wrap:break-word.
/// Without this, tokens overflow narrow table cells into adjacent columns — the
/// production TCIS HBCX template exhibits this exact symptom.
/// </summary>
public sealed class WordBreakTests
{
    private static LayoutContext MakeContext(float availableWidth) =>
        new()
        {
            PageWidth = availableWidth,
            PageHeight = 800f,
            AvailableWidth = availableWidth,
            CurrentY = 0f,
            CurrentPageIndex = 0,
            TotalPages = 0,
            TextMetrics = EstimatedTextMetrics.Instance,
            PageMargins = PdfMargins.Zero,
        };

    private static InlineBox Inline(string text, string? wordBreak = null) =>
        new()
        {
            Text = text,
            FontFamily = "serif",
            FontSize = 10f,
            WordBreak = wordBreak,
        };

    [Fact]
    public void WordBreak_BreakAll_SplitsLongTokenAtCharacterBoundary()
    {
        // "ONEE0000002" at 10pt × ~6pt/char ≈ 66pt; container cell ≈ 40pt → must split.
        var box = Inline("ONEE0000002", wordBreak: "break-all");
        var engine = new InlineLayoutEngine();
        var ctx = MakeContext(availableWidth: 40f);
        var output = new List<PositionedElement>();

        engine.Layout(new BoxNode[] { box }, ctx, output, pageIndex: 0);

        output.Count.Should().BeGreaterThan(1,
            because: "word-break:break-all must split the token across multiple positioned chunks");

        // No emitted chunk may exceed the available width.
        foreach (var pe in output)
            pe.Position.Width.Should().BeLessOrEqualTo(40f + 0.5f,
                because: "each character chunk must fit within the cell");

        // Concatenated chunks must equal the original text.
        string joined = string.Concat(output.Select(e => e.RenderedText));
        joined.Should().Be("ONEE0000002");
    }

    [Fact]
    public void WordBreak_BreakWord_OnlySplitsWhenTokenAloneExceedsLine()
    {
        // "ONEE0000002" alone (~66pt) > 40pt cell → must split.
        // Short normal words ("ab") on a wide line must NOT split.
        var longBox = Inline("ONEE0000002", wordBreak: "break-word");
        var engine = new InlineLayoutEngine();
        var ctx = MakeContext(availableWidth: 40f);
        var output = new List<PositionedElement>();

        engine.Layout(new BoxNode[] { longBox }, ctx, output, pageIndex: 0);

        output.Count.Should().BeGreaterThan(1,
            because: "break-word must split a token that alone exceeds the line width");

        string joined = string.Concat(output.Select(e => e.RenderedText));
        joined.Should().Be("ONEE0000002");
    }

    [Fact]
    public void WordBreak_Null_LongTokenRemainsSingleOverflowingChunk()
    {
        // Default behavior (no word-break): token renders as single chunk that overflows.
        // This is the pre-Phase-12.4 baseline that produced the visual gap.
        var box = Inline("ONEE0000002", wordBreak: null);
        var engine = new InlineLayoutEngine();
        var ctx = MakeContext(availableWidth: 40f);
        var output = new List<PositionedElement>();

        engine.Layout(new BoxNode[] { box }, ctx, output, pageIndex: 0);

        output.Should().HaveCount(1, because: "without word-break, the token is emitted as one chunk");
        output[0].RenderedText.Should().Be("ONEE0000002");
        output[0].Position.Width.Should().BeGreaterThan(40f,
            because: "the un-broken token overflows the narrow cell (legacy behavior)");
    }
}
