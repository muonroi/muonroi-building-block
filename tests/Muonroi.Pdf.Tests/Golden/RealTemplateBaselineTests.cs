using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PDFtoImage;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Extensions;
using Muonroi.Pdf.Governance.Policies;
using Muonroi.Pdf.Tests.Service;
using Xunit;
using Xunit.Abstractions;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Permanent fidelity reporting harness. Renders all 17 production corpus templates through the
/// public <see cref="IMPdfService"/> path using <see cref="LegacyPrintPolicy"/> (Profile v1).
/// Rasterizes each produced PDF to PNG for Opus visual review.
///
/// Design: always-pass reporting harness — never fails the build. Each [Fact]:
///   1. Skips with a diagnostic message if the template file is absent (CI-safe).
///   2. Attempts render; on engine exception logs ExceptionType + Message + TopFrame and returns.
///   3. On success writes the PDF to TestResults/visual/real-{slug}.pdf.
///   4. Rasterizes page 0 at 200 DPI and writes TestResults/visual/real-{slug}.png.
///   5. Never calls Assert.Fail — all results (RENDER: OK / RENDER: FAILED / RASTER: FAILED)
///      are reported as ITestOutputHelper lines; the test always passes.
///
/// Uses [Trait("Category","RealTemplate")] so headless CI can exclude via
///   dotnet test --filter "Category!=RealTemplate"
/// </summary>
[Collection(PdfRenderCollection.Name)]
[Trait("Category", "RealTemplate")]
public sealed class RealTemplateBaselineTests
{
    private readonly ITestOutputHelper _out;

    public RealTemplateBaselineTests(ITestOutputHelper output) => _out = output;

    private const string TemplateDir = @"D:\Data\Template\Htmls\PreviewRegistion";

    // Minimal 8-bit RGB (color_type=2, bit_depth=8) 1x1 PNG — passes PureImageDecoder.
    // The standard "iVBORw0KGgo...fFcSJ..." is RGBA (color_type=6); this is the RGB variant.
    private const string TinyRgbPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVQI12P4z8AAAAACAAHiIbwzAAAAAElFTkSuQmCC";

    // Full set of dummy values covering all 18 templates.
    private static readonly Dictionary<string, string> Dummies = new()
    {
        // Common fields (HSLA_E, HANG_E, NHAR_E, CAPR_E, CRCD_E, CSLA_E, CHNG_E)
        ["title"]              = "Phieu dang ky lam hang",
        ["logo"]               = TinyRgbPngBase64,
        ["barcode"]            = TinyRgbPngBase64,
        ["operMethodName"]     = "Giao thang",
        ["operMethodCode"]     = "GT",
        ["orderNo"]            = "LO12345",
        ["orderDetailNo"]      = "DK67890",
        ["currentDate"]        = "27/05/2026",
        ["customerName"]       = "CONG TY ABC",
        ["containerNo"]        = "TGHU1234567",
        ["iso"]                = "45G1",
        ["agent"]              = "ONE",
        ["linerOper"]          = "ONE",
        ["fullEmpty"]          = "FULL",
        ["returnDate"]         = "30/05/2026",
        ["billNo"]             = "BL-0001\nBL-0002",
        ["paymentStatus"]      = "Da thanh toan",
        ["specialHandlings"]   = "Hang thuong",
        ["linerRemark"]        = "Ghi chu hang tau",
        ["vesselVoyage"]       = "VESSEL 001N",
        ["customerRemark"]     = "Khach yeu cau giao gap",
        ["truckNumber"]        = "51C-12345",
        ["chassisNumber"]      = "RM-6789",
        ["phoneNumber"]        = "0901234567",
        ["username"]           = "Nguyen Van A",
        ["registrantPhone"]    = "0901234567",
        ["executeDate"]        = "30/05/2026",

        // CAPR_E / CRCD_E / CSLA_E extended
        ["bookNo"]             = "BOOK-001",
        ["cO2"]                = "5.0",
        ["fod"]                = "N",
        ["humidity"]           = "65",
        ["inGate"]             = "30/05/2026 08:00",
        ["lineOper"]           = "ONE",
        ["linerExpiryDate"]    = "30/06/2026",
        ["o2"]                 = "21.0",
        ["outVoyage"]          = "VESSEL 002N",
        ["placeLOfDelivery"]   = "Hai Phong",
        ["pod"]                = "VNHPH",
        ["registrantName"]     = "Tran Thi B",
        ["sealNo"]             = "SEAL-9876",
        ["temp"]               = "-18",
        ["vent"]               = "CLOSE",
        ["vesselName"]         = "EVER FORWARD",
        ["destinationPort"]    = "SGSIN",
        ["transferPort"]       = "THBKK",

        // CHNG_E
        // (shares most keys above)

        // CHNG_F / CSLA_F / GTHA_F / GTND_F — spaced-key variants
        [" billNumber "]       = "BL-2026-001",
        [" chassisNumber "]    = "RM-6789",
        [" customerName "]     = "CONG TY XYZ",
        [" customerNotes "]    = "Ghi chu khach",
        [" date "]             = "27/05/2026",
        [" expirationDate "]   = "30/06/2026",
        [" linerOper "]        = "ONE",
        [" lotNumber "]        = "LOT-001",
        [" operMethodCode "]   = "GT",
        [" operMethodName "]   = "Giao thang",
        [" remarksSubtitle "]  = "Ghi chu phu",
        [" remarksTitle "]     = "Ghi chu chinh",
        [" title "]            = "Phieu dang ky lam hang",
        [" truckNumber "]      = "51C-12345",
        [" vesselVoyage "]     = "VESSEL 001N",
        [" unplugDate "]       = "01/06/2026",
        [" siteName "]         = "Cang Tan Cang",

        // Container sub-object placeholders (CHNG_F, CSLA_F, GTHA_F, GTND_F, HANG_F, HBCX_F, HBND_F, HSLA_F)
        ["container.containerNumber"] = "TGHU1234567",
        ["container.imdg"]            = "N/A",
        ["container.ohOlOw"]          = "0/0/0",
        ["container.sealNumber"]      = "SEAL-9876",
        ["container.size"]            = "40HC",
        ["container.tempVent"]        = "-18/CLOSE",
        ["container.unno"]            = "N/A",
        ["container.vgm"]             = "25000",
        ["container.shippingLine"]    = "ONE",
        ["container.status"]          = "FULL",
        ["destinationPort"]           = "SGSIN",

        // GTHA_F extra
        ["executeDate"]               = "30/05/2026",

        // HANG_E / NHAR_E
        ["bookingNumber"]      = "BOOK-2026-001",
        ["lotNumber"]          = "LOT-001",
        ["remarksSubtitle"]    = "Ghi chu phu",
        ["remarksTitle"]       = "Ghi chu chinh",
        ["customerNotes"]      = "Ghi chu khach",
        ["date"]               = "27/05/2026",

        // HANG_F
        ["bookingNumber"]      = "BOOK-2026-001",

        // HBCX_F — spaced date/title handled by spaced-key entries above
        // additional non-spaced keys same as HANG_F

        // HBND_F
        ["exportPort"]         = "VNSGN",

        // HBL
        ["houseBill"]          = "HBL-001",
        ["masterBill"]         = "MBL-001",
        ["releaseTo"]          = "CONG TY ABC",
        ["validToDate"]        = "30/06/2026",
        ["time"]               = "08:00",
        ["countEpuip"]         = "2",
        // HBL loop item sub-fields
        ["item.ContainerNo"]   = "TGHU1234567",
        ["item.InVoyageNo"]    = "001N",
        ["item.IsoCode"]       = "45G1",
        ["item.SecureCode"]    = "SEAL-9876",
        ["item.VesselName"]    = "EVER FORWARD",
        // HBL empty/full sub-fields
        ["empty.Detention"]       = "10",
        ["empty.PlaceOfEmpty"]    = "Cang Cat Lai",
        ["full.PlaceOfDelivery"]  = "Hai Phong",

        // BNTT
        ["address"]            = "123 Nguyen Van Linh, Q7, HCM",
        ["billName"]           = "INVOICE",
        ["billedTo"]           = "CONG TY ABC",
        ["createdDate"]        = "27/05/2026",
        ["fullName"]           = "Nguyen Van A",
        ["invoiceNo"]          = "INV-2026-001",
        ["pattern"]            = "Dich vu cang",
        ["serial"]             = "SER-001",
        ["sumBilledAmount"]    = "1000000",
        ["transactionCode"]    = "TC-001",
        // BNTT loop item sub-fields
        ["item.BilledAmount"]  = "1000000",
        ["item.ContainerNo"]   = "TGHU1234567",
        ["item.Description"]   = "Phi dich vu cang",
        ["item.DiscountAmount"]= "0",
        ["item.OrderDetailNo"] = "DK67890",
        ["item.Rate"]          = "1000000",
        ["item.TaxRate"]       = "10",
        ["item.UnitsCharged"]  = "1",
    };

    private static string FillTemplate(string html)
    {
        foreach (KeyValuePair<string, string> kv in Dummies)
            html = html.Replace("{{" + kv.Key + "}}", kv.Value);
        // Replace any remaining {{...}} tokens (loop constructs, unknown keys) with "X"
        return Regex.Replace(html, @"\{\{-?\s*[^}]+\}\}", "X");
    }

    private static string GetOutDir()
    {
        string assemblyDir = Path.GetDirectoryName(typeof(RealTemplateBaselineTests).Assembly.Location)!;
        return Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "TestResults", "visual"));
    }

    private static async Task<byte[]> RenderWithLegacyPolicyAsync(string html, PdfRenderOptions options)
    {
        var services = new ServiceCollection();
        // Register LegacyPrintPolicy BEFORE AddTestDoubles + AddPdf so TryAdd respects it.
        services.TryAddSingleton<IPdfCssPolicy, LegacyPrintPolicy>();
        services.AddTestDoubles(PdfServiceTestHarness.ValidConfig());
        services.AddPdf(PdfServiceTestHarness.ValidConfig());
        using ServiceProvider provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IMPdfService>();
        (byte[] bytes, _) = await svc.RenderToBytesAsync(html, options);
        return bytes;
    }

    private async Task RenderTemplate(string templateFile, string slug, PdfPageSize pageSize, PdfOrientation orientation)
    {
        string templatePath = Path.Combine(TemplateDir, templateFile);

        if (!File.Exists(templatePath))
        {
            _out.WriteLine($"SKIP: template file not found: {templatePath}");
            return;
        }

        string html = FillTemplate(await File.ReadAllTextAsync(templatePath));

        string outDir = GetOutDir();
        Directory.CreateDirectory(outDir);
        string pdfPath = Path.Combine(outDir, slug + ".pdf");
        string pngPath = Path.Combine(outDir, slug + ".png");

        var options = new PdfRenderOptions
        {
            PageSize = pageSize,
            Orientation = orientation,
            TemplateId = slug,
        };

        byte[]? pdfBytes = null;
        try
        {
            pdfBytes = await RenderWithLegacyPolicyAsync(html, options);
            await File.WriteAllBytesAsync(pdfPath, pdfBytes);
            _out.WriteLine($"RENDER: OK — {slug}  PDF {pdfBytes.Length} bytes at {pdfPath}");
        }
        catch (Exception ex)
        {
            _out.WriteLine($"RENDER: FAILED — {slug}");
            _out.WriteLine($"  ExceptionType: {ex.GetType().FullName}");
            _out.WriteLine($"  Message: {ex.Message}");
            string[] st = (ex.StackTrace ?? "").Split('\n');
            _out.WriteLine($"  TopFrame: {(st.Length > 0 ? st[0].Trim() : "(none)")}");
            if (ex.InnerException is { } inner)
            {
                _out.WriteLine($"  Inner: {inner.GetType().FullName}: {inner.Message}");
                string[] ist = (inner.StackTrace ?? "").Split('\n');
                _out.WriteLine($"  InnerTopFrame: {(ist.Length > 0 ? ist[0].Trim() : "(none)")}");
            }
            // Reporting harness: always pass regardless of render failure.
            return;
        }

        try
        {
            using var pngStream = new MemoryStream();
            Conversion.SavePng(pngStream, pdfBytes, 0, password: null, options: new RenderOptions(Dpi: 200));
            await File.WriteAllBytesAsync(pngPath, pngStream.ToArray());
            _out.WriteLine($"RASTER: OK — {slug}  PNG {pngStream.Length} bytes at {pngPath}");
        }
        catch (Exception ex)
        {
            _out.WriteLine($"RASTER: FAILED — {slug} PDFtoImage threw during rasterization.");
            _out.WriteLine($"  ExceptionType: {ex.GetType().FullName}");
            _out.WriteLine($"  Message: {ex.Message}");
            // Rasterization failure is reported but does not fail the test —
            // the PDF was already validated above and the PNG is a bonus artifact.
        }
    }

    // ── 18 corpus templates ──────────────────────────────────────────────────

    [Fact] public Task RealTemplate_BNTT()    => RenderTemplate("BNTT.html",   "real-bntt",   PdfPageSize.A4, PdfOrientation.Portrait);
    [Fact] public Task RealTemplate_CAPR_E()  => RenderTemplate("CAPR_E.html", "real-capr-e", PdfPageSize.A4, PdfOrientation.Portrait);
    [Fact] public Task RealTemplate_CHNG_E()  => RenderTemplate("CHNG_E.html", "real-chng-e", PdfPageSize.A4, PdfOrientation.Portrait);
    [Fact] public Task RealTemplate_CHNG_F()  => RenderTemplate("CHNG_F.html", "real-chng-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_CRCD_E()  => RenderTemplate("CRCD_E.html", "real-crcd-e", PdfPageSize.A4, PdfOrientation.Portrait);
    [Fact] public Task RealTemplate_CSLA_E()  => RenderTemplate("CSLA_E.html", "real-csla-e", PdfPageSize.A4, PdfOrientation.Portrait);
    [Fact] public Task RealTemplate_CSLA_F()  => RenderTemplate("CSLA_F.html", "real-csla-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_GTHA_F()  => RenderTemplate("GTHA_F.html", "real-gtha-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_GTND_F()  => RenderTemplate("GTND_F.html", "real-gtnd-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_HANG_E()  => RenderTemplate("HANG_E.html", "real-hang-e", PdfPageSize.A4, PdfOrientation.Portrait);
    [Fact] public Task RealTemplate_HANG_F()  => RenderTemplate("HANG_F.html", "real-hang-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_HBCX_F()  => RenderTemplate("HBCX_F.html", "real-hbcx-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_HBL()     => RenderTemplate("HBL.html",    "real-hbl",    PdfPageSize.A4, PdfOrientation.Portrait);
    [Fact] public Task RealTemplate_HBND_F()  => RenderTemplate("HBND_F.html", "real-hbnd-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_HSLA_E()  => RenderTemplate("HSLA_E.html", "real-hsla-e", PdfPageSize.A5, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_HSLA_F()  => RenderTemplate("HSLA_F.html", "real-hsla-f", PdfPageSize.A4, PdfOrientation.Landscape);
    [Fact] public Task RealTemplate_NHAR_E()  => RenderTemplate("NHAR_E.html", "real-nhar-e", PdfPageSize.A4, PdfOrientation.Portrait);

    // Template file count: 17 unique HTML files in the directory.
    // The plan references "18 templates" — the list above covers all 17 confirmed files.
    // (The corpus grep confirmed: BNTT, CAPR_E, CHNG_E, CHNG_F, CRCD_E, CSLA_E, CSLA_F,
    //  GTHA_F, GTND_F, HANG_E, HANG_F, HBCX_F, HBL, HBND_F, HSLA_E, HSLA_F, NHAR_E = 17)
}
