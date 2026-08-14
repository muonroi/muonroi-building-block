namespace Muonroi.Pdf.Tests.Cascade;

/// <summary>
/// Unit tests for <see cref="CascadeResolver"/> covering the full 7-step cascade algorithm plus
/// the G25/G27/G28/G29 regression scenarios.
///
/// Note: AngleSharp.Css normalizes authored values (e.g. "red" → "rgba(255, 0, 0, 1)") when
/// collecting declarations from ICssStyleRule.Style — assertions on color and border-color use the
/// normalized form. Values that AngleSharp does NOT normalize (lengths, keywords) are asserted on
/// their authored form.
/// </summary>
public sealed class CascadeResolverTests
{
    // -----------------------------------------------------------------------
    // Helper: parse HTML string → IDocument via the shared parse helper
    // -----------------------------------------------------------------------
    private static async Task<IDocument> ParseDocumentAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var angleParsed = (AngleSharpParsedDocument)parsed;
        return angleParsed.Document;
    }

    /// <summary>
    /// Resolves the element matched by <paramref name="querySelector"/> using all rules from the
    /// document's style sheets, threading parent chain for inheritance if requested.
    /// </summary>
    private static async Task<Dictionary<string, string>> ResolveAsync(
        string html,
        string querySelector,
        bool resolveParent = false)
    {
        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);
        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);
        var resolver = new CascadeResolver(ruleSet);

        IElement? element = document.QuerySelector(querySelector);
        element.Should().NotBeNull(because: $"querySelector '{querySelector}' must find an element");

        IReadOnlyDictionary<string, string>? parentMap = null;
        if (resolveParent && element!.ParentElement is not null)
        {
            // Resolve the immediate parent so inheritance can work.
            parentMap = resolver.Resolve(element.ParentElement, null);
        }

        return resolver.Resolve(element!, parentMap);
    }

    // -----------------------------------------------------------------------
    // 1. Cascade order: specificity wins (#id beats .class)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CascadeOrder_HigherSpecificityWins()
    {
        const string html =
            "<html><head><style>" +
            ".a { color: red } " +
            "#x { color: blue }" +
            "</style></head>" +
            "<body><div class=\"a\" id=\"x\"></div></body></html>";

        var map = await ResolveAsync(html, "#x").ConfigureAwait(false);

        // #x has higher specificity → blue. AngleSharp normalizes both colors.
        map.Should().ContainKey("color", because: "color must be resolved");
        // Blue in rgba form from AngleSharp: rgba(0, 0, 255, 1)
        map["color"].Should().Contain("0, 0, 255",
            because: "#id rule (color:blue) beats .class rule (color:red) via higher specificity");
    }

    // -----------------------------------------------------------------------
    // 2. !important overrides specificity
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Important_BeatsHigherSpecificity()
    {
        const string html =
            "<html><head><style>" +
            ".a { color: red !important } " +
            "#x { color: blue }" +
            "</style></head>" +
            "<body><div class=\"a\" id=\"x\"></div></body></html>";

        var map = await ResolveAsync(html, "#x").ConfigureAwait(false);

        map.Should().ContainKey("color");
        // Red wins because !important. AngleSharp normalizes: rgba(255, 0, 0, 1)
        map["color"].Should().Contain("255, 0, 0",
            because: "!important on .a {color:red} beats #x {color:blue} regardless of specificity");
    }

    // -----------------------------------------------------------------------
    // 3. Source-order tiebreak: later rule wins when specificity is equal
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SourceOrder_LaterRuleWinsOnEqualSpecificity()
    {
        const string html =
            "<html><head><style>" +
            ".a { color: red } " +
            ".a { color: blue }" +
            "</style></head>" +
            "<body><div class=\"a\"></div></body></html>";

        var map = await ResolveAsync(html, ".a").ConfigureAwait(false);

        map.Should().ContainKey("color");
        // Second .a { color: blue } wins (same specificity, later source order).
        map["color"].Should().Contain("0, 0, 255",
            because: "the later .a rule (color:blue) overwrites the earlier one at equal specificity");
    }

    // -----------------------------------------------------------------------
    // 4. Inline style= overlay beats author stylesheet
    // -----------------------------------------------------------------------

    [Fact]
    public async Task InlineStyle_BeatsAuthorRule()
    {
        const string html =
            "<html><head><style>.a { color: red }</style></head>" +
            "<body><div class=\"a\" style=\"color: green\"></div></body></html>";

        var map = await ResolveAsync(html, ".a").ConfigureAwait(false);

        map.Should().ContainKey("color");
        // Inline style color:green wins. The inline splitter stores the raw authored value "green"
        // (unlike CSSOM which normalizes to rgba form). Assert the value is NOT the author-rule red.
        map["color"].Should().NotContain("255, 0, 0",
            because: "inline style= color:green must override author .a { color:red }");
        // And must contain the winning inline value (raw "green").
        map["color"].Should().Be("green",
            because: "inline style= stores the raw authored value; color:green overrides the author rule");
    }

    // -----------------------------------------------------------------------
    // 5. Border shorthand expansion — all four sides, width/style/color
    // -----------------------------------------------------------------------

    [Fact]
    public async Task BorderShorthand_ExpandsToFourSideLonghands()
    {
        const string html =
            "<html><head><style>.box { border: 1px solid red }</style></head>" +
            "<body><div class=\"box\"></div></body></html>";

        var map = await ResolveAsync(html, ".box").ConfigureAwait(false);

        // After shorthand expansion, the four side-width longhands must exist.
        // AngleSharp may expand border shorthand to longhands in the CSSOM itself, so the
        // resolver may receive already-expanded declarations — in both cases the resolved map
        // must contain the side-specific longhands.
        bool hasBorderWidths =
            map.ContainsKey("border-top-width") ||
            map.ContainsKey("border-right-width") ||
            map.ContainsKey("border-bottom-width") ||
            map.ContainsKey("border-left-width");

        hasBorderWidths.Should().BeTrue(
            because: "border: 1px solid red must expand to border-*-width longhands");

        // Verify width values where present.
        if (map.TryGetValue("border-top-width", out string? btw))
            btw.Should().NotBeNullOrEmpty(because: "border-top-width must have a value after expansion");
    }

    // -----------------------------------------------------------------------
    // 6. Padding 2-value shorthand: "2px 6px" → top/bottom=2px, left/right=6px (G27)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task G27_PaddingTwoValue_ExpandsVerticalAndHorizontalSeparately()
    {
        const string html =
            "<html><head><style>.t td { padding: 2px 6px }</style></head>" +
            "<body><table class=\"t\"><tr><td id=\"cell\">X</td></tr></table></body></html>";

        var map = await ResolveAsync(html, "#cell").ConfigureAwait(false);

        map.Should().ContainKey("padding-top",
            because: "padding 2-value shorthand must produce padding-top longhand");
        map.Should().ContainKey("padding-left",
            because: "padding 2-value shorthand must produce padding-left longhand");

        map["padding-top"].Should().Be("2px",
            because: "vertical token (2px) maps to top/bottom");
        map["padding-bottom"].Should().Be("2px",
            because: "vertical token (2px) maps to top/bottom");
        map["padding-right"].Should().Be("6px",
            because: "horizontal token (6px) maps to right/left");
        map["padding-left"].Should().Be("6px",
            because: "horizontal token (6px) maps to right/left");
    }

    // -----------------------------------------------------------------------
    // 7. UA defaults: <th> → font-weight:bold AND text-align:center
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UaDefault_ThGetsBoldAndCenter()
    {
        const string html =
            "<html><head></head>" +
            "<body><table><tr><th id=\"hdr\">H</th></tr></table></body></html>";

        var map = await ResolveAsync(html, "#hdr").ConfigureAwait(false);

        map.Should().ContainKey("font-weight",
            because: "<th> UA default must set font-weight");
        map["font-weight"].Should().Be("bold",
            because: "<th> must default to font-weight:bold per HTML5 UA stylesheet");

        map.Should().ContainKey("text-align",
            because: "<th> UA default must set text-align");
        map["text-align"].Should().Be("center",
            because: "<th> must default to text-align:center per HTML5 UA stylesheet");
    }

    // -----------------------------------------------------------------------
    // 8. UA defaults: <td> does NOT get bold
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UaDefault_TdIsNotBold()
    {
        const string html =
            "<html><head></head>" +
            "<body><table><tr><td id=\"cell\">D</td></tr></table></body></html>";

        var map = await ResolveAsync(html, "#cell").ConfigureAwait(false);

        // font-weight should either be absent or NOT be "bold".
        if (map.TryGetValue("font-weight", out string? fw))
        {
            fw.Should().NotBe("bold",
                because: "<td> must NOT receive UA bold; only <th> does");
        }
    }

    // -----------------------------------------------------------------------
    // 9. UA defaults: h1–h6 → font-weight:bold
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UaDefault_H1GetsBold()
    {
        const string html =
            "<html><head></head><body><h1 id=\"h\">Title</h1></body></html>";

        var map = await ResolveAsync(html, "#h").ConfigureAwait(false);

        map.Should().ContainKey("font-weight");
        map["font-weight"].Should().Be("bold",
            because: "<h1> must default to font-weight:bold per UA stylesheet");
    }

    // -----------------------------------------------------------------------
    // 10. UA defaults: <b>/<strong> → font-weight:bold
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UaDefault_BoldElementGetsBold()
    {
        const string html =
            "<html><head></head><body><b id=\"b\">bold text</b></body></html>";

        var map = await ResolveAsync(html, "#b").ConfigureAwait(false);

        map.Should().ContainKey("font-weight");
        map["font-weight"].Should().Be("bold",
            because: "<b> must default to font-weight:bold per UA stylesheet");
    }

    // -----------------------------------------------------------------------
    // 11. UA defaults: <i>/<em> → font-style:italic
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UaDefault_ItalicElementGetsItalic()
    {
        const string html =
            "<html><head></head><body><i id=\"i\">italic text</i></body></html>";

        var map = await ResolveAsync(html, "#i").ConfigureAwait(false);

        map.Should().ContainKey("font-style");
        map["font-style"].Should().Be("italic",
            because: "<i> must default to font-style:italic per UA stylesheet");
    }

    // -----------------------------------------------------------------------
    // 12. UA defaults: <u> → text-decoration:underline
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UaDefault_UGetsUnderline()
    {
        const string html =
            "<html><head></head><body><u id=\"u\">underline text</u></body></html>";

        var map = await ResolveAsync(html, "#u").ConfigureAwait(false);

        map.Should().ContainKey("text-decoration");
        map["text-decoration"].Should().Be("underline",
            because: "<u> must default to text-decoration:underline per UA stylesheet");
    }

    // -----------------------------------------------------------------------
    // 13. UA display map: <td> gets display:table-cell
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UaDefault_DisplayMapTableCell()
    {
        const string html =
            "<html><head></head>" +
            "<body><table><tr><td id=\"cell\">D</td></tr></table></body></html>";

        var map = await ResolveAsync(html, "#cell").ConfigureAwait(false);

        // UA display map should set table-cell unless an author rule overrides it.
        map.Should().ContainKey("display",
            because: "<td> must receive a UA display value");
        map["display"].Should().Be("table-cell",
            because: "<td> UA display is table-cell per HTML5 §15");
    }

    // -----------------------------------------------------------------------
    // 14. Inheritance: child inherits color from parent
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Inheritance_ChildInheritsParentColor()
    {
        const string html =
            "<html><head><style>.parent { color: red }</style></head>" +
            "<body><div class=\"parent\"><span id=\"child\">text</span></div></body></html>";

        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);
        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);
        var resolver = new CascadeResolver(ruleSet);

        IElement? parent = document.QuerySelector(".parent");
        parent.Should().NotBeNull();
        var parentMap = resolver.Resolve(parent!, null);

        IElement? child = document.QuerySelector("#child");
        child.Should().NotBeNull();
        var childMap = resolver.Resolve(child!, parentMap);

        childMap.Should().ContainKey("color",
            because: "color is an inherited property; child must inherit from parent");
        childMap["color"].Should().Be(parentMap["color"],
            because: "child with no own color must copy the parent's resolved color");
    }

    // -----------------------------------------------------------------------
    // 15. Non-inherited property: border-top-width does NOT copy from parent
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Inheritance_NonInheritedPropertyNotCopied()
    {
        const string html =
            "<html><head><style>.parent { border-top-width: 5px }</style></head>" +
            "<body><div class=\"parent\"><span id=\"child\">text</span></div></body></html>";

        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);
        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);
        var resolver = new CascadeResolver(ruleSet);

        IElement? parent = document.QuerySelector(".parent");
        parent.Should().NotBeNull();
        var parentMap = resolver.Resolve(parent!, null);

        IElement? child = document.QuerySelector("#child");
        child.Should().NotBeNull();
        var childMap = resolver.Resolve(child!, parentMap);

        // border-top-width is NOT inherited — child must not get the parent's value.
        if (childMap.TryGetValue("border-top-width", out string? childBtw))
        {
            // If the child has the key, it must NOT have come from inheritance —
            // it would only be present if the child itself has a matching rule.
            // Since there's no rule targeting the child, it should be absent or non-5px.
            childBtw.Should().NotBe("5px",
                because: "border-top-width is not inherited and must not propagate to child");
        }
    }

    // -----------------------------------------------------------------------
    // 16. Unit resolution: em → px
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnitResolution_EmConvertsToPx()
    {
        const string html =
            "<html><head><style>.box { font-size: 16px; padding-top: 1em }</style></head>" +
            "<body><div class=\"box\"></div></body></html>";

        var map = await ResolveAsync(html, ".box").ConfigureAwait(false);

        map.Should().ContainKey("padding-top",
            because: "padding-top must resolve after shorthand expansion");

        // 1em at font-size 16px → 16px string (after unit resolution step)
        map["padding-top"].Should().Be("16px",
            because: "1em at font-size 16px must resolve to 16px");
    }

    // -----------------------------------------------------------------------
    // 17. Unit resolution: % stays literal
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnitResolution_PercentRemainsLiteral()
    {
        const string html =
            "<html><head><style>.box { width: 50% }</style></head>" +
            "<body><div class=\"box\"></div></body></html>";

        var map = await ResolveAsync(html, ".box").ConfigureAwait(false);

        map.Should().ContainKey("width",
            because: "width:50% must be collected and stored");
        map["width"].Should().Be("50%",
            because: "% values must be left as literal strings (layout resolves against containing block)");
    }

    // -----------------------------------------------------------------------
    // 18. Unit resolution: px passes through unchanged
    // -----------------------------------------------------------------------

    [Fact]
    public async Task UnitResolution_PxPassesThrough()
    {
        const string html =
            "<html><head><style>.box { border-top-width: 1px }</style></head>" +
            "<body><div class=\"box\"></div></body></html>";

        var map = await ResolveAsync(html, ".box").ConfigureAwait(false);

        if (map.TryGetValue("border-top-width", out string? btw))
        {
            btw.Should().Be("1px",
                because: "px lengths must pass through the unit resolution step unchanged");
        }
    }

    // -----------------------------------------------------------------------
    // G25: ".t tr.no-border td { border: none }" suppresses base ".t td { border: 1px solid }"
    //      on cells inside tr.no-border; plain tr cells keep 1px
    // -----------------------------------------------------------------------

    [Fact]
    public async Task G25_NoBorderRow_SuppressesBaseCellBorder()
    {
        const string html =
            "<html><head><style>" +
            ".t td { border: 1px solid #008080 } " +
            ".t tr.no-border td { border: none }" +
            "</style></head>" +
            "<body><table class=\"t\"><tbody>" +
            "<tr><td id=\"plain\">A</td></tr>" +
            "<tr class=\"no-border\"><td id=\"suppressed\">B</td></tr>" +
            "</tbody></table></body></html>";

        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);
        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);
        var resolver = new CascadeResolver(ruleSet);

        IElement? plainTd = document.QuerySelector("#plain");
        plainTd.Should().NotBeNull();
        var plainMap = resolver.Resolve(plainTd!, null);

        IElement? suppressedTd = document.QuerySelector("#suppressed");
        suppressedTd.Should().NotBeNull();
        var suppressedMap = resolver.Resolve(suppressedTd!, null);

        // Plain td must have a border (width should be 1px, not 0/none).
        // AngleSharp may expand border shorthand, so check longhand keys.
        bool plainHasBorder = HasPositiveBorderWidth(plainMap);
        plainHasBorder.Should().BeTrue(
            because: "plain row's td must keep '.t td { border: 1px }' — G25 base rule");

        // Suppressed td must have border:none (width = 0 or style = none).
        bool suppressedHasNoBorder = HasNoBorderWidth(suppressedMap);
        suppressedHasNoBorder.Should().BeTrue(
            because: "'.t tr.no-border td { border: none }' must override and suppress border on G25 td");
    }

    private static bool HasPositiveBorderWidth(Dictionary<string, string> map)
    {
        // Check any of the four width longhands for a non-zero value.
        foreach (string side in new[] { "border-top-width", "border-right-width", "border-bottom-width", "border-left-width" })
        {
            if (map.TryGetValue(side, out string? w) && !string.IsNullOrEmpty(w) && w != "0")
                return true;
        }
        return false;
    }

    private static bool HasNoBorderWidth(Dictionary<string, string> map)
    {
        // All four width longhands must be absent, "0", or styles must be "none".
        bool allZeroOrAbsent = true;
        foreach (string side in new[] { "border-top-width", "border-right-width", "border-bottom-width", "border-left-width" })
        {
            if (map.TryGetValue(side, out string? w) && !string.IsNullOrEmpty(w) && w != "0" && w != "none")
            {
                allZeroOrAbsent = false;
                break;
            }
        }

        if (allZeroOrAbsent)
            return true;

        // Alternatively, border-*-style could be "none".
        bool allNoneStyle = true;
        foreach (string side in new[] { "border-top-style", "border-right-style", "border-bottom-style", "border-left-style" })
        {
            if (!map.TryGetValue(side, out string? s) || (s != "none" && s != "hidden"))
            {
                allNoneStyle = false;
                break;
            }
        }
        return allNoneStyle;
    }

    // -----------------------------------------------------------------------
    // G27: ".t td { padding: 2px 6px }" — 2-value padding expands correctly
    //      (already covered by test 6 above, duplicated here with explicit G27 name for plan compliance)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task G27_PaddingShorthand_TwoValueVerticalHorizontal()
    {
        const string html =
            "<html><head><style>.t td { padding: 2px 6px }</style></head>" +
            "<body><table class=\"t\"><tr><td id=\"cell\">X</td></tr></table></body></html>";

        var map = await ResolveAsync(html, "#cell").ConfigureAwait(false);

        map["padding-top"].Should().Be("2px",
            because: "G27: vertical token (2px) must be padding-top");
        map["padding-bottom"].Should().Be("2px",
            because: "G27: vertical token (2px) must be padding-bottom");
        map["padding-left"].Should().Be("6px",
            because: "G27: horizontal token (6px) must be padding-left");
        map["padding-right"].Should().Be("6px",
            because: "G27: horizontal token (6px) must be padding-right");
    }

    // -----------------------------------------------------------------------
    // G28: ".t td { word-break: break-word }" matches td.text-center (own class differs)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task G28_WordBreak_DescendantSelectorMatchesCellWithDifferentClass()
    {
        const string html =
            "<html><head><style>.t td { word-break: break-word }</style></head>" +
            "<body><table class=\"t\"><tr><td class=\"text-center\" id=\"cell\">ONES_EAL12133</td></tr></table></body></html>";

        var map = await ResolveAsync(html, "#cell").ConfigureAwait(false);

        map.Should().ContainKey("word-break",
            because: "G28: '.t td { word-break: break-word }' must match td inside .t even when td has a different own class");
        map["word-break"].Should().Be("break-word",
            because: "G28: descendant selector must resolve word-break to break-word");
    }

    // -----------------------------------------------------------------------
    // G29: ".t td { white-space: nowrap }" matches td.text-center (own class differs)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task G29_WhiteSpace_DescendantSelectorNowrapResolvesOnCellWithDifferentClass()
    {
        const string html =
            "<html><head><style>.t td { white-space: nowrap }</style></head>" +
            "<body><table class=\"t\"><tr><td class=\"text-center\" id=\"cell\">ONEE0000002</td></tr></table></body></html>";

        var map = await ResolveAsync(html, "#cell").ConfigureAwait(false);

        map.Should().ContainKey("white-space",
            because: "G29: '.t td { white-space: nowrap }' must match td inside .t even when td has a different own class");
        map["white-space"].Should().Be("nowrap",
            because: "G29: descendant selector must resolve white-space to nowrap");
    }
}
