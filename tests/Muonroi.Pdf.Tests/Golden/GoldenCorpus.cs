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

    /// <summary>Every registered case across all groups. Later plans extend by concatenation.</summary>
    internal static readonly IReadOnlyList<GoldenCase> AllCases =
        BlockLayout
            .ToList();

    /// <summary>MemberData source yielding <c>[case.Name]</c> for every registered case.</summary>
    public static IEnumerable<object[]> AllCasesData() =>
        AllCases.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the block-layout group only.</summary>
    public static IEnumerable<object[]> BlockCasesData() =>
        BlockLayout.Select(c => new object[] { c.Name });

    /// <summary>Looks up a registered case by name.</summary>
    public static GoldenCase ByName(string name) =>
        AllCases.First(c => c.Name == name);
}
