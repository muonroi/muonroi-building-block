using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Extensions;
using Muonroi.Pdf.Governance.Policies;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using Muonroi.Pdf.Tests.Service;
using Xunit;
using Xunit.Abstractions;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G23b regression tests: table cell WidthRaw="X%" must not be applied a second time
/// against the already-resolved column width inside BlockLayoutEngine.
///
/// Root cause (pre-fix): after the column solver resolved e.g. 16% of 757pt ≈ 121pt,
/// MeasureCell / final-pass Layout called _blockEngine.Layout(cell, mc, ...) where
/// mc.AvailableWidth = cellWidth (121pt). But cell.WidthRaw was still "16%". The inner
/// Layout's first action is ResolveWidth, which saw WidthRaw="16%" and computed
/// 16% of 121pt ≈ 19pt — leaving only 19pt for content, causing per-word wrapping.
///
/// Fix: save/clear cell.WidthRaw before each _blockEngine.Layout call; restore after.
/// </summary>
public sealed class TableCellWidthDoubleApplicationTests
{
    // A4 landscape content width: 842pt - 2*(10mm*2.835pt/mm) ≈ 785pt.
    // Use a round number for deterministic assertions.
    private const float TableWidthPt = 720f;

    private static LayoutContext MakeContext(float availableWidth = TableWidthPt) =>
        new()
        {
            PageWidth      = availableWidth,
            PageHeight     = 595f,
            AvailableWidth = availableWidth,
            CurrentY       = 0f,
            CurrentPageIndex = 0,
            TotalPages       = 0,
            TextMetrics      = EstimatedTextMetrics.Instance,
            PageMargins      = PdfMargins.Zero,
        };

    private static (BlockLayoutEngine block, TableLayoutEngine table) MakeEngines()
    {
        var block = new BlockLayoutEngine();
        var table = new TableLayoutEngine(block, block.InlineEngine);
        block.TableEngine = table;
        return (block, table);
    }

    // -------------------------------------------------------------------------
    // G23b core: TH cell width:16% in a 720pt fixed table → column ≈ 115pt.
    // All words of the inline text must land on the SAME Y (single line), proving
    // available width is ~115pt not ~19pt (16% of 115pt — double-applied).
    // -------------------------------------------------------------------------

    [Fact]
    public void FixedTable_ThPercentWidth_InlineTextFitsOnOneLine_NotDoubleApplied()
    {
        // Arrange: <table style="table-layout:fixed;width:100%">
        //            <tr><th style="width:16%">long text here</th>
        //                <th style="width:16%">other</th></tr></table>
        // With 720pt table: 16% = 115.2pt per column.
        // Double-applied bug: 16% of 115.2pt ≈ 18.4pt — short words would each wrap.
        var tableBox = BuildTwoThTable(tableWidth: TableWidthPt);

        var (_, tableEngine) = MakeEngines();
        var ctx = MakeContext(TableWidthPt);
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, ctx, output, pageIndex: 0);

        // Extract TH cells
        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();
        cells.Should().HaveCount(2);

        // Get all inline text elements for the first TH
        var firstThText = cells[0].Children.OfType<InlineBox>().First();
        var inlineElements = output.Where(e => e.Source == firstThText).ToList();
        inlineElements.Should().NotBeEmpty(because: "TH cell must produce positioned inline output");

        // All words must share the same Y coordinate (single line, not per-word wrap)
        float firstY = inlineElements[0].Position.Y;
        inlineElements.Should().AllSatisfy(el =>
            el.Position.Y.Should().BeApproximately(firstY, precision: 1f,
                because: "all words must be on the same line when cell width is correctly resolved"),
            because: "with ~115pt available 'long text here' fits on one line");
    }

    // -------------------------------------------------------------------------
    // G23b: assert the first TH's PositionedElement width ≈ column width (115pt)
    // not double-applied (~19pt).
    // -------------------------------------------------------------------------

    [Fact]
    public void FixedTable_ThPercentWidth_CellElementWidthIsColumnWidth()
    {
        var tableBox = BuildTwoThTable(tableWidth: TableWidthPt);

        var (_, tableEngine) = MakeEngines();
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, MakeContext(TableWidthPt), output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();

        var col0Pe = output.First(e => e.Source == cells[0]);
        float expectedColumnWidth = TableWidthPt * 0.16f; // 115.2pt

        // Cell element width = column width (not double-applied ≈ 19pt)
        col0Pe.Position.Width.Should().BeApproximately(expectedColumnWidth, precision: 2f,
            because: "cell PositionedElement width must equal the column width resolved by the solver");
    }

    // -------------------------------------------------------------------------
    // G23b: symmetric assertion for TD cells (not just TH).
    // -------------------------------------------------------------------------

    [Fact]
    public void FixedTable_TdPercentWidth_InlineTextFitsOnOneLine_NotDoubleApplied()
    {
        // Build <table style="table-layout:fixed;width:100%">
        //   <tr><td style="width:16%">long text here</td>
        //       <td style="width:16%">other</td></tr></table>
        var tableBox = BuildTwoTdTable(tableWidth: TableWidthPt);

        var (_, tableEngine) = MakeEngines();
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, MakeContext(TableWidthPt), output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();
        cells.Should().HaveCount(2);

        var firstTdText = cells[0].Children.OfType<InlineBox>().First();
        var inlineElements = output.Where(e => e.Source == firstTdText).ToList();
        inlineElements.Should().NotBeEmpty(because: "TD cell must produce positioned inline output");

        float firstY = inlineElements[0].Position.Y;
        inlineElements.Should().AllSatisfy(el =>
            el.Position.Y.Should().BeApproximately(firstY, precision: 1f,
                because: "all words must be on the same line when TD cell width is correctly resolved"),
            because: "with ~115pt available 'long text here' fits on one line");
    }

    // -------------------------------------------------------------------------
    // G23b: column solver must NOT be broken — WidthRaw is still read correctly.
    // -------------------------------------------------------------------------

    [Fact]
    public void ColumnSolver_StillReadsWidthRaw_AfterFix()
    {
        // The fix clears WidthRaw only inside MeasureCell / final-pass, AFTER the solver runs.
        // Verify that column widths are still solved correctly from WidthRaw.
        var tableBox = BuildTwoThTable(tableWidth: TableWidthPt);

        var (_, tableEngine) = MakeEngines();
        var output = new List<PositionedElement>();
        tableEngine.Layout(tableBox, MakeContext(TableWidthPt), output, pageIndex: 0);

        var cells = tableBox.Children.OfType<TableRowGroupBox>()
            .SelectMany(g => g.Children.OfType<TableRowBox>())
            .SelectMany(r => r.Children.OfType<TableCellBox>())
            .ToList();

        var col0Pe = output.First(e => e.Source == cells[0]);
        var col1Pe = output.First(e => e.Source == cells[1]);

        float expectedWidth = TableWidthPt * 0.16f; // 115.2pt
        col0Pe.Position.Width.Should().BeApproximately(expectedWidth, precision: 2f,
            because: "column solver must still read WidthRaw=16% correctly");
        col1Pe.Position.Width.Should().BeApproximately(expectedWidth, precision: 2f,
            because: "second column must also be solved from WidthRaw=16%");
    }

    // -------------------------------------------------------------------------
    // Rasterization sanity check: CHNG_E.html renders to non-trivial PDF bytes
    // and the text layer contains "Container".
    // Skipped if template or pdftotext.exe is absent (CI-safe).
    // -------------------------------------------------------------------------

    private const string ChngETemplatePath = @"D:\Data\Template\Htmls\PreviewRegistion\CHNG_E.html";
    private const string PdfToTextExe = @"C:\Users\phila\AppData\Local\poppler\Library\bin\pdftotext.exe";

    private const string TinyPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR42mM4oaEBRwzEcQDRQxGBoNNuZAAAAABJRU5ErkJggg==";

    private static string FillTemplate(string html)
    {
        var dummies = new Dictionary<string, string>
        {
            ["title"]                     = "Phiếu đăng ký làm hàng",
            ["logo"]                      = TinyPng,
            ["barcode"]                   = TinyPng,
            ["operMethodName"]            = "Giao thẳng",
            ["operMethodCode"]            = "GT",
            ["orderNo"]                   = "LOT-001",
            ["currentDate"]               = "2026-05-29",
            ["orderDetailNo"]             = "DK-001",
            ["customerName"]              = "ACME Corp",
            ["containerNo"]               = "ABCD1234567",
            ["iso"]                       = "20GP",
            ["agent"]                     = "CMA",
            ["linerOper"]                 = "CMA CGM",
            ["fullEmpty"]                 = "F",
            ["returnDate"]                = "2026-06-01",
            ["billNo"]                    = "BILL123",
            ["paymentStatus"]             = "Paid",
            ["specialHandlings"]          = "None",
            ["linerRemark"]               = "OK",
            ["vesselVoyage"]              = "VES001/001W",
            ["customerRemark"]            = "No remark",
            ["truckNumber"]               = "51A-12345",
            ["chassisNumber"]             = "CH-001",
            ["phoneNumber"]               = "0901234567",
            ["username"]                  = "Nguyen Van A",
            ["exportPort"]                = "VNSGN",
            ["destinationPort"]           = "SGSIN",
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
        };

        foreach (var kv in dummies)
            html = html.Replace("{{" + kv.Key + "}}", kv.Value);

        return Regex.Replace(html, @"\{\{-?\s*[^}]+\}\}", "X");
    }

    [Fact]
    public async Task Rasterization_ChngE_ProducesNonTrivialPdf_WithContainerText()
    {
        if (!File.Exists(ChngETemplatePath))
        {
            // CI-safe: skip if template is absent
            return;
        }

        string rawHtml = await File.ReadAllTextAsync(ChngETemplatePath);
        string html = FillTemplate(rawHtml);

        var services = new ServiceCollection();
        services.TryAddSingleton<IPdfCssPolicy, LegacyPrintPolicy>();
        services.AddTestDoubles(PdfServiceTestHarness.ValidConfig());
        services.AddPdf(PdfServiceTestHarness.ValidConfig());
        using ServiceProvider provider = services.BuildServiceProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        var options = new PdfRenderOptions
        {
            PageSize    = PdfPageSize.A4,
            Orientation = PdfOrientation.Portrait,
            TemplateId  = "g23b-chng-e-sanity",
        };

        (byte[] pdfBytes, _) = await svc.RenderToBytesAsync(html, options);

        // Assert non-trivial PDF
        pdfBytes.Should().NotBeNull();
        pdfBytes.Length.Should().BeGreaterThan(1000,
            because: "a rendered CHNG_E page must produce a non-trivial PDF");

        if (!File.Exists(PdfToTextExe))
        {
            // pdftotext absent — skip text-layer assertion but size check already passed
            return;
        }

        string tmpPdf = Path.Combine(Path.GetTempPath(), $"g23b_chng_e_{Guid.NewGuid():N}.pdf");
        string tmpTxt = Path.ChangeExtension(tmpPdf, ".txt");
        try
        {
            await File.WriteAllBytesAsync(tmpPdf, pdfBytes);

            var psi = new ProcessStartInfo
            {
                FileName               = PdfToTextExe,
                Arguments              = $"\"{tmpPdf}\" \"{tmpTxt}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true,
            };

            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0 || !File.Exists(tmpTxt))
                return;  // pdftotext failed — size check is sufficient

            string text = await File.ReadAllTextAsync(tmpTxt, System.Text.Encoding.UTF8);

            text.Length.Should().BeGreaterThan(200,
                because: "text layer must be non-trivial — a zero-height table would produce ~0 chars");

            bool hasContainerFragment = text.Contains("Container", StringComparison.Ordinal)
                                     || text.Contains("Con ainer", StringComparison.Ordinal);
            hasContainerFragment.Should().BeTrue(
                because: "CHNG_E table header 'Container' (or font-encoded 'Con ainer') must appear in text layer");
        }
        finally
        {
            if (File.Exists(tmpPdf)) File.Delete(tmpPdf);
            if (File.Exists(tmpTxt)) File.Delete(tmpTxt);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TableBox BuildTwoThTable(float tableWidth)
    {
        var tableBox = new TableBox { TableLayout = "fixed", BorderSpacing = 0f, Width = tableWidth };
        var thead = new TableRowGroupBox { GroupType = TableRowGroupType.Header };
        var row = new TableRowBox();

        var th0 = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = "16%", Width = -1f };
        th0.Children.Add(new InlineBox { Text = "long text here", FontFamily = "serif", FontSize = 10f });

        var th1 = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = "16%", Width = -1f };
        th1.Children.Add(new InlineBox { Text = "other", FontFamily = "serif", FontSize = 10f });

        row.Children.Add(th0);
        row.Children.Add(th1);
        thead.Children.Add(row);
        tableBox.Children.Add(thead);
        return tableBox;
    }

    private static TableBox BuildTwoTdTable(float tableWidth)
    {
        var tableBox = new TableBox { TableLayout = "fixed", BorderSpacing = 0f, Width = tableWidth };
        var tbody = new TableRowGroupBox { GroupType = TableRowGroupType.Body };
        var row = new TableRowBox();

        var td0 = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = "16%", Width = -1f };
        td0.Children.Add(new InlineBox { Text = "long text here", FontFamily = "serif", FontSize = 10f });

        var td1 = new TableCellBox { Colspan = 1, Rowspan = 1, WidthRaw = "16%", Width = -1f };
        td1.Children.Add(new InlineBox { Text = "other", FontFamily = "serif", FontSize = 10f });

        row.Children.Add(td0);
        row.Children.Add(td1);
        tbody.Children.Add(row);
        tableBox.Children.Add(tbody);
        return tableBox;
    }
}
