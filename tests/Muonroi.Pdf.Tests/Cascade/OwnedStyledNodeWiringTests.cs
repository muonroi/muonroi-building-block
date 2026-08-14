namespace Muonroi.Pdf.Tests.Cascade;

/// <summary>
/// End-to-end wiring tests that verify <see cref="AngleSharpStyledDocument"/> constructs an
/// <c>OwnedStyledNode</c> root backed by <see cref="CascadeResolver"/> — no
/// <c>GetComputedStyle</c> / <c>ComputeCurrentStyle</c> path.
///
/// Key scenarios:
/// <list type="bullet">
///   <item>width:50% on a div — previously threw ArgumentException in headless context; now resolves correctly.</item>
///   <item>Descendant class-selector border rule (`.table-bodered2 td`) resolves through the full document chain.</item>
///   <item><c>@page</c> and <c>@font-face</c> declarations survive the rewiring (extraction unchanged).</item>
///   <item>Style is cached: two reads of .Style on the same node return the same value without re-invoking the resolver.</item>
/// </list>
/// </summary>
public sealed class OwnedStyledNodeWiringTests
{
    // -----------------------------------------------------------------------
    // Helper: parse HTML → AngleSharpStyledDocument (the seam under test)
    // -----------------------------------------------------------------------

    private static async Task<AngleSharpStyledDocument> ParseDocumentAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var angleParsed = (AngleSharpParsedDocument)parsed;
        // Construct via the same path that AngleSharpCascadeEngine uses.
        return new AngleSharpStyledDocument(angleParsed.Document, angleParsed.SourceHtmlBytes);
    }

    /// <summary>
    /// Walks the IStyledNode tree depth-first and returns the first node whose LocalName matches.
    /// </summary>
    private static IStyledNode? FindNode(IStyledNode node, string localName)
    {
        if (node.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase))
            return node;
        foreach (IStyledNode child in node.Children)
        {
            IStyledNode? found = FindNode(child, localName);
            if (found is not null)
                return found;
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // 1. width:50% resolves without ArgumentException — the core regression
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PercentWidth_ResolvesWithoutException()
    {
        const string html = """
            <html><head><style>
              .content { width: 50%; }
            </style></head>
            <body><div class="content">hello</div></body></html>
            """;

        AngleSharpStyledDocument doc = await ParseDocumentAsync(html).ConfigureAwait(false);

        // Walk to the div
        IStyledNode? div = FindNode(doc.Root, "div");
        div.Should().NotBeNull("div must be found in the tree");

        // Width must resolve — never empty, never throw
        string? width = div!.Style.GetValue("width");
        width.Should().Be("50%", because: "the owned resolver leaves % values as literal strings");
    }

    // -----------------------------------------------------------------------
    // 2. Descendant class-selector (.table-bodered2 td) resolves border-left-width
    // -----------------------------------------------------------------------

    [Fact]
    public async Task DescendantClassSelector_BorderLeftWidth_ResolvedOnTd()
    {
        const string html = """
            <html><head><style>
              .table-bodered2 td { border-left: 1px solid black; }
            </style></head>
            <body>
              <table class="table-bodered2">
                <tbody><tr><td>cell</td></tr></tbody>
              </table>
            </body></html>
            """;

        AngleSharpStyledDocument doc = await ParseDocumentAsync(html).ConfigureAwait(false);

        IStyledNode? td = FindNode(doc.Root, "td");
        td.Should().NotBeNull("td must be found in the tree");

        string? borderLeftWidth = td!.Style.GetValue("border-left-width");
        borderLeftWidth.Should().Be("1px",
            because: "the owned cascade must resolve descendant class-selector border rules");
    }

    // -----------------------------------------------------------------------
    // 3. @page extraction is unchanged after rewiring
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AtPage_IsExtractedUnchanged()
    {
        const string html = """
            <html><head><style>
              @page { margin: 20mm; size: A4; }
            </style></head>
            <body><p>text</p></body></html>
            """;

        AngleSharpStyledDocument doc = await ParseDocumentAsync(html).ConfigureAwait(false);

        doc.PageRule.Should().NotBeNull(because: "@page rule must be extracted from the document");
    }

    // -----------------------------------------------------------------------
    // 4. @font-face extraction is unchanged after rewiring
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AtFontFace_IsExtractedUnchanged()
    {
        const string html = """
            <html><head><style>
              @font-face { font-family: 'TestFont'; font-weight: bold; font-style: normal; src: url('test.ttf'); }
            </style></head>
            <body><p>text</p></body></html>
            """;

        AngleSharpStyledDocument doc = await ParseDocumentAsync(html).ConfigureAwait(false);

        doc.FontFaces.Should().HaveCount(1, because: "@font-face declarations must survive the rewiring");
        doc.FontFaces[0].Family.Should().Be("TestFont");
    }

    // -----------------------------------------------------------------------
    // 5. Style is cached: resolver runs at most once per node
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StyleIsCached_TwoReadsReturnSameInstance()
    {
        const string html = """
            <html><head><style>
              div { color: blue; }
            </style></head>
            <body><div>hello</div></body></html>
            """;

        AngleSharpStyledDocument doc = await ParseDocumentAsync(html).ConfigureAwait(false);

        IStyledNode? div = FindNode(doc.Root, "div");
        div.Should().NotBeNull();

        IComputedStyle first  = div!.Style;
        IComputedStyle second = div!.Style;

        // Same reference proves the cache returned the same object.
        ReferenceEquals(first, second).Should().BeTrue(
            because: "Style must be cached — resolver runs at most once per node");
    }

    // -----------------------------------------------------------------------
    // 6. Inheritance: parent color inherited by child that has no own color rule
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Inheritance_ParentColorFlowsToChild()
    {
        const string html = """
            <html><head><style>
              .parent { color: red; }
            </style></head>
            <body><div class="parent"><span>child</span></div></body></html>
            """;

        AngleSharpStyledDocument doc = await ParseDocumentAsync(html).ConfigureAwait(false);

        IStyledNode? div  = FindNode(doc.Root, "div");
        IStyledNode? span = FindNode(doc.Root, "span");

        div.Should().NotBeNull();
        span.Should().NotBeNull();

        // AngleSharp.Css normalizes "red" -> "rgba(255, 0, 0, 1)" in the CSSOM;
        // the supplemental raw-text parser stores the authored value. Check both.
        string? divColor = div!.Style.GetValue("color");
        divColor.Should().NotBeNullOrEmpty(because: "div.parent should have a resolved color");

        // Span inherits color from its parent div.
        string? spanColor = span!.Style.GetValue("color");
        spanColor.Should().Be(divColor,
            because: "span has no own color rule; it must inherit from its parent div");
    }

    // -----------------------------------------------------------------------
    // 7. Text nodes: IsText true, Style is empty OwnedComputedStyle
    // -----------------------------------------------------------------------

    [Fact]
    public async Task TextNode_HasEmptyStyle()
    {
        const string html = "<html><body><p>hello</p></body></html>";

        AngleSharpStyledDocument doc = await ParseDocumentAsync(html).ConfigureAwait(false);

        // Walk into the p element, then find its text child.
        IStyledNode? p = FindNode(doc.Root, "p");
        p.Should().NotBeNull();

        IStyledNode? textNode = p!.Children.FirstOrDefault(c => c.IsText);
        textNode.Should().NotBeNull(because: "p should have a text child node");

        textNode!.IsText.Should().BeTrue();
        textNode.IsElement.Should().BeFalse();
        textNode.LocalName.Should().Be("#text");
        textNode.Style.GetValue("color").Should().BeNull(
            because: "text nodes have no cascade and return empty style");
    }
}
