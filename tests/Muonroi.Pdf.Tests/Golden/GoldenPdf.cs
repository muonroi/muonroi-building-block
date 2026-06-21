using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Tests.Service;
using Xunit.Sdk;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Hand-rolled golden comparer for PDF snapshots (locked decision 1 — NO Verify.Xunit).
/// Renders HTML through the real <c>AddPdf</c> container + embedded font resolver and asserts the
/// produced output matches a committed embedded baseline.
///
/// Regeneration is opt-in via the <c>MUONROI_UPDATE_SNAPSHOTS</c> env var: when set to <c>1</c>/<c>true</c>
/// the rendered bytes are written to the SOURCE tree (committable, NOT bin/) and the test passes.
///
/// Comparison is STRUCTURAL, not raw-byte: both actual and baseline are passed through
/// <see cref="NormalizeStructure"/>, which decompresses every <c>/FlateDecode</c> stream and drops the
/// xref/startxref byte-offset table before comparing. The renderer is content-deterministic by design
/// (fixed <c>/CreationDate</c>/<c>/ModDate</c> sentinel, fixed <c>/ID</c>, fixed subset prefix, embedded
/// fonts) — see <c>OwnedPdfWriter</c>. The ONE remaining source of cross-environment variance is the
/// raw output of <c>ZLibStream</c>/<c>DeflateStream</c> at <see cref="CompressionLevel.Optimal"/>: the
/// .NET BCL does NOT guarantee identical deflate-encoded bytes across runtime patch levels / OS zlib
/// builds (managed vs zlib-ng). A one-byte difference in any compressed stream shifts every subsequent
/// xref offset and the <c>/Length</c> values, so naive byte-equality breaks on a different .NET 8 patch
/// even though the logical PDF (decompressed content, object graph, glyphs, security invariants) is
/// identical. Normalizing the compression layer keeps the snapshot lock meaningful and fully content-
/// verifying while being reproducible on any runtime. Security semantics are independently locked by
/// <c>SecurityGoldenTests.HardenedGolden_IsPdf17_WithNoJavaScriptToken</c>.
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
    /// Flag-aware render path (FLEX-07): builds the provider with
    /// <c>PdfConfigs:Policy:AllowModernLayout</c> set from <paramref name="allowModernLayout"/> so
    /// flex goldens render through the modern-layout opt-in. The flag-less overload above is left
    /// byte-for-byte unchanged — the 81-baseline regression guard depends on the default path.
    /// </summary>
    internal static async Task<byte[]> RenderAsync(
        string html, PdfRenderOptions options, bool allowModernLayout, CancellationToken ct = default)
    {
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider(
            PdfServiceTestHarness.ValidConfig(new Dictionary<string, string?>
            {
                ["PdfConfigs:Policy:AllowModernLayout"] = allowModernLayout ? "true" : "false",
            }));
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
        await VerifyCoreAsync(caseName, html, options, allowModernLayout: false, ct, callerPath);
    }

    /// <summary>
    /// Flag-aware <see cref="VerifyAsync(string, string, PdfRenderOptions, CancellationToken, string)"/>
    /// overload: renders through the modern-layout opt-in (FLEX-07). UpdateMode + structural compare
    /// are identical to the flag-less overload.
    /// </summary>
    public static async Task VerifyAsync(
        string caseName,
        string html,
        PdfRenderOptions options,
        bool allowModernLayout,
        CancellationToken ct = default,
        [CallerFilePath] string callerPath = "")
    {
        await VerifyCoreAsync(caseName, html, options, allowModernLayout, ct, callerPath);
    }

    private static async Task VerifyCoreAsync(
        string caseName,
        string html,
        PdfRenderOptions options,
        bool allowModernLayout,
        CancellationToken ct,
        string callerPath)
    {
        byte[] actual = allowModernLayout
            ? await RenderAsync(html, options, allowModernLayout: true, ct)
            : await RenderAsync(html, options, ct);

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

        byte[] actualNorm = NormalizeStructure(actual);
        byte[] expectedNorm = NormalizeStructure(expected);

        actualNorm.SequenceEqual(expectedNorm).Should().BeTrue(
            $"golden '{caseName}' must match its committed baseline structurally — decompressed content + "
            + $"object graph (actual {actual.Length} raw / {actualNorm.Length} normalized bytes, "
            + $"baseline {expected.Length} raw / {expectedNorm.Length} normalized bytes)");
    }

    /// <summary>
    /// Canonicalizes a PDF for cross-runtime comparison: decompresses every <c>/FlateDecode</c> stream
    /// in place (so the logical stream content is compared, not its runtime-dependent deflate encoding)
    /// and truncates the trailing <c>xref</c>/<c>startxref</c> byte-offset table (offsets shift whenever
    /// any compressed stream length changes, but carry no logical information the object graph does not).
    /// Everything else — object order/ids, dictionaries, the deterministic <c>/Info</c> sentinels, the
    /// fixed <c>/ID</c>, glyph content streams, image XObjects — is compared verbatim.
    /// </summary>
    internal static byte[] NormalizeStructure(byte[] pdf)
    {
        // Drop everything from the final "xref" keyword onward (xref table + trailer + startxref offset).
        // The trailer dictionary's logical fields (/Root, /Info, /ID, /Size) are deterministic and are
        // re-derived from the object graph; only their byte offsets vary, so excluding the table is safe.
        int xrefStart = LastIndexOf(pdf, Ascii("\nxref\n"), pdf.Length - 1);
        int logicalEnd = xrefStart >= 0 ? xrefStart + 1 /* keep the leading newline boundary out */ : pdf.Length;

        var output = new MemoryStream(pdf.Length);
        byte[] streamKw = Ascii("stream\n");
        byte[] endstreamKw = Ascii("\nendstream");
        byte[] flateMarker = Ascii("/FlateDecode");

        int pos = 0;
        while (pos < logicalEnd)
        {
            int streamAt = IndexOf(pdf, streamKw, pos, logicalEnd);
            if (streamAt < 0)
            {
                output.Write(pdf, pos, logicalEnd - pos);
                break;
            }

            int dataStart = streamAt + streamKw.Length;
            int endAt = IndexOf(pdf, endstreamKw, dataStart, logicalEnd);
            if (endAt < 0)
            {
                // Malformed (no terminator before logical end) — emit the remainder verbatim.
                output.Write(pdf, pos, logicalEnd - pos);
                break;
            }

            // Is this stream FlateDecode? Look back into the object's dictionary (between the previous
            // "obj" boundary and this "stream" keyword) for the /FlateDecode marker.
            int objAt = LastIndexOf(pdf, Ascii(" obj\n"), streamAt);
            int dictStart = objAt >= 0 ? objAt : pos;
            bool isFlate = IndexOf(pdf, flateMarker, dictStart, streamAt) >= 0;

            int dataLen = endAt - dataStart;
            if (isFlate)
            {
                // The stream-dict prefix carries "/Length <compressedLength>" — that length is the
                // deflate output size and therefore varies across runtimes. Canonicalize the compressed
                // /Length value (NOT /Length1, the deterministic uncompressed subset size) so it does not
                // reintroduce variance, then emit the inflated payload.
                byte[] prefix = new byte[dataStart - pos];
                Array.Copy(pdf, pos, prefix, 0, prefix.Length);
                byte[] normPrefix = NormalizeCompressedLength(prefix);
                output.Write(normPrefix, 0, normPrefix.Length);

                byte[] inflated = TryInflate(pdf, dataStart, dataLen);
                // Tag the inflated payload so a compressed-vs-uncompressed mismatch can never alias.
                output.Write(Ascii("[INFLATED]"), 0, 10);
                output.Write(inflated, 0, inflated.Length);
            }
            else
            {
                // Emit the dictionary + "stream\n" prefix and the payload verbatim.
                output.Write(pdf, pos, dataStart - pos);
                output.Write(pdf, dataStart, dataLen);
            }

            output.Write(endstreamKw, 0, endstreamKw.Length);
            pos = endAt + endstreamKw.Length;
        }

        return output.ToArray();
    }

    /// <summary>Inflates a zlib (RFC 1950) datastream; returns the raw bytes unchanged if inflation fails.</summary>
    private static byte[] TryInflate(byte[] buffer, int offset, int length)
    {
        try
        {
            using var input = new MemoryStream(buffer, offset, length, writable: false);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var outMs = new MemoryStream(length * 3 + 64);
            zlib.CopyTo(outMs);
            return outMs.ToArray();
        }
        catch (InvalidDataException)
        {
            // Not a valid zlib stream after all — fall back to the raw bytes so the comparison still runs.
            var raw = new byte[length];
            Array.Copy(buffer, offset, raw, 0, length);
            return raw;
        }
    }

    /// <summary>
    /// Rewrites the compressed-stream <c>/Length &lt;n&gt;</c> token in a stream-dict prefix to a fixed
    /// placeholder so the deflate-output size does not leak into the comparison. Leaves <c>/Length1</c>
    /// (the deterministic uncompressed subset size) untouched.
    /// </summary>
    private static byte[] NormalizeCompressedLength(byte[] prefix)
    {
        string text = Encoding.Latin1.GetString(prefix);
        // Match "/Length <digits>" where the next char after "Length" is whitespace (excludes "/Length1").
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"/Length\s+\d+", "/Length <NORM>");
        return Encoding.Latin1.GetBytes(text);
    }

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    private static int IndexOf(byte[] haystack, byte[] needle, int start, int endExclusive)
    {
        int limit = endExclusive - needle.Length;
        for (int i = start; i <= limit; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }

            if (match) return i;
        }

        return -1;
    }

    private static int LastIndexOf(byte[] haystack, byte[] needle, int fromInclusive)
    {
        int from = Math.Min(fromInclusive, haystack.Length - needle.Length);
        for (int i = from; i >= 0; i--)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }

            if (match) return i;
        }

        return -1;
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
