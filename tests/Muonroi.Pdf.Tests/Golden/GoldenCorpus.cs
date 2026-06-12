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
        new GoldenCase(
            "table-large-colspan-rowspan",
            Doc("table{border-collapse:separate;}td{border:1px solid black;padding:4px;}",
                "<table>" +
                "<tr><td colspan=\"10\" rowspan=\"2\">Wide spanning cell</td><td>C1</td><td>D1</td></tr>" +
                "<tr><td>C2</td><td>D2</td></tr>" +
                "<tr><td>A3</td><td>B3</td><td>C3</td><td>D3</td></tr>" +
                "</table>")),
    };

    /// <summary>
    /// Extended table golden cases: border-collapse:collapse and vertical-align variants.
    /// Added by Plan 04 (Wave 2).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> TablesExtended = new[]
    {
        new GoldenCase(
            "table-border-collapse",
            Doc("table{border-collapse:collapse;}td{border-left:1px solid black;padding:4px;}",
                "<table><tr><td>A1</td><td>B1</td></tr><tr><td>A2</td><td>B2</td></tr></table>")),
        new GoldenCase(
            "table-vertical-align-top",
            Doc("table{border-collapse:separate;}td{height:50px;vertical-align:top;padding:4px;border:1px solid black;}",
                "<table><tr><td>A</td></tr></table>")),
        new GoldenCase(
            "table-vertical-align-middle",
            Doc("table{border-collapse:separate;}td{height:50px;vertical-align:middle;padding:4px;border:1px solid black;}",
                "<table><tr><td>B</td></tr></table>")),
    };

    /// <summary>
    /// Extended fidelity golden cases: background-color fill + background-image data-URI.
    /// Added by Plan 07 (Wave 4).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> FidelityExtended = new[]
    {
        new GoldenCase(
            "background-color-block",
            Doc("div{background-color:#EEEEEE;padding:6px;}",
                "<div><p>HELLO</p></div>")),
        new GoldenCase(
            "background-image-data-uri",
            Doc("div{background-image:url(" + PngDataUri + ");width:50px;height:50px;}",
                "<div></div>")),
    };

    /// <summary>
    /// Extended inline golden cases: text-transform:uppercase, white-space:pre-line, nobr.
    /// Added by Plan 07 (Wave 4).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> InlineLayoutExtended = new[]
    {
        new GoldenCase(
            "inline-text-transform-uppercase",
            Doc("div{}", "<div style=\"text-transform:uppercase\">hello world</div>")),
        new GoldenCase(
            "inline-whitespace-pre-line",
            Doc("td{white-space:pre-line;}", "<table><tr><td>line1\nline2</td></tr></table>")),
        new GoldenCase(
            "inline-nobr",
            Doc("div{width:80px;}", "<div><nobr>no break here</nobr></div>")),
    };

    /// <summary>
    /// Abs-pos layout golden cases: position:absolute deferred-pass in a position:relative container.
    /// Added by Plan 06 (Wave 3b).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> PositionedLayout = new[]
    {
        new GoldenCase(
            "abs-pos-image",
            Doc(".container{position:relative;width:200px;height:100px;}" +
                ".overlay{position:absolute;top:10px;left:20px;width:50px;height:30px;}",
                "<div class=\"container\"><div class=\"overlay\">ABS</div><p>Normal flow</p></div>")),
        new GoldenCase(
            "abs-pos-percent-top",
            Doc(".container{position:relative;width:200px;height:100px;}" +
                ".overlay{position:absolute;top:50%;left:10px;width:50px;height:20px;}",
                "<div class=\"container\"><div class=\"overlay\">50%</div><p>Normal flow</p></div>")),
    };

    /// <summary>
    /// Float layout golden cases: float:left/right side-by-side, clear:both.
    /// Added by Plan 05 (Wave 3a).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> BlockLayoutFloat = new[]
    {
        new GoldenCase(
            "float-two-column",
            Doc(".left{float:left;width:40%;}.right{float:right;width:40%;}.clear{clear:both;}",
                "<div><div class=\"left\">LEFT</div><div class=\"right\">RIGHT</div><div class=\"clear\"></div></div>")),
        new GoldenCase(
            "float-clear-below",
            Doc(".left{float:left;width:40%;}.right{float:right;width:40%;}.clear{clear:both;}",
                "<div><div class=\"left\">LEFT</div><div class=\"right\">RIGHT</div><div class=\"clear\"></div><div>BELOW</div></div>")),
    };

    private static string LongFlow(int paragraphs)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= paragraphs; i++)
        {
            sb.Append("<p>Paragraph ").Append(i)
              .Append(" of a long document that flows across multiple pages to exercise pagination.</p>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Paged-media golden cases: explicit page breaks, @page margins, multiple page sizes and
    /// orientations, repeating header/footer margin boxes, and counter(page)/counter(pages).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> PagedMedia = new[]
    {
        new GoldenCase(
            "page-break-before-always",
            Doc(".next{page-break-before:always;}",
                "<p>First page content.</p><p class=\"next\">Forced onto a new page.</p>")),
        new GoldenCase(
            "page-break-after",
            Doc(".brk{page-break-after:always;}",
                "<p class=\"brk\">Ends this page.</p><p>Starts the next page.</p>")),
        new GoldenCase(
            "page-break-inside-avoid",
            Doc(".keep{page-break-inside:avoid;padding:6px;}",
                "<div class=\"keep\"><p>Block one.</p><p>Block two kept together.</p></div>")),
        new GoldenCase(
            "multi-page-overflow-flow",
            Doc("p{margin:6px 0;}", LongFlow(60))),
        new GoldenCase(
            "page-margins",
            Doc("p{margin:0;}", "<p>Document with wide custom @page margins.</p>"),
            new PdfRenderOptions { Margins = PdfMargins.Uniform(30) }),
        new GoldenCase(
            "page-size-a5",
            Doc("p{margin:0;}", "<p>A5 page size.</p>"),
            new PdfRenderOptions { PageSize = PdfPageSize.A5 }),
        new GoldenCase(
            "page-size-letter",
            Doc("p{margin:0;}", "<p>US Letter page size.</p>"),
            new PdfRenderOptions { PageSize = PdfPageSize.Letter }),
        new GoldenCase(
            "page-size-legal",
            Doc("p{margin:0;}", "<p>US Legal page size.</p>"),
            new PdfRenderOptions { PageSize = PdfPageSize.Legal }),
        new GoldenCase(
            "orientation-landscape",
            Doc("p{margin:0;}", "<p>Landscape orientation.</p>"),
            new PdfRenderOptions { Orientation = PdfOrientation.Landscape }),
        new GoldenCase(
            "header-footer-repeat",
            Doc("p{margin:6px 0;}", LongFlow(40)),
            new PdfRenderOptions
            {
                Header = new PdfHeaderFooter(CenterHtml: "Report Title", ShowLine: true),
                Footer = new PdfHeaderFooter(CenterHtml: "Confidential", ShowLine: true),
            }),
        new GoldenCase(
            "counter-page",
            Doc("p{margin:6px 0;}", LongFlow(30)),
            new PdfRenderOptions
            {
                Footer = new PdfHeaderFooter(RightHtml: "<span>Page counter(page)</span>"),
            }),
        new GoldenCase(
            "counter-pages-x-of-y",
            Doc("p{margin:6px 0;}", LongFlow(30)),
            new PdfRenderOptions
            {
                Footer = new PdfHeaderFooter(
                    CenterHtml: "<span>counter(page) of counter(pages)</span>"),
            }),
    };

    // Deterministic 4x4 truecolor PNG (red) and 4x4 baseline JPEG (blue), generated once and embedded
    // as literals so image cases are self-contained — no external fetch, no extra committed asset.
    private const string PngDataUri =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR42mM4oaEBRwzEcQDRQxGBoNNuZAAAAABJRU5ErkJggg==";

    private const string JpegDataUri =
        "data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAYABgAAD/2wCEAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSgBBwcHCggKEwoKEygaFhooKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKP/AABEIAAQABAMBIgACEQEDEQH/xAGiAAABBQEBAQEBAQAAAAAAAAAAAQIDBAUGBwgJCgsQAAIBAwMCBAMFBQQEAAABfQECAwAEEQUSITFBBhNRYQcicRQygZGhCCNCscEVUtHwJDNicoIJChYXGBkaJSYnKCkqNDU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6g4SFhoeIiYqSk5SVlpeYmZqio6Slpqeoqaqys7S1tre4ubrCw8TFxsfIycrS09TV1tfY2drh4uPk5ebn6Onq8fLz9PX29/j5+gEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoLEQACAQIEBAMEBwUEBAABAncAAQIDEQQFITEGEkFRB2FxEyIygQgUQpGhscEJIzNS8BVictEKFiQ04SXxFxgZGiYnKCkqNTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqCg4SFhoeIiYqSk5SVlpeYmZqio6Slpqeoqaqys7S1tre4ubrCw8TFxsfIycrS09TV1tfY2dri4+Tl5ufo6ery8/T19vf4+fr/2gAMAwEAAhEDEQA/AOBooor9MPnT/9k=";

    /// <summary>Image golden cases: PNG/JPEG data-URIs, per-call resolver, intrinsic + explicit sizing.</summary>
    internal static readonly IReadOnlyList<GoldenCase> Images = new[]
    {
        new GoldenCase(
            "image-png-datauri",
            Doc("img{display:block;}", $"<img src=\"{PngDataUri}\" />")),
        new GoldenCase(
            "image-jpeg-datauri",
            Doc("img{display:block;}", $"<img src=\"{JpegDataUri}\" />")),
        new GoldenCase(
            "image-via-resolver",
            Doc("img{display:block;}", "<img src=\"https://assets.local/logo.png\" />"),
            new PdfRenderOptions { ResourceResolver = StubPngResolver.Instance }),
        new GoldenCase(
            "image-intrinsic-size",
            Doc("img{display:block;}", $"<img src=\"{PngDataUri}\" />")),
        new GoldenCase(
            "image-explicit-wh",
            Doc("img{display:block;width:48px;height:48px;}", $"<img src=\"{PngDataUri}\" />")),
    };

    /// <summary>Font golden cases: embedded subset, weight/style variants, scale, @font-face resolution.</summary>
    internal static readonly IReadOnlyList<GoldenCase> Fonts = new[]
    {
        new GoldenCase(
            "font-embedded-ttf-subset",
            Doc("p{margin:0;}", "<p>Embedded subset glyph coverage.</p>")),
        new GoldenCase(
            "font-bold-weight",
            Doc("p{margin:0;font-weight:bold;}", "<p>Bold weight text.</p>")),
        new GoldenCase(
            "font-italic-style",
            Doc("p{margin:0;font-style:italic;}", "<p>Italic style text.</p>")),
        new GoldenCase(
            "font-size-scale",
            Doc(".s{font-size:8px;}.m{font-size:16px;}.l{font-size:32px;}",
                "<p class=\"s\">small</p><p class=\"m\">medium</p><p class=\"l\">large</p>")),
        new GoldenCase(
            "font-face-resolved",
            Doc("p{margin:0;font-family:serif;}", "<p>Resolved via @font-face under serif.</p>")),
    };

    /// <summary>
    /// Security golden: a normal document whose output must be a hardened %PDF-1.7 stream carrying no
    /// /JavaScript token (locks SEC-01/02 into the corpus). SecurityGoldenTests asserts on the bytes.
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> Security = new[]
    {
        new GoldenCase(
            "security-hardened-no-js",
            Doc("p{margin:0;}", "<p>Hardened output: no JavaScript actions, %PDF-1.7 header.</p>")),
    };

    private static string VnLongFlow(int paragraphs)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 1; i <= paragraphs; i++)
        {
            sb.Append("<p>Đoạn ").Append(i)
              .Append(" của một tài liệu dài tiếng Việt trải qua nhiều trang để kiểm tra phân trang"
                  + " với các dấu thanh điệu chồng nhau như ế ộ ữ ầ ổ ừ.</p>");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Vietnamese golden cases (TEST-02): precomposed diacritics, diacritic stacking (vowel + tone),
    /// mixed Latin+Vietnamese, line-breaking/wrapping, table cells, paged counters, and multi-page
    /// flow. Exercises the embedded Noto Sans Vietnamese glyph coverage (guarded by
    /// <c>VietnameseFont_HasGlyphCoverage</c> so baselines are never vacuous .notdef boxes).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> Vietnamese = new[]
    {
        new GoldenCase(
            "vn-diacritic-word",
            Doc("p{margin:0;}", "<p>Tiếng Việt</p>")),
        new GoldenCase(
            "vn-stacked-tone-vowel",
            Doc("p{margin:0;}", "<p>ế ộ ữ ổ ừ ẹ ầ</p>")),
        new GoldenCase(
            "vn-mixed-latin-vn",
            Doc("p{margin:0;}", "<p>Hello thế giới — mixing Latin and Tiếng Việt in one run.</p>")),
        new GoldenCase(
            "vn-line-wrap",
            Doc("p{width:120px;margin:0;}",
                "<p>Một câu tiếng Việt đủ dài để ngắt dòng bên trong hộp hẹp với nhiều dấu thanh.</p>")),
        new GoldenCase(
            "vn-table-cell",
            Doc("table{border-collapse:separate;}td{border:1px solid black;padding:4px;}",
                "<table><tr><td>Tiếng Việt</td><td>ế ộ ữ</td></tr>" +
                "<tr><td>Ầ Ữ</td><td>Trang</td></tr></table>")),
        new GoldenCase(
            "vn-page-header-counter",
            Doc("p{margin:6px 0;}", VnLongFlow(12)),
            new PdfRenderOptions
            {
                Header = new PdfHeaderFooter(CenterHtml: "Báo cáo Tiếng Việt", ShowLine: true),
            }),
        new GoldenCase(
            "vn-uppercase-diacritics",
            Doc("p{margin:0;}", "<p>Ầ Ữ Ổ Ừ Ế Ộ</p>")),
        new GoldenCase(
            "vn-long-paragraph-pagebreak",
            Doc("p{margin:6px 0;}", VnLongFlow(60))),
        new GoldenCase(
            "vn-digits-trang-x-of-y",
            Doc("p{margin:6px 0;}", VnLongFlow(20)),
            new PdfRenderOptions
            {
                Footer = new PdfHeaderFooter(CenterHtml: "<span>Trang counter(page) / counter(pages)</span>"),
            }),
        new GoldenCase(
            "vn-bold-italic-runs",
            Doc(".b{font-weight:bold;}.i{font-style:italic;}",
                "<p><span class=\"b\">Tiếng Việt đậm</span> và <span class=\"i\">nghiêng ế ộ ữ</span></p>")),
        new GoldenCase(
            "vn-counter-footer",
            Doc("p{margin:6px 0;}", VnLongFlow(15)),
            new PdfRenderOptions
            {
                Margins = PdfMargins.Uniform(30),
                Footer = new PdfHeaderFooter(RightHtml: "<span>Tài liệu — Trang counter(page)</span>"),
            }),
        new GoldenCase(
            "vn-multi-page-flow",
            Doc("p{margin:6px 0;}", VnLongFlow(40)),
            new PdfRenderOptions { Margins = PdfMargins.Uniform(25) }),
    };

    /// <summary>
    /// Fidelity-layout golden cases: text-align (center/right/justify), line-height (unitless/px),
    /// and text-decoration (underline/line-through). Exercises FIDELITY-01..03.
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> FidelityLayout = new[]
    {
        new GoldenCase(
            "text-align-center",
            Doc("p{margin:0;}", "<p style=\"text-align:center\">Centered text on the page.</p>")),
        new GoldenCase(
            "text-align-right",
            Doc("p{margin:0;}", "<p style=\"text-align:right\">Right-aligned text on the page.</p>")),
        new GoldenCase(
            "text-align-justify",
            Doc("p{margin:0;width:300px;}",
                "<p style=\"text-align:justify\">Justified text stretches to fill the full line width. " +
                "Each interior line should have equal spacing between words. " +
                "The last line is not stretched per CSS 2.1.</p>")),
        new GoldenCase(
            "line-height-factor",
            Doc("p{margin:0;font-size:14px;}",
                "<p style=\"line-height:2.0\">Double-spaced line one.<br/>Line two also double spaced.</p>")),
        new GoldenCase(
            "line-height-px",
            Doc("p{margin:0;}",
                "<p style=\"line-height:32px;font-size:16px\">Explicit 32 px line height text.</p>")),
        new GoldenCase(
            "text-decoration-underline",
            Doc("p{margin:0;}", "<p><u>Underlined text rendered with decoration rule.</u></p>")),
        new GoldenCase(
            "text-decoration-strikethrough",
            Doc("p{margin:0;}", "<p><s>Struck-through text rendered with strikethrough rule.</s></p>")),
    };

    /// <summary>
    /// HTML5 semantics golden cases: br, hr, ordered/unordered lists, and link annotations.
    /// Exercises FIDELITY-04..07.
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> Html5Semantics = new[]
    {
        new GoldenCase(
            "br-line-break",
            Doc("p{margin:0;}", "<p>Line one.<br/>Line two.<br/>Line three.</p>")),
        new GoldenCase(
            "hr-rule",
            Doc("p{margin:0;}", "<p>Before the rule.</p><hr/><p>After the rule.</p>")),
        new GoldenCase(
            "list-unordered",
            Doc("ul{margin:0;padding-left:20px;}",
                "<ul><li>Item A</li><li>Item B</li><li>Item C</li></ul>")),
        new GoldenCase(
            "list-ordered",
            Doc("ol{margin:0;padding-left:20px;}",
                "<ol><li>First</li><li>Second</li><li>Third</li></ol>")),
        new GoldenCase(
            "link-annotation",
            Doc("p{margin:0;}a{color:blue;}",
                "<p><a href=\"https://example.com\">Click here to visit example.com</a></p>")),
    };

    /// <summary>
    /// Wave 7 regression cases: Bug Y (transparent/rgb background-color parsing) and
    /// Bug X (float side-by-side inline content positioning).
    /// </summary>
    internal static readonly IReadOnlyList<GoldenCase> Wave7Regression = new[]
    {
        // Bug Y regression: AngleSharp returns rgb(r,g,b) for color values. Verify the teal
        // background (#008080 = rgb(0,128,128)) is written as non-zero rg in the content stream,
        // not as the black (0,0,0) fallback.
        new GoldenCase(
            "w7-rgb-background-color",
            Doc("div{background-color:#008080;padding:4px;}",
                "<div><p>Teal bg</p></div>")),

        // Bug Y regression: transparent elements (rgba(0,0,0,0)) must not produce a black fill rect.
        new GoldenCase(
            "w7-transparent-background-no-fill",
            Doc("div{background-color:transparent;padding:4px;}",
                "<div><p>No fill</p></div>")),

        // Bug X regression: inline text that follows a float:left block must start at
        // LeftFloatRight (i.e. shifted right of the float), not at PageMarginLeft.
        new GoldenCase(
            "w7-float-left-inline-beside",
            Doc(".left{float:left;width:30%;}.text{margin:0;}",
                "<div><div class=\"left\">FLOAT</div><p class=\"text\">BESIDE</p></div>")),
    };

    /// <summary>Every registered case across all groups. Later plans extend by concatenation.</summary>
    internal static readonly IReadOnlyList<GoldenCase> AllCases =
        BlockLayout
            .Concat(InlineLayout)
            .Concat(Tables)
            .Concat(TablesExtended)
            .Concat(BlockLayoutFloat)
            .Concat(PositionedLayout)
            .Concat(FidelityExtended)
            .Concat(InlineLayoutExtended)
            .Concat(PagedMedia)
            .Concat(Images)
            .Concat(Fonts)
            .Concat(Security)
            .Concat(Vietnamese)
            .Concat(FidelityLayout)
            .Concat(Html5Semantics)
            .Concat(Wave7Regression)
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

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the extended table group only (Plan 04).</summary>
    public static IEnumerable<object[]> TablesExtendedCasesData() =>
        TablesExtended.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the float layout group only (Plan 05).</summary>
    public static IEnumerable<object[]> BlockLayoutFloatCasesData() =>
        BlockLayoutFloat.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the positioned layout group only (Plan 06).</summary>
    public static IEnumerable<object[]> PositionedLayoutCasesData() =>
        PositionedLayout.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for extended fidelity cases (Plan 07).</summary>
    public static IEnumerable<object[]> FidelityExtendedCasesData() =>
        FidelityExtended.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for extended inline layout cases (Plan 07).</summary>
    public static IEnumerable<object[]> InlineLayoutExtendedCasesData() =>
        InlineLayoutExtended.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the paged-media group only.</summary>
    public static IEnumerable<object[]> PagedMediaCasesData() =>
        PagedMedia.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the image group only.</summary>
    public static IEnumerable<object[]> ImageCasesData() =>
        Images.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the font group only.</summary>
    public static IEnumerable<object[]> FontCasesData() =>
        Fonts.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the Vietnamese group only.</summary>
    public static IEnumerable<object[]> VietnameseCasesData() =>
        Vietnamese.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the fidelity-layout group only.</summary>
    public static IEnumerable<object[]> FidelityLayoutCasesData() =>
        FidelityLayout.Select(c => new object[] { c.Name });

    /// <summary>MemberData source yielding <c>[case.Name]</c> for the HTML5 semantics group only.</summary>
    public static IEnumerable<object[]> Html5SemanticsCasesData() =>
        Html5Semantics.Select(c => new object[] { c.Name });

    /// <summary>MemberData source for Wave 7 regression cases (Bug X float + Bug Y color).</summary>
    public static IEnumerable<object[]> Wave7RegressionCasesData() =>
        Wave7Regression.Select(c => new object[] { c.Name });

    /// <summary>
    /// Per-call <see cref="IResourceResolver"/> stub returning the embedded deterministic PNG for any
    /// requested URI — exercises the resolver path without touching the shared harness.
    /// </summary>
    private sealed class StubPngResolver : IResourceResolver
    {
        public static readonly StubPngResolver Instance = new();

        private static readonly byte[] PngBytes = System.Convert.FromBase64String(
            PngDataUri["data:image/png;base64,".Length..]);

        public System.Threading.Tasks.ValueTask<ResourceResult?> ResolveAsync(
            System.Uri uri, string? contentTypeHint = null,
            System.Threading.CancellationToken cancellationToken = default) =>
            new(new ResourceResult(PngBytes, "image/png"));
    }

    /// <summary>Looks up a registered case by name.</summary>
    public static GoldenCase ByName(string name) =>
        AllCases.First(c => c.Name == name);
}
