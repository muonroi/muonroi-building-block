namespace Muonroi.Pdf.Tests.Layout;

public sealed class InlineLayoutTests
{
    private static LayoutContext MakeContext(float availableWidth = 400f, float startY = 0f) =>
        new()
        {
            PageWidth = availableWidth,
            PageHeight = 800f,
            AvailableWidth = availableWidth,
            CurrentY = startY,
            CurrentPageIndex = 0,
            TotalPages = 0,
            TextMetrics = EstimatedTextMetrics.Instance,
            PageMargins = PdfMargins.Zero,
        };

    private static InlineBox MakeInlineBox(string text, float fontSize, string verticalAlign = "baseline") =>
        new()
        {
            Text = text,
            FontFamily = "serif",
            FontSize = fontSize,
            VerticalAlign = verticalAlign,
        };

    // SC2: vertical-align:top → y-offset within line = 0
    [Fact]
    public void InlineBox_VerticalAlignTop_PlacedAtLineTop()
    {
        // 12px and 24px inline boxes on the same line
        // line height = max(12*1.2, 24*1.2) = 28.8pt
        // vertical-align:top → y-offset = 0 (placed at line top)
        var smallBox = MakeInlineBox("A", 12f, verticalAlign: "top");
        var largeBox = MakeInlineBox("B", 24f, verticalAlign: "baseline");

        var engine = new InlineLayoutEngine();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        engine.Layout(new BoxNode[] { smallBox, largeBox }, ctx, output, pageIndex: 0);

        var smallPe = output.First(e => e.Source == smallBox);
        smallPe.Position.Y.Should().BeApproximately(0f, precision: 0.1f,
            because: "vertical-align:top places the box at y=lineY (no offset)");
    }

    // SC2: vertical-align:middle → y-offset = (lineHeight - boxHeight) / 2
    [Fact]
    public void InlineBox_VerticalAlignMiddle_PlacedAtLineMidpoint()
    {
        // Large box (24px) defines line height = 28.8pt
        // Small box (12px, height=14.4pt) with vertical-align:middle
        // yOffset = (28.8 - 14.4) / 2 = 7.2pt
        float lineHeight = 24f * 1.2f;     // 28.8pt from 24px font
        float smallBoxHeight = 12f * 1.2f; // 14.4pt
        float expectedOffset = (lineHeight - smallBoxHeight) / 2f; // 7.2pt

        var smallBox = MakeInlineBox("A", 12f, verticalAlign: "middle");
        var largeBox = MakeInlineBox("B", 24f, verticalAlign: "baseline");

        var engine = new InlineLayoutEngine();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        engine.Layout(new BoxNode[] { smallBox, largeBox }, ctx, output, pageIndex: 0);

        var smallPe = output.First(e => e.Source == smallBox);
        smallPe.Position.Y.Should().BeApproximately(expectedOffset, precision: 0.1f,
            because: "vertical-align:middle offsets the box by (lineHeight - boxHeight) / 2");
    }

    // SC2: baseline alignment — larger font sets the line's baseline; smaller shifts down
    [Fact]
    public void MixedFontSizes_SmallerBox_ShiftedDownToMatchBaseline()
    {
        // 12px: ascender = 9.6pt; 24px: ascender = 19.2pt
        // lineAscender = max(9.6, 19.2) = 19.2pt
        // 24px box yOffset = lineAscender - 24*0.8 = 19.2 - 19.2 = 0
        // 12px box yOffset = lineAscender - 12*0.8 = 19.2 - 9.6 = 9.6pt
        float lineAscender = 24f * 0.8f; // 19.2pt (dominant)
        float smallAscender = 12f * 0.8f; // 9.6pt
        float expectedSmallOffset = lineAscender - smallAscender; // 9.6pt

        var smallBox = MakeInlineBox("A", 12f, verticalAlign: "baseline");
        var largeBox = MakeInlineBox("B", 24f, verticalAlign: "baseline");

        var engine = new InlineLayoutEngine();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        engine.Layout(new BoxNode[] { smallBox, largeBox }, ctx, output, pageIndex: 0);

        var smallPe = output.First(e => e.Source == smallBox);
        smallPe.Position.Y.Should().BeApproximately(expectedSmallOffset, precision: 0.1f,
            because: "smaller font box is shifted down so baselines align with the dominant font");
    }

    [Fact]
    public void InlineLayout_TotalHeightEqualsLineHeight()
    {
        // Single-line inline content: total height = line height of the font
        var box = MakeInlineBox("Hello", 12f);
        float expectedLineHeight = 12f * 1.2f; // 14.4pt

        var engine = new InlineLayoutEngine();
        var ctx = MakeContext();
        var output = new List<PositionedElement>();
        float h = engine.Layout(new BoxNode[] { box }, ctx, output, pageIndex: 0);

        h.Should().BeApproximately(expectedLineHeight, precision: 0.1f,
            because: "single-line layout height equals the font's line height");
    }

    [Fact]
    public void VietnamesePlusLatin_MixedText_ProducesOneElementPerSpaceSeparatedToken()
    {
        var box = MakeInlineBox("Xin chào world", 12f);

        var engine = new InlineLayoutEngine();
        var ctx = MakeContext(availableWidth: 500f);
        var output = new List<PositionedElement>();
        engine.Layout(new BoxNode[] { box }, ctx, output, pageIndex: 0);

        output.Count.Should().Be(3,
            because: "InlineLayoutEngine splits on spaces, producing one PositionedElement per token regardless of script");
    }
}
