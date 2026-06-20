using AngleSharp.Dom;
using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;

namespace Muonroi.Pdf.Tests.Cascade;

/// <summary>
/// Unit tests for <see cref="CssRuleSet.FromDocument"/>: rule collection, grouped-selector
/// splitting, specificity ordering, source order, and the !important flag.
///
/// Note: AngleSharp.Css normalizes color values (e.g. "red" → "rgba(255, 0, 0, 1)") and
/// expands shorthand properties (e.g. "margin: 0" → margin-top/right/bottom/left longhands).
/// Tests assert on property presence and Important flags, not raw authored values, to remain
/// stable against AngleSharp normalization behavior.
/// </summary>
public sealed class CssRuleSetTests
{
    // -----------------------------------------------------------------------
    // Helper: parse HTML → IDocument via the existing AngleSharpHtmlParser path.
    // -----------------------------------------------------------------------
    private static async Task<IDocument> ParseDocumentAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        // AngleSharpParsedDocument is internal; InternalsVisibleTo("Muonroi.Pdf.Tests") is set in the project.
        var angleParsed = (AngleSharpParsedDocument)parsed;
        return angleParsed.Document;
    }

    // -----------------------------------------------------------------------
    // 1. Single rule, single selector, single declaration.
    //    AngleSharp normalizes "red" → rgba form; assert on property name only.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SingleSelector_ProducesOneRuleWithColorDeclaration()
    {
        const string html = "<html><head><style>.a { color: red }</style></head><body></body></html>";
        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);

        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);

        ruleSet.Rules.Should().HaveCount(1);
        CssMatchableRule rule = ruleSet.Rules[0];
        rule.SelectorText.Should().Be(".a");
        rule.Declarations.Should().ContainSingle(d => d.Property == "color",
            because: "AngleSharp may normalize the value but must preserve the property name");
        rule.Declarations.Should().AllSatisfy(d => d.Important.Should().BeFalse());
    }

    // -----------------------------------------------------------------------
    // 2. Grouped selector splits into two entries, both with the same declaration.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task GroupedSelector_SplitsIntoTwoRuleEntries()
    {
        const string html = "<html><head><style>.a, .b { color: red }</style></head><body></body></html>";
        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);

        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);

        ruleSet.Rules.Should().HaveCount(2, because: "one entry per split simple selector");
        ruleSet.Rules.Should().AllSatisfy(r =>
            r.Declarations.Should().ContainSingle(d => d.Property == "color"));
        ruleSet.Rules.Select(r => r.SelectorText).Should().BeEquivalentTo([".a", ".b"]);
    }

    // -----------------------------------------------------------------------
    // 3. !important sets the Important flag on the declaration.
    //    AngleSharp expands "margin" shorthand into margin-top/right/bottom/left;
    //    assert that at least one expanded longhand carries Important=true.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task ImportantDeclaration_SetsImportantFlagTrue()
    {
        const string html = "<html><head><style>.x { margin: 0 !important }</style></head><body></body></html>";
        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);

        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);

        ruleSet.Rules.Should().HaveCount(1);
        CssMatchableRule rule = ruleSet.Rules[0];
        // AngleSharp expands "margin" to its four longhands; all carry !important.
        rule.Declarations.Should().Contain(d => d.Important,
            because: "!important on a shorthand must propagate to at least one expanded declaration");
    }

    // -----------------------------------------------------------------------
    // 4. Source order is strictly increasing across rules.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task SourceOrder_IsStrictlyIncreasingAcrossRules()
    {
        const string html = "<html><head><style>.a { color: red } .b { color: blue }</style></head><body></body></html>";
        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);

        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);

        ruleSet.Rules.Should().HaveCountGreaterThanOrEqualTo(2);
        CssMatchableRule ruleA = ruleSet.Rules.First(r => r.SelectorText == ".a");
        CssMatchableRule ruleB = ruleSet.Rules.First(r => r.SelectorText == ".b");
        ruleB.SourceOrder.Should().BeGreaterThan(ruleA.SourceOrder,
            because: ".b appears after .a in document order");
    }

    // -----------------------------------------------------------------------
    // 5. Specificity ordering: #id > .class > element tag.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Specificity_IdGreaterThanClassGreaterThanTag()
    {
        const string html =
            "<html><head><style>" +
            "#myid { color: red } " +
            ".cls { color: green } " +
            "div { color: blue }" +
            "</style></head><body></body></html>";
        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);

        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);

        CssMatchableRule idRule = ruleSet.Rules.First(r => r.SelectorText == "#myid");
        CssMatchableRule classRule = ruleSet.Rules.First(r => r.SelectorText == ".cls");
        CssMatchableRule tagRule = ruleSet.Rules.First(r => r.SelectorText == "div");

        idRule.Specificity.Should().BeGreaterThan(classRule.Specificity,
            because: "#id specificity > .class specificity");
        classRule.Specificity.Should().BeGreaterThan(tagRule.Specificity,
            because: ".class specificity > element-tag specificity");
    }

    // -----------------------------------------------------------------------
    // 6. @page-only document produces zero collected rules.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task AtPageOnly_ProducesZeroRules()
    {
        const string html = "<html><head><style>@page { margin: 1cm }</style></head><body></body></html>";
        IDocument document = await ParseDocumentAsync(html).ConfigureAwait(false);

        CssRuleSet ruleSet = CssRuleSet.FromDocument(document);

        ruleSet.Rules.Should().BeEmpty(because: "only ICssStyleRule entries are collected; @page is ignored");
    }
}
