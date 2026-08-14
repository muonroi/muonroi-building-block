namespace Muonroi.Pdf.Tests.Cascade;

/// <summary>
/// Phase 14 (Group A): <c>@page</c> margin-box parsing. AngleSharp.Css does not surface
/// <c>@top-*/@bottom-*</c> at-rules through its object model, so <see cref="AngleSharpPageRule"/>
/// extracts them from the raw <c>&lt;style&gt;</c> source. <c>counter(page)/counter(pages)</c>
/// tokens are preserved verbatim for downstream per-page substitution.
/// </summary>
public sealed class MarginBoxParsingTests
{
    private static async Task<IPageRule?> PageRuleAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var angleParsed = (AngleSharpParsedDocument)parsed;
        var doc = new AngleSharpStyledDocument(angleParsed.Document, angleParsed.SourceHtmlBytes);
        return doc.PageRule;
    }

    [Fact]
    public async Task TopCenter_ContentWithCounter_IsFlattenedAndPreservesCounterTokens()
    {
        const string html = """
            <html><head><style>
              @page { margin: 20mm; @top-center { content: "Trang " counter(page) "/" counter(pages); } }
            </style></head><body><p>x</p></body></html>
            """;

        IPageRule? rule = await PageRuleAsync(html).ConfigureAwait(false);

        rule.Should().NotBeNull();
        rule!.TopCenterHtml.Should().Be("Trang counter(page)/counter(pages)",
            because: "quoted strings flatten to text and counter() tokens are kept verbatim");
        rule.HasTopMarginBoxes.Should().BeTrue();
        rule.HasBottomMarginBoxes.Should().BeFalse();
    }

    [Fact]
    public async Task AllSixBoxes_AreExtractedIntoTheirSlots()
    {
        const string html = """
            <html><head><style>
              @page {
                @top-left { content: "TL"; }
                @top-center { content: "TC"; }
                @top-right { content: "TR"; }
                @bottom-left { content: "BL"; }
                @bottom-center { content: "BC"; }
                @bottom-right { content: "BR"; }
              }
            </style></head><body><p>x</p></body></html>
            """;

        IPageRule? rule = await PageRuleAsync(html).ConfigureAwait(false);

        rule.Should().NotBeNull();
        rule!.TopLeftHtml.Should().Be("TL");
        rule.TopCenterHtml.Should().Be("TC");
        rule.TopRightHtml.Should().Be("TR");
        rule.BottomLeftHtml.Should().Be("BL");
        rule.BottomCenterHtml.Should().Be("BC");
        rule.BottomRightHtml.Should().Be("BR");
        rule.HasTopMarginBoxes.Should().BeTrue();
        rule.HasBottomMarginBoxes.Should().BeTrue();
    }

    [Fact]
    public async Task AbsentMarginBoxes_AreNull()
    {
        const string html = """
            <html><head><style>
              @page { margin: 15mm; @bottom-right { content: counter(page); } }
            </style></head><body><p>x</p></body></html>
            """;

        IPageRule? rule = await PageRuleAsync(html).ConfigureAwait(false);

        rule.Should().NotBeNull();
        rule!.BottomRightHtml.Should().Be("counter(page)");
        rule.TopLeftHtml.Should().BeNull();
        rule.TopCenterHtml.Should().BeNull();
        rule.TopRightHtml.Should().BeNull();
        rule.BottomLeftHtml.Should().BeNull();
        rule.BottomCenterHtml.Should().BeNull();
        rule.HasTopMarginBoxes.Should().BeFalse();
        rule.HasBottomMarginBoxes.Should().BeTrue();
    }

    [Fact]
    public async Task TopLeft_DoesNotMatchTopLeftCorner()
    {
        // @top-left-corner must not be picked up by the @top-left slot (prefix guard).
        const string html = """
            <html><head><style>
              @page { @top-left-corner { content: "CORNER"; } @top-center { content: "MID"; } }
            </style></head><body><p>x</p></body></html>
            """;

        IPageRule? rule = await PageRuleAsync(html).ConfigureAwait(false);

        rule.Should().NotBeNull();
        rule!.TopLeftHtml.Should().BeNull(because: "@top-left-corner is a different box than @top-left");
        rule.TopCenterHtml.Should().Be("MID");
    }
}
