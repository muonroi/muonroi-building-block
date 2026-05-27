using System.Collections.Generic;
using System.Linq;
using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Central registry of golden corpus cases (name -> html + options), shared by the golden
/// baseline tests and the determinism canary. Each CSS-feature group lives in its own
/// <c>internal static readonly</c> field; later plans (07-02, 07-03) APPEND their groups and
/// extend <see cref="AllCases"/> by concatenation. This plan registers only the block-layout group.
/// </summary>
internal static class GoldenCorpus
{
    /// <summary>One renderable corpus case. <paramref name="Options"/> defaults to <c>new()</c>.</summary>
    internal readonly record struct GoldenCase(string Name, string Html, PdfRenderOptions Options)
    {
        public GoldenCase(string name, string html) : this(name, html, new PdfRenderOptions()) { }
    }

    // The box tree assigns synthesized inline text the default family "serif" (block-level
    // font-family is not inherited down to inline text nodes in the current cascade). The headless
    // build host has no OS fonts, so the embedded test face must be declared UNDER "serif" for the
    // resolver to find it. Mirrors MPdfServiceIntegrationTests.
    private const string FontFace = "@font-face{font-family:serif;src:url(test.ttf);}";

    private static string Doc(string style, string body) =>
        $"<html><head><style>{FontFace}{style}</style></head><body>{body}</body></html>";

    /// <summary>
    /// Block-layout golden cases exercising only the v0.1 CSS subset (block/inline; NO
    /// flex/grid/float/position).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> BlockLayout = new[]
    {
        new GoldenCase(
            "block-single",
            Doc("p{margin:0;}", "<p>Single block paragraph.</p>")),
        new GoldenCase(
            "block-nested",
            Doc("div{padding:5px;}", "<div><div><p>Nested block boxes.</p></div></div>")),
        new GoldenCase(
            "margin-collapse-adjacent",
            Doc("p{margin:20px 0;}", "<p>First.</p><p>Second.</p>")),
        new GoldenCase(
            "margin-collapse-parent-child",
            Doc(".parent{margin-top:30px;}.child{margin-top:10px;}",
                "<div class=\"parent\"><p class=\"child\">Parent/child margin collapse.</p></div>")),
        new GoldenCase(
            "bfc-root-overflow-hidden",
            Doc("div{overflow:hidden;padding:8px;}",
                "<div><p>Block formatting context root.</p></div>")),
        new GoldenCase(
            "box-sizing-padding-border",
            Doc("div{box-sizing:border-box;padding:10px;border:2px solid black;width:200px;}",
                "<div><p>Box sizing border-box.</p></div>")),
        new GoldenCase(
            "block-multi-paragraph",
            Doc("p{margin:5px 0;}",
                "<p>Para one.</p><p>Para two.</p><p>Para three.</p>")),
        new GoldenCase(
            "block-background-color",
            Doc("div{background-color:#eeeeee;padding:6px;}",
                "<div><p>Block with background color.</p></div>")),
    };

    /// <summary>
    /// Inline-layout golden cases: line wrapping, baseline alignment across font sizes,
    /// vertical-align, and white-space handling. v0.1 inline subset only.
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> InlineLayout = new[]
    {
        new GoldenCase(
            "inline-single-wrap",
            Doc("p{width:120px;margin:0;}",
                "<p>This sentence is long enough to wrap onto a second line inside a narrow box.</p>")),
        new GoldenCase(
            "inline-multi-line-wrap",
            Doc("p{width:90px;margin:0;}",
                "<p>Alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi.</p>")),
        new GoldenCase(
            "inline-mixed-font-size-baseline",
            Doc(".big{font-size:24px;}.small{font-size:10px;}",
                "<p><span class=\"small\">small</span> <span class=\"big\">BIG</span> <span class=\"small\">small</span></p>")),
        new GoldenCase(
            "inline-vertical-align",
            Doc(".sup{vertical-align:super;font-size:10px;}.sub{vertical-align:sub;font-size:10px;}",
                "<p>base<span class=\"sup\">up</span> and<span class=\"sub\">down</span> baseline</p>")),
        new GoldenCase(
            "white-space-normal-vs-pre",
            Doc(".pre{white-space:pre;}",
                "<p>normal   collapses   spaces</p><p class=\"pre\">pre   keeps   spaces</p>")),
        new GoldenCase(
            "inline-trailing-space",
            Doc("p{width:140px;margin:0;}",
                "<p>Trailing inline spaces should collapse at the end of the line.     </p>")),
    };

    /// <summary>
    /// Table golden cases: 2x2 baseline, colspan/rowspan/combined, explicit border-spacing,
    /// and auto vs fixed column widths. <c>border-collapse:separate</c> only (collapse is policy-rejected).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> Tables = new[]
    {
        new GoldenCase(
            "table-2x2",
            Doc("table{border-collapse:separate;}td{border:1px solid black;padding:4px;}",
                "<table><tr><td>A1</td><td>B1</td></tr><tr><td>A2</td><td>B2</td></tr></table>")),
        new GoldenCase(
            "table-colspan2",
            Doc("table{border-collapse:separate;}td{border:1px solid black;padding:4px;}",
                "<table><tr><td colspan=\"2\">Spanning header</td></tr>" +
                "<tr><td>Left</td><td>Right</td></tr></table>")),
        new GoldenCase(
            "table-rowspan2",
            Doc("table{border-collapse:separate;}td{border:1px solid black;padding:4px;}",
                "<table><tr><td rowspan=\"2\">Tall</td><td>Top</td></tr>" +
                "<tr><td>Bottom</td></tr></table>")),
        new GoldenCase(
            "table-colspan-rowspan",
            Doc("table{border-collapse:separate;}td{border:1px solid black;padding:4px;}",
                "<table><tr><td colspan=\"2\" rowspan=\"2\">Big</td><td>C1</td></tr>" +
                "<tr><td>C2</td></tr><tr><td>A3</td><td>B3</td><td>C3</td></tr></table>")),
        new GoldenCase(
            "table-border-collapse-separate-spacing",
            Doc("table{border-collapse:separate;border-spacing:8px;}td{border:1px solid black;padding:4px;}",
                "<table><tr><td>A1</td><td>B1</td></tr><tr><td>A2</td><td>B2</td></tr></table>")),
        new GoldenCase(
            "table-auto-column-width",
            Doc("table{border-collapse:separate;}td{border:1px solid black;padding:4px;}",
                "<table><tr><td>short</td><td>a much longer cell content column</td></tr>" +
                "<tr><td>x</td><td>y</td></tr></table>")),
        new GoldenCase(
            "table-fixed-column-width",
            Doc("table{border-collapse:separate;table-layout:fixed;width:300px;}" +
                "td{border:1px solid black;padding:4px;width:150px;}",
                "<table><tr><td>fixed one</td><td>fixed two</td></tr>" +
                "<tr><td>x</td><td>y</td></tr></table>")),
    };

    /// <summary>Every registered case across all groups. Later plans extend by concatenation.</summary>
    internal static readonly IReadOnlyList<GoldenCase> AllCases =
        BlockLayout
            .Concat(InlineLayout)
            .Concat(Tables)
            .ToList();

    /// <summary>MemberData source yielding <c>[case.Name]</c> for every registered case.</summary>
    public static IEnumerable<object[]> AllCasesData() =>
        AllCases.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the block-layout group only.</summary>
    public static IEnumerable<object[]> BlockCasesData() =>
        BlockLayout.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the inline-layout group only.</summary>
    public static IEnumerable<object[]> InlineCasesData() =>
        InlineLayout.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the table group only.</summary>
    public static IEnumerable<object[]> TableCasesData() =>
        Tables.Select(c => new object[] { c.Name });

    /// <summary>Looks up a registered case by name.</summary>
    public static GoldenCase ByName(string name) =>
        AllCases.First(c => c.Name == name);
}
