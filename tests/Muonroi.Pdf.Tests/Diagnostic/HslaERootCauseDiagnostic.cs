using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Extensions;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Tests.Service;
using Xunit;
using Xunit.Abstractions;

namespace Muonroi.Pdf.Tests.Diagnostic;

/// <summary>
/// One-shot diagnostic for HSLA_E near-blank rendering (Phase 8.8).
/// Dumps the PositionedElement list with X/Y/W/H for every rendered element so
/// we can see WHERE content lands in PDF coordinate space.
///
/// HOW TO ENABLE:
///   Remove the [Skip] attribute from the [Fact] below, then run:
///     dotnet test --filter "FullyQualifiedName~HslaERootCauseDiagnostic" -v n
///   Output appears in the test runner's stdout (xunit ITestOutputHelper).
///
/// DO NOT commit with [Skip] removed — this is a diagnostic aid only.
/// </summary>
[Collection("DiagnosticSerial")]  // run alone to avoid pollution
public sealed class HslaERootCauseDiagnostic(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _out = output;
    private const string TemplatePath = @"D:\Data\Template\Htmls\PreviewRegistion\HSLA_E.html";

    // Minimal dummy values — enough to render a structurally representative page.
    // {{barcode}} still uses the 4×4 placeholder; {{logo}} is upgraded to the real 32×32
    // stub so visual-diff output shows a recognizable logo region (not a solid red block).
    private const string TinyPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR42mM4oaEBRwzEcQDRQxGBoNNuZAAAAABJRU5ErkJggg==";

    private static string FillTemplate(string html) => html
        .Replace("{{title}}", "Test")
        .Replace("{{logo}}", LogoStubTests.RealLogoBase64)
        .Replace("{{barcode}}", TinyPng)
        .Replace("{{operMethodName}}", "Giao thẳng")
        .Replace("{{operMethodCode}}", "GT")
        .Replace("{{orderNo}}", "LOT-001")
        .Replace("{{currentDate}}", "2026-05-28")
        .Replace("{{orderDetailNo}}", "DK-001")
        .Replace("{{customerName}}", "ACME Corp")
        .Replace("{{containerNo}}", "ABCD1234567")
        .Replace("{{iso}}", "20GP")
        .Replace("{{agent}}", "CMA")
        .Replace("{{linerOper}}", "CMA CGM")
        .Replace("{{fullEmpty}}", "F")
        .Replace("{{returnDate}}", "2026-06-01")
        .Replace("{{billNo}}", "BILL123")
        .Replace("{{paymentStatus}}", "Paid")
        .Replace("{{specialHandlings}}", "None")
        .Replace("{{linerRemark}}", "OK")
        .Replace("{{vesselVoyage}}", "VES001/001W")
        .Replace("{{customerRemark}}", "No remark")
        .Replace("{{truckNumber}}", "51A-12345")
        .Replace("{{chassisNumber}}", "CH-001")
        .Replace("{{phoneNumber}}", "0901234567")
        .Replace("{{username}}", "Nguyen Van A");

    /// <summary>
    /// DISABLED by default. Remove [Skip] to run the diagnostic dump.
    /// </summary>
    [Fact]
    public async Task DumpPositionedElements_HslaE()
    {
        if (!File.Exists(TemplatePath))
        {
            _out.WriteLine($"SKIP: Template not found at {TemplatePath}");
            return;
        }

        string rawHtml = await File.ReadAllTextAsync(TemplatePath);
        string html = FillTemplate(rawHtml);

        using var sp = PdfServiceTestHarness.BuildProvider();

        var options = new PdfRenderOptions
        {
            PageSize = PdfPageSize.A5,
            Orientation = PdfOrientation.Landscape
        };

        // Use internal LayoutEngine directly to capture PositionedElements before PDF write.
        // This requires the Governance pipeline to parse HTML → IStyledDocument.
        var parser = sp.GetRequiredService<IHtmlParser>();
        var cascader = sp.GetRequiredService<ICssCascadeEngine>();

        var parsed = await parser.ParseAsync(html);
        var styled = await cascader.CascadeAsync(parsed, userStyleSheet: null);

        var engine = new LayoutEngine();
        var result = (PositionedPageList)engine.Layout(
            styled, options, new PdfConfigs.PdfLimits(), CancellationToken.None);

        _out.WriteLine($"=== HSLA_E Layout Dump ===");
        _out.WriteLine($"Page count: {result.PageCount}");
        _out.WriteLine($"A5 Landscape: pageW=595.28pt pageH=419.53pt");
        _out.WriteLine($"Margins 10mm: left=right=28.35pt top=bottom=28.35pt");
        _out.WriteLine($"availableWidth=538.58pt");
        _out.WriteLine("");

        for (int p = 0; p < result.PageCount; p++)
        {
            var page = result.Pages[p];
            _out.WriteLine($"--- Page {p} ({page.Elements.Count} elements) ---");
            foreach (var el in page.Elements)
            {
                string tag = el.Source?.Source?.LocalName ?? el.Source?.GetType().Name ?? "?";
                string text = el.RenderedText != null ? $" \"{el.RenderedText}\"" : "";
                var r = el.Position;
                _out.WriteLine(
                    $"  [{tag}]{text} X={r.X:F1} Y={r.Y:F1} W={r.Width:F1} H={r.Height:F1}"
                    + $"  right={r.X + r.Width:F1}");
            }
        }

        // Key assertions to confirm hypothesis H6 (float content at wrong X):
        // Float 2 (w-50) is placed at X≈136pt. Any inline content at X<28pt would be
        // evidence of the ContentOriginX-for-floats bug.
        var allElements = result.Pages.SelectMany(pg => pg.Elements).ToList();
        var textElements = allElements.Where(e => e.RenderedText != null).ToList();

        _out.WriteLine("");
        _out.WriteLine($"=== Text elements ({textElements.Count} total) ===");
        foreach (var te in textElements)
        {
            _out.WriteLine($"  \"{te.RenderedText}\" at X={te.Position.X:F1} Y={te.Position.Y:F1}");
        }

        // Show float boxes specifically (float divs won't have RenderedText but will be BlockBox)
        var blockElements = allElements
            .Where(e => e.Source?.Source?.LocalName is "div" or "h2" or "p" or "table")
            .ToList();
        _out.WriteLine("");
        _out.WriteLine($"=== Block/table elements ({blockElements.Count} total) ===");
        foreach (var be in blockElements)
        {
            var r = be.Position;
            _out.WriteLine(
                $"  <{be.Source?.Source?.LocalName}> X={r.X:F1}..{r.X + r.Width:F1} Y={r.Y:F1} W={r.Width:F1}");
        }
    }
}
