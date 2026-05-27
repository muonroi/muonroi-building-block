using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Muonroi.Pdf.Tests.Golden;
using Xunit.Abstractions;

namespace Muonroi.Pdf.Tests.Performance;

/// <summary>
/// PERF-01/PERF-02 informational dev-machine perf gate.
/// Measures cold (first render, includes JIT warmup) and warm (best-of-N steady-state) render
/// durations on the ~50 KB reference template using a Stopwatch.
///
/// GATE assertion uses a GENEROUS ceiling so this test never flakes on slow/CI hardware
/// (locked decision 2 from 07-04-PLAN.md):
///   - cold &lt;= 1500 ms
///   - warm &lt;= 400 ms
///
/// The test belongs to the non-parallel <see cref="PdfRenderCollection"/> (PdfSharpCore
/// GlobalFontSettings race) and is tagged Category=SlowIntegration so the pre-publish gate
/// filter (Category!=SlowIntegration) excludes it from blocking the release pipeline (GATE-02).
///
/// Skip via env var: set MUONROI_SKIP_PERF=1 (or =true) to skip this test entirely.
/// </summary>
[Collection(PdfRenderCollection.Name)]
[Trait("Category", "SlowIntegration")]
public sealed class PerfGateTests
{
    // GATE assertions — generous ceiling so CI/slow hardware does not flake (locked decision 2).
    // Dev-machine goal (PERF-01/PERF-02): cold <=300 ms, warm <=80 ms.
    private const int ColdCeilingMs = 1500;
    private const int WarmCeilingMs = 400;

    private const int WarmIterations = 5;

    private const string ResourceName =
        "Muonroi.Pdf.Tests.TestResources.Perf.reference-50kb.html";

    private readonly ITestOutputHelper _output;

    public PerfGateTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task PerfGate_50kbTemplate_ColdAndWarmWithinCeiling()
    {
        // Skip path — no external package dependency (locked decision 2 / interfaces note).
        string? skipVar = Environment.GetEnvironmentVariable("MUONROI_SKIP_PERF");
        if (skipVar is "1" or "true")
        {
            _output.WriteLine("MUONROI_SKIP_PERF is set — skipping perf gate.");
            return;
        }

        string html = LoadReferenceTemplate();

        // ── COLD render (includes JIT / first-call overhead) ─────────────────────────
        var coldSw = Stopwatch.StartNew();
        await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
        coldSw.Stop();
        long coldMs = coldSw.ElapsedMilliseconds;

        // ── WARM renders (best-of-N after the cold path has warmed the JIT) ──────────
        long bestWarmMs = long.MaxValue;
        for (int i = 0; i < WarmIterations; i++)
        {
            var sw = Stopwatch.StartNew();
            await GoldenPdf.RenderAsync(html, new PdfRenderOptions());
            sw.Stop();
            if (sw.ElapsedMilliseconds < bestWarmMs)
                bestWarmMs = sw.ElapsedMilliseconds;
        }

        long warmMs = bestWarmMs;

        // ── Informational output ──────────────────────────────────────────────────────
        _output.WriteLine($"[PERF] Cold render : {coldMs,6} ms  (gate ceiling: {ColdCeilingMs} ms | dev-machine goal: 300 ms)");
        _output.WriteLine($"[PERF] Warm render : {warmMs,6} ms  (gate ceiling: {WarmCeilingMs} ms | dev-machine goal:  80 ms)");
        _output.WriteLine($"[PERF] Warm = best of {WarmIterations} iterations after cold render.");

        bool coldMetTightTarget = coldMs <= 300;
        bool warmMetTightTarget = warmMs <= 80;
        _output.WriteLine($"[PERF] Tight dev-machine target met: cold={coldMetTightTarget} ({coldMs}<=300), warm={warmMetTightTarget} ({warmMs}<=80)");

        // ── Gate assertions (generous ceiling — never flakes on CI) ──────────────────
        coldMs.Should().BeLessThanOrEqualTo(ColdCeilingMs,
            $"cold render {coldMs} ms must be within ceiling {ColdCeilingMs} ms (dev goal: 300 ms)");
        warmMs.Should().BeLessThanOrEqualTo(WarmCeilingMs,
            $"warm render {warmMs} ms must be within ceiling {WarmCeilingMs} ms (dev goal: 80 ms)");
    }

    private static string LoadReferenceTemplate()
    {
        Assembly asm = typeof(PerfGateTests).Assembly;
        using Stream? stream = asm.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' not found. " +
                $"Ensure tests/Muonroi.Pdf.Tests/TestResources/Perf/reference-50kb.html " +
                $"is present and the project has <EmbeddedResource Include=\"TestResources/**\" />.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
