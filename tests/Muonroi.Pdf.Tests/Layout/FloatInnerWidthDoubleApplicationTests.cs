using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G19 + G21 regression tests: float box WidthRaw="X%" must not be applied a second time
/// against the already-narrowed measureCtx.AvailableWidth inside the inner Layout call.
/// </summary>
public sealed class FloatInnerWidthDoubleApplicationTests
{
    // A4 page content width after 10mm margins on each side:
    // 595pt (A4) - 2 * (10mm * 2.835 pt/mm) ≈ 538pt.
    // The test uses this as the canonical page available width.
    private const float PageContentWidth = 538f;

    private static LayoutContext MakeRootContext(float availableWidth = PageContentWidth) =>
        new()
        {
            PageWidth = availableWidth,
            PageHeight = 841f,
            AvailableWidth = availableWidth,
            CurrentY = 0f,
            CurrentPageIndex = 0,
            TotalPages = 0,
            TextMetrics = EstimatedTextMetrics.Instance,
            PageMargins = PdfMargins.Zero,
            Exclusions = new List<FloatExclusion>(),
        };

    // -------------------------------------------------------------------------
    // G19: div.w-30.float-left — expect float width ≈ 30% of 538pt ≈ 161pt
    // -------------------------------------------------------------------------

    [Fact]
    public void FloatLeft_30Percent_WidthIsResolvedAgainstPageNotAgainstItself()
    {
        // Arrange: root block > float block (w=30%, float-left) > inline text
        var floatBlock = new BlockBox
        {
            WidthRaw = "30%",
            FloatValue = "left",
        };
        var textInline = new InlineBox
        {
            Text = "Mã lô: LO12345",
            FontFamily = "serif",
            FontSize = 12f,
        };
        floatBlock.Children.Add(textInline);

        var root = new BlockBox();
        root.Children.Add(floatBlock);

        var engine = new BlockLayoutEngine();
        var tableEngine = new TableLayoutEngine(engine, engine.InlineEngine);
        engine.TableEngine = tableEngine;

        var output = new List<PositionedElement>();
        engine.Layout(root, MakeRootContext(), output, pageIndex: 0, isRoot: true);

        // Assert: the PositionedElement for the float box must have Width ≈ 30% of 538pt ≈ 161pt.
        // Before the fix it was ≈ 30% of 161pt ≈ 48pt (double-applied).
        float expectedWidth = PageContentWidth * 0.30f; // ≈ 161.4pt

        var floatElement = output.FirstOrDefault(e => e.Source == floatBlock);
        floatElement.Should().NotBeNull(because: "float block must emit a PositionedElement");
        floatElement!.Position.Width.Should().BeApproximately(expectedWidth, precision: 1f,
            because: "float width must be 30% of page content width, not 30% of 30% of page");
    }

    [Fact]
    public void FloatLeft_30Percent_InlineTextFitsOnOneLine()
    {
        // Before the fix: available width for inline was ~48pt (30% of 161pt — double-applied).
        // "Mã lô: LO12345" has 3 words. At 48pt available:
        //   "Mã"(14.4pt) + space(7.2pt) + "lô:"(21.6pt) = 43.2pt fits line 1,
        //   "LO12345"(50.4pt) > 48pt-43.2pt remaining → wraps to line 2.
        //   Result: words appear on TWO different Y coordinates.
        // After the fix: available width is ~161pt — all 3 words fit on ONE line (same Y).

        var floatBlock = new BlockBox
        {
            WidthRaw = "30%",
            FloatValue = "left",
        };
        var textInline = new InlineBox
        {
            Text = "Mã lô: LO12345",
            FontFamily = "serif",
            FontSize = 12f,
        };
        floatBlock.Children.Add(textInline);

        var root = new BlockBox();
        root.Children.Add(floatBlock);

        var engine = new BlockLayoutEngine();
        var tableEngine = new TableLayoutEngine(engine, engine.InlineEngine);
        engine.TableEngine = tableEngine;

        var output = new List<PositionedElement>();
        engine.Layout(root, MakeRootContext(), output, pageIndex: 0, isRoot: true);

        // All word elements from the inline text must share the same Y coordinate (one line).
        var inlineElements = output.Where(e => e.Source == textInline).ToList();
        inlineElements.Should().NotBeEmpty(because: "inline text inside float must produce positioned output");

        float firstY = inlineElements[0].Position.Y;
        inlineElements.Should().AllSatisfy(el =>
            el.Position.Y.Should().BeApproximately(firstY, precision: 0.1f,
                because: "all words must be on the same line when float width is correctly resolved"),
            because: "with ~161pt available all words of 'Mã lô: LO12345' fit on one line");
    }

    // -------------------------------------------------------------------------
    // G21: div.w-25.float-left — expect float width ≈ 25% of 538pt ≈ 134pt
    // -------------------------------------------------------------------------

    [Fact]
    public void FloatLeft_25Percent_WidthIsResolvedAgainstPageNotAgainstItself()
    {
        var floatBlock = new BlockBox
        {
            WidthRaw = "25%",
            FloatValue = "left",
        };
        var textInline = new InlineBox
        {
            Text = "Số điện thoại: 0901234567",
            FontFamily = "serif",
            FontSize = 12f,
        };
        floatBlock.Children.Add(textInline);

        var root = new BlockBox();
        root.Children.Add(floatBlock);

        var engine = new BlockLayoutEngine();
        var tableEngine = new TableLayoutEngine(engine, engine.InlineEngine);
        engine.TableEngine = tableEngine;

        var output = new List<PositionedElement>();
        engine.Layout(root, MakeRootContext(), output, pageIndex: 0, isRoot: true);

        float expectedWidth = PageContentWidth * 0.25f; // ≈ 134.5pt

        var floatElement = output.FirstOrDefault(e => e.Source == floatBlock);
        floatElement.Should().NotBeNull(because: "float block must emit a PositionedElement");
        floatElement!.Position.Width.Should().BeApproximately(expectedWidth, precision: 1f,
            because: "float width must be 25% of page content width (not 25% of 25% ≈ 6.25%)");
    }

    // -------------------------------------------------------------------------
    // Regression guard: non-float blocks with width:% must NOT be affected
    // -------------------------------------------------------------------------

    [Fact]
    public void NonFloat_PercentWidth_NotAffectedByFix()
    {
        // A normal (non-floated) block with WidthRaw="50%" should still resolve to 50% of
        // the available width, and the fix must not alter its behavior.
        var normalBlock = new BlockBox
        {
            WidthRaw = "50%",
            // FloatValue is null — not a float
        };

        var root = new BlockBox();
        root.Children.Add(normalBlock);

        var engine = new BlockLayoutEngine();
        var tableEngine = new TableLayoutEngine(engine, engine.InlineEngine);
        engine.TableEngine = tableEngine;

        var output = new List<PositionedElement>();
        engine.Layout(root, MakeRootContext(), output, pageIndex: 0, isRoot: true);

        float expectedWidth = PageContentWidth * 0.50f; // ≈ 269pt

        var blockElement = output.FirstOrDefault(e => e.Source == normalBlock);
        blockElement.Should().NotBeNull(because: "normal block must emit a PositionedElement");
        blockElement!.Position.Width.Should().BeApproximately(expectedWidth, precision: 1f,
            because: "non-float percent widths must still resolve against parent available width");
    }
}
