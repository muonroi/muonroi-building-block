namespace Muonroi.Pdf.Tests.Diagnostic;

/// <summary>
/// Phase 8.16 Wave C — C2 charter item: image render audit across all production templates.
///
/// Renders every *.html file in the PreviewRegistion template directory and reports
/// per-template render status (OK / ERR) and page count.  Substitutes {{logo}} with
/// <see cref="LogoStubTests.RealLogoBase64"/> so the audit reflects the real 32×32 PNG
/// stub introduced in Wave B (#33).  Any remaining {{...}} / {{ ... }} tokens are replaced
/// with an inline dummy so the engine receives structurally complete HTML.
///
/// PDFs are written to D:\sources\TEP\audit-816\ for subsequent rasterisation and visual
/// inspection.  The test is deliberately lenient: it does NOT assert page counts or image
/// positions — those are audited manually and documented in AUDIT.md.  Only a hard render
/// exception counts as a test failure (the audit must complete without crashing the engine).
///
/// This test is long-lived and kept in [Collection("DiagnosticSerial")] so it does not run
/// in parallel with other diagnostic tests that also build ServiceProvider instances.
/// </summary>
[Collection("DiagnosticSerial")]
public sealed class TemplateImageAudit(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _out = output;
    private const string TemplateDir = @"D:\Data\Template\Htmls\PreviewRegistion";
    private const string OutDir = @"D:\sources\TEP\audit-816";

    // Minimal 4×4 PNG stub for non-logo image tokens (barcode, etc.).
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR42mM4oaEBRwzEcQDRQxGBoNNuZAAAAABJRU5ErkJggg==";

    /// <summary>
    /// Fill all {{token}} and {{ token }} placeholders in the template HTML so the engine
    /// receives structurally complete HTML.  Known image tokens are substituted with real
    /// (or tiny) PNG data URIs; all other tokens receive a generic text dummy.
    /// </summary>
    private static string FillTemplate(string html)
    {
        // Known image placeholders
        html = html.Replace("{{logo}}", LogoStubTests.RealLogoBase64);
        html = html.Replace("{{barcode}}", TinyPngBase64);

        // Space-delimited variants used by _F templates (Scriban-style)
        html = html.Replace("{{ logo }}", LogoStubTests.RealLogoBase64);
        html = html.Replace("{{ barcode }}", TinyPngBase64);

        // Replace loop/control constructs ({{ for ... }}, {{ end }}, {{ if ... }}) with ""
        html = Regex.Replace(html, @"\{\{-?\s*(for|end|if|else)\b[^}]*\}\}", string.Empty);

        // Replace all remaining {{ ... }} tokens with "X"
        html = Regex.Replace(html, @"\{\{-?\s*[^}]+\}\}", "X");

        return html;
    }

    [Fact]
    public async Task Render_All_Templates_To_Pdf()
    {
        if (!Directory.Exists(TemplateDir))
        {
            _out.WriteLine($"SKIP: template directory not found — {TemplateDir}");
            return;
        }

        Directory.CreateDirectory(OutDir);

        string[] files = Directory.GetFiles(TemplateDir, "*.html");
        Array.Sort(files);

        _out.WriteLine($"Templates found: {files.Length}");
        _out.WriteLine($"Output directory: {OutDir}");
        _out.WriteLine(new string('-', 72));

        using ServiceProvider sp = PdfServiceTestHarness.BuildProvider();
        var svc = sp.GetRequiredService<IMPdfService>();

        int okCount = 0;
        int errCount = 0;

        foreach (string file in files)
        {
            string name = Path.GetFileNameWithoutExtension(file);
            string rawHtml = await File.ReadAllTextAsync(file);
            string html = FillTemplate(rawHtml);

            var options = new PdfRenderOptions
            {
                PageSize = PdfPageSize.A4,
                Orientation = PdfOrientation.Portrait,
                TemplateId = name,
            };

            string outPath = Path.Combine(OutDir, name + ".pdf");

            try
            {
                await using FileStream fs = File.Create(outPath);
                PdfRenderResult result = await svc.RenderAsync(html, fs, options, CancellationToken.None);
                _out.WriteLine($"OK   {name,-14} pages={result.PageCount}");
                okCount++;
            }
            catch (Exception ex)
            {
                _out.WriteLine($"ERR  {name,-14} {ex.GetType().Name}: {ex.Message}");
                errCount++;
            }
        }

        _out.WriteLine(new string('-', 72));
        _out.WriteLine($"Result: {okCount} OK / {errCount} ERR / {files.Length} total");

        // Audit must not crash the engine on any template.
        // If errors occurred they are recorded in the output above for AUDIT.md authoring.
        Assert.Equal(0, errCount);
    }
}
