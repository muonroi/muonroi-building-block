using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Tests.Service;
using Xunit.Sdk;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Hand-rolled byte-equality golden comparer for PDF snapshots (locked decision 1 — NO Verify.Xunit).
/// Renders HTML through the real <c>AddPdf</c> container + embedded font resolver and asserts the
/// produced bytes match a committed embedded baseline.
///
/// Regeneration is opt-in via the <c>MUONROI_UPDATE_SNAPSHOTS</c> env var: when set to <c>1</c>/<c>true</c>
/// the rendered bytes are written to the SOURCE tree (committable, NOT bin/) and the test passes.
/// When unset, the test asserts byte-equality against the embedded baseline.
/// </summary>
internal static class GoldenPdf
{
    private const string ResourcePrefix = "Muonroi.Pdf.Tests.TestResources.Golden.";

    /// <summary>True when <c>MUONROI_UPDATE_SNAPSHOTS</c> is set to <c>1</c> or <c>true</c>.</summary>
    public static bool UpdateMode
    {
        get
        {
            string? v = Environment.GetEnvironmentVariable("MUONROI_UPDATE_SNAPSHOTS");
            return string.Equals(v, "1", StringComparison.Ordinal)
                || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Renders <paramref name="html"/> to PDF bytes through the shared harness path. Reused by both
    /// the baseline comparer and the determinism canary so they exercise one render path.
    /// </summary>
    internal static async Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken ct = default)
    {
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();
        (byte[] bytes, _) = await svc.RenderToBytesAsync(html, options, ct);
        return bytes;
    }

    /// <summary>
    /// Renders the case and either regenerates (UpdateMode) or asserts byte-equality against the
    /// committed embedded baseline.
    /// </summary>
    public static async Task VerifyAsync(
        string caseName,
        string html,
        PdfRenderOptions options,
        CancellationToken ct = default,
        [CallerFilePath] string callerPath = "")
    {
        byte[] actual = await RenderAsync(html, options, ct);

        if (UpdateMode)
        {
            string sourceDir = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(callerPath)!, "..", "TestResources", "Golden"));
            Directory.CreateDirectory(sourceDir);
            File.WriteAllBytes(Path.Combine(sourceDir, caseName + ".pdf"), actual);
            return;
        }

        byte[]? expected = LoadBaseline(caseName);
        if (expected is null)
        {
            throw new XunitException(
                $"No baseline for '{caseName}'. Run with MUONROI_UPDATE_SNAPSHOTS=1 to create it.");
        }

        actual.SequenceEqual(expected).Should().BeTrue(
            $"golden '{caseName}' must match its committed baseline (actual {actual.Length} bytes, baseline {expected.Length} bytes)");
    }

    private static byte[]? LoadBaseline(string caseName)
    {
        using Stream? stream = typeof(GoldenPdf).Assembly
            .GetManifestResourceStream(ResourcePrefix + caseName + ".pdf");
        if (stream is null)
        {
            return null;
        }

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
