// Phase 8.5 "Owned PDF Writer" — per-stage allocation probe for OwnedPdfWriter.
//
// Measures GC.GetTotalAllocatedBytes(precise:true) around each pipeline stage on the
// 50 KB stress template. Prints results to test output and asserts SC4 (≤288.96 MB total)
// for OwnedPdfWriter.
//
// The probe has InternalsVisibleTo access to the engine (see Muonroi.Pdf.csproj).

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Writer;
using Xunit.Abstractions;

namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// Allocation probe: runs each pipeline stage independently and captures
/// <c>GC.GetTotalAllocatedBytes(precise:true)</c> deltas. Tagged Category=SlowIntegration.
/// </summary>
[Collection(PdfRenderCollection.Name)]
[Trait("Category", "SlowIntegration")]
public sealed class AllocationProbe
{
    // SC4 threshold (ALLOC-01): total render ≤ 288.96 MB (30% below 412.8 MB v0.1 baseline).
    private const double Sc4ThresholdMb = 288.96;

    private const string ResourceName =
        "Muonroi.Pdf.Tests.TestResources.Perf.reference-50kb.html";

    private readonly ITestOutputHelper _out;

    public AllocationProbe(ITestOutputHelper output) => _out = output;

    [Fact]
    public async Task Probe_OwnedPdfWriter_PerStageAllocations()
    {
        string html = LoadReferenceTemplate();
        var options = new PdfRenderOptions();

        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();

        (double parseMb, IParsedDocument parsed) = await MeasureAsync("parse",
            async () =>
            {
                var parser = new Muonroi.Pdf.Governance.Parsing.AngleSharpHtmlParser();
                return await parser.ParseAsync(html, CancellationToken.None);
            });

        (double cascadeMb, IStyledDocument styled) = await MeasureAsync("cascade",
            async () =>
            {
                var cascade = new Muonroi.Pdf.Governance.Cascade.AngleSharpCascadeEngine();
                return await cascade.CascadeAsync(parsed, null, CancellationToken.None);
            });

        (double policyMb, _) = await MeasureAsync("policy",
            async () =>
            {
                var policy = new Muonroi.Pdf.Governance.Policies.DefaultStrictPolicy();
                return await policy.ValidateAsync((Muonroi.Pdf.Abstractions.Policy.IPdfDocumentContext)styled, CancellationToken.None);
            });

        var fontResolver = provider.GetRequiredService<IFontResolver>();
        var imageDecoder = provider.GetRequiredService<IImageDecoder>();

        (double layoutMb, IPositionedPageList pageList) = await MeasureAsync("layout",
            async () =>
            {
                var engine = new LayoutEngine();
                return await engine.LayoutAsync(styled, options, new PdfConfigs.PdfLimits(), fontResolver, null, imageDecoder, CancellationToken.None);
            });

        var writer = new OwnedPdfWriter();
        (double writeMb, long byteCount) = await MeasureAsync("write (OwnedPdfWriter)",
            async () =>
            {
                using var ms = new MemoryStream();
                return await writer.WriteAsync(pageList, options, ms, CancellationToken.None);
            });

        double totalMb = parseMb + cascadeMb + policyMb + layoutMb + writeMb;

        _out.WriteLine("=== OwnedPdfWriter allocation probe ===");
        _out.WriteLine($"  parse   : {parseMb,8:F2} MB");
        _out.WriteLine($"  cascade : {cascadeMb,8:F2} MB");
        _out.WriteLine($"  policy  : {policyMb,8:F2} MB");
        _out.WriteLine($"  layout  : {layoutMb,8:F2} MB");
        _out.WriteLine($"  write   : {writeMb,8:F2} MB");
        _out.WriteLine($"  TOTAL   : {totalMb,8:F2} MB  (SC4 threshold: {Sc4ThresholdMb} MB)");
        _out.WriteLine($"  PDF size: {byteCount:N0} bytes");
        _out.WriteLine(totalMb <= Sc4ThresholdMb ? "  SC4 MET ✓" : $"  SC4 NOT MET (delta: +{totalMb - Sc4ThresholdMb:F2} MB)");

        // SC4 assertion for the owned writer
        totalMb.Should().BeLessThanOrEqualTo(Sc4ThresholdMb,
            $"OwnedPdfWriter total allocation {totalMb:F2} MB must be ≤ SC4 threshold {Sc4ThresholdMb} MB");
    }

    // ── measurement helper ────────────────────────────────────────────────────

    private static async Task<(double Mb, T Result)> MeasureAsync<T>(string label, Func<Task<T>> fn)
    {
        // Force a GC collect to get a clean baseline.
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        long before = GC.GetTotalAllocatedBytes(precise: true);
        T result = await fn().ConfigureAwait(false);
        long after = GC.GetTotalAllocatedBytes(precise: true);

        double mb = (after - before) / (1024.0 * 1024.0);
        return (mb, result);
    }

    // ── resource loader ───────────────────────────────────────────────────────

    private static string LoadReferenceTemplate()
    {
        Assembly asm = typeof(AllocationProbe).Assembly;
        using Stream? stream = asm.GetManifestResourceStream(ResourceName);
        if (stream is null)
            throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
