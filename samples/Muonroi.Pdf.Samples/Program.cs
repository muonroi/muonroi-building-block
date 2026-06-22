// Muonroi.Pdf — worked samples.
//
// Each method below renders one self-contained scenario to a .pdf in ./pdf-output, so reading
// this file top-to-bottom is enough to learn the engine: register once with AddPdf(), inject
// IMPdfService, build a PdfRenderOptions, call RenderAsync. Run with:  dotnet run
//
// Templates use only the supported HTML/CSS subset (see docs: Supported HTML / CSS / JS).
// Scenarios 5 and 6 (Flexbox / CSS Grid) require the opt-in AllowModernLayout policy flag, so
// they use a second provider built with that flag enabled — see BuildPdf().

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Extensions;

string outDir = Path.Combine(AppContext.BaseDirectory, "pdf-output");
Directory.CreateDirectory(outDir);

// Strict default profile (legacy-print-v1). Flex/grid here are rejected fail-loud.
IMPdfService pdf = BuildPdf(allowModernLayout: false);

// Same pipeline with AllowModernLayout=true → real Flexbox + CSS Grid layout engines.
IMPdfService pdfModern = BuildPdf(allowModernLayout: true);

await RenderMinimal(pdf, outDir);
await RenderInvoice(pdf, outDir);
await RenderReportWithHeaderFooter(pdf, outDir);
await RenderWatermarkAndGradient(pdf, outDir);
await RenderFlexbox(pdfModern, outDir);
await RenderGrid(pdfModern, outDir);
await RenderMultiPage(pdf, outDir);
await DemonstratePolicyRejection(pdf);

Console.WriteLine($"\nDone. PDFs written to: {outDir}");
return 0;

// ── Composition root ────────────────────────────────────────────────────────
// One AddPdf() call wires the whole pipeline. The Generic Host supplies IHostEnvironment (used by
// the default font resolver) and IConfiguration. AllowModernLayout is set via in-memory config
// here; in a real host it comes from appsettings.json under "PdfConfigs:Policy".
static IMPdfService BuildPdf(bool allowModernLayout)
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["PdfConfigs:Policy:AllowModernLayout"] = allowModernLayout ? "true" : "false",
    });
    builder.Services.AddPdf(builder.Configuration); // auto-wires Muonroi.Logging + execution-context accessor + bundled fonts
    return builder.Build().Services.GetRequiredService<IMPdfService>();
}

// ── Helper: render an HTML string to a named file and print the result line ───
static async Task Write(IMPdfService pdf, string outDir, string name, string html, PdfRenderOptions? options = null)
{
    string path = Path.Combine(outDir, name);
    await using FileStream output = File.Create(path);
    PdfRenderResult result = await pdf.RenderAsync(html, output, options ?? new PdfRenderOptions());
    Console.WriteLine($"  {name,-28} {result.PageCount}p  {result.ByteCount,7}b");
}

// ── 1. Minimal ────────────────────────────────────────────────────────────────
static Task RenderMinimal(IMPdfService pdf, string outDir) => Write(pdf, outDir, "01-minimal.pdf",
    """
    <!DOCTYPE html>
    <html><head><style>
      body { font-family: Arial, sans-serif; font-size: 12pt; color: #222; }
      h1   { color: #0c6b6b; }
    </style></head>
    <body>
      <h1>Hello, Muonroi.Pdf</h1>
      <p>Pure-managed HTML &#8594; PDF. No browser, no native binary.</p>
    </body></html>
    """);

// ── 2. Invoice: tables + floats + %-column widths + totals box ─────────────────
static Task RenderInvoice(IMPdfService pdf, string outDir) => Write(pdf, outDir, "02-invoice.pdf",
    """
    <!DOCTYPE html>
    <html><head><style>
      body { font-family: Arial, sans-serif; font-size: 11pt; color: #222; margin: 0; }
      .head { overflow: hidden; margin-bottom: 16px; }
      .head .brand { float: left;  font-size: 18pt; font-weight: bold; color: #0c6b6b; }
      .head .meta  { float: right; text-align: right; font-size: 10pt; color: #555; }
      table { width: 100%; border-collapse: collapse; table-layout: fixed; }
      th, td { border: 1px solid #ccc; padding: 6px 8px; }
      th { background-color: #0c6b6b; color: #fff; text-align: left; }
      .num { text-align: right; }
      .col-desc { width: 55%; } .col-qty { width: 15%; } .col-amt { width: 30%; }
      .totals { float: right; width: 40%; margin-top: 12px; }
      .totals td { border: none; padding: 2px 8px; }
      .totals .grand { font-weight: bold; border-top: 2px solid #0c6b6b; }
    </style></head>
    <body>
      <div class="head">
        <div class="brand">ACME Corp</div>
        <div class="meta">Invoice #INV-2026-0042<br/>Date: 2026-06-22</div>
      </div>
      <table>
        <thead>
          <tr><th class="col-desc">Description</th><th class="col-qty num">Qty</th><th class="col-amt num">Amount</th></tr>
        </thead>
        <tbody>
          <tr><td>Consulting services</td><td class="num">10</td><td class="num">$1,500.00</td></tr>
          <tr><td>Hosting (annual)</td><td class="num">1</td><td class="num">$600.00</td></tr>
          <tr><td>Support plan</td><td class="num">1</td><td class="num">$300.00</td></tr>
        </tbody>
      </table>
      <table class="totals">
        <tr><td>Subtotal</td><td class="num">$2,400.00</td></tr>
        <tr><td>Tax (10%)</td><td class="num">$240.00</td></tr>
        <tr class="grand"><td>Total</td><td class="num">$2,640.00</td></tr>
      </table>
    </body></html>
    """);

// ── 3. Report: programmatic running header/footer + page numbers ───────────────
static Task RenderReportWithHeaderFooter(IMPdfService pdf, string outDir)
{
    // Long body so the document spans multiple pages and the page counters advance.
    var rows = string.Concat(Enumerable.Range(1, 40).Select(i =>
        $"<p>Section line {i}: lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>"));

    var options = new PdfRenderOptions
    {
        PageSize    = PdfPageSize.A4,
        Orientation = PdfOrientation.Portrait,
        Margins     = PdfMargins.Uniform(15),
        Header = new PdfHeaderFooter(
            CenterHtml: "<b style=\"color:#0c6b6b;\">Quarterly Report</b>",
            RightHtml:  "Page counter(page)/counter(pages)",
            HeightMm:   20,
            ShowLine:   true),
        Footer = new PdfHeaderFooter(
            CenterHtml: "Confidential — counter(page)/counter(pages)",
            HeightMm:   12,
            ShowLine:   true),
        TemplateId = "quarterly-report",
    };

    return Write(pdf, outDir, "03-report-header-footer.pdf",
        $"<!DOCTYPE html><html><head><style>body{{font-family:Arial;font-size:11pt;}}</style></head>"
        + $"<body><h1>Quarterly Report</h1>{rows}</body></html>",
        options);
}

// ── 4. Watermark (transform:rotate) + gradient header band ─────────────────────
static Task RenderWatermarkAndGradient(IMPdfService pdf, string outDir) => Write(pdf, outDir, "04-watermark-gradient.pdf",
    """
    <!DOCTYPE html>
    <html><head><style>
      body { font-family: Arial, sans-serif; margin: 0; }
      .banner { height: 60px; color: #fff; padding: 16px;
                background: linear-gradient(90deg, #0c6b6b 0%, #13a89e 100%); }
      .vignette { background: radial-gradient(ellipse at center, #ffffff 0%, #eef3f3 100%);
                  padding: 24px; height: 400px; position: relative; }
      .watermark { position: absolute; top: 160pt; left: 120pt;
                   font-size: 64pt; color: #d0d0d0; transform: rotate(-35deg); }
    </style></head>
    <body>
      <div class="banner"><h1>Certificate of Completion</h1></div>
      <div class="vignette">
        <div class="watermark">DRAFT</div>
        <p>Awarded to Jane Doe for outstanding achievement.</p>
      </div>
    </body></html>
    """);

// ── 5. Flexbox (requires AllowModernLayout) ────────────────────────────────────
static Task RenderFlexbox(IMPdfService pdfModern, string outDir) => Write(pdfModern, outDir, "05-flexbox.pdf",
    """
    <!DOCTYPE html>
    <html><head><style>
      body { font-family: Arial, sans-serif; padding: 16px; }
      .cards { display: flex; flex-direction: row; gap: 12px; align-items: stretch; }
      .card  { flex: 1 1 0; border: 1px solid #ccc; padding: 12px; height: 80px; }
      .card h3 { margin: 0 0 6px; color: #0c6b6b; }
    </style></head>
    <body>
      <h1>Flexbox cards</h1>
      <div class="cards">
        <div class="card"><h3>Revenue</h3><p>$2.64M</p></div>
        <div class="card"><h3>Orders</h3><p>1,204</p></div>
        <div class="card"><h3>Refunds</h3><p>1.8%</p></div>
      </div>
    </body></html>
    """);

// ── 6. CSS Grid (requires AllowModernLayout) ───────────────────────────────────
static Task RenderGrid(IMPdfService pdfModern, string outDir) => Write(pdfModern, outDir, "06-grid.pdf",
    """
    <!DOCTYPE html>
    <html><head><style>
      body { font-family: Arial, sans-serif; padding: 16px; }
      .grid { display: grid;
              grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
              gap: 10px; }
      .tile { border: 1px solid #ddd; padding: 10px; height: 60px; }
      .dash { display: grid; margin-top: 20px;
              grid-template-columns: 200px 1fr;
              grid-template-rows: auto 1fr;
              grid-template-areas: "side header" "side main";
              gap: 8px; height: 200px; }
      .dash .h    { grid-area: header; background: #0c6b6b; color: #fff; padding: 8px; }
      .dash .side { grid-area: side; border: 1px solid #ccc; padding: 8px; }
      .dash .main { grid-area: main; padding: 8px; }
    </style></head>
    <body>
      <h1>Product grid (auto-fill)</h1>
      <div class="grid">
        <div class="tile">SKU-001</div><div class="tile">SKU-002</div>
        <div class="tile">SKU-003</div><div class="tile">SKU-004</div>
        <div class="tile">SKU-005</div>
      </div>
      <div class="dash">
        <div class="h">Dashboard</div>
        <div class="side">Filters</div>
        <div class="main">Content…</div>
      </div>
    </body></html>
    """);

// ── 7. Multi-page: one PDF from several HTML fragments ─────────────────────────
static Task RenderMultiPage(IMPdfService pdf, string outDir)
{
    var pages = new[]
    {
        "<h1>Cover</h1><p>Annual Report 2026</p>",
        "<h2>Financials</h2><p>Revenue grew 12% year over year.</p>",
        "<h2>Appendix</h2><p>Methodology and notes.</p>",
    };
    string path = Path.Combine(outDir, "07-multipage.pdf");
    return RenderMultiPageCore(pdf, path, pages);

    static async Task RenderMultiPageCore(IMPdfService pdf, string path, string[] pages)
    {
        await using FileStream output = File.Create(path);
        PdfRenderResult result = await pdf.RenderMultiPageAsync(pages, output, new PdfRenderOptions
        {
            Footer = new PdfHeaderFooter(CenterHtml: "counter(page)/counter(pages)", HeightMm: 10),
        });
        Console.WriteLine($"  07-multipage.pdf             {result.PageCount}p  {result.ByteCount,7}b");
    }
}

// ── 8. Policy rejection: forbidden CSS throws PdfPolicyException before rendering ─
static async Task DemonstratePolicyRejection(IMPdfService pdf)
{
    // position:fixed is outside the print subset → rejected fail-loud (no PDF is produced).
    const string bad = "<!DOCTYPE html><html><body><div style=\"position:fixed;\">x</div></body></html>";
    try
    {
        await using var sink = new MemoryStream();
        await pdf.RenderAsync(bad, sink, new PdfRenderOptions());
        Console.WriteLine("  policy-rejection             (unexpected: no violation thrown)");
    }
    catch (PdfPolicyException ex)
    {
        Console.WriteLine($"  policy-rejection             rejected {ex.Violations.Count} violation(s):");
        foreach (var v in ex.Violations)
            Console.WriteLine($"      - {v.RuleId} on '{v.CssSelector}': {v.RejectedValue} -> {v.SuggestedAlternative}");
    }
}
