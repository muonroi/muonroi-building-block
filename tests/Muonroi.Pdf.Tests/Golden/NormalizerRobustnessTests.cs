namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Locks the structural-comparison invariant that <see cref="GoldenPdf.NormalizeStructure"/> relies on:
/// two PDFs that are identical in logical content but differ ONLY in their FlateDecode encoding (e.g.
/// rendered on different .NET runtime patch levels whose <c>ZLibStream</c> emits different — equally
/// valid — deflate bytes) must normalize to the SAME canonical form, even though their raw bytes differ.
///
/// This is the regression that makes the golden snapshots reproducible across environments: without it,
/// a deflate-output change shifts every xref offset and /Length value and the golden tests fail spuriously
/// on a different runtime even when nothing about the rendered document changed.
/// Belongs to the non-parallel <see cref="PdfRenderCollection"/>.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class NormalizerRobustnessTests
{
    [Fact]
    public async Task ReEncodingFlateStreams_ChangesRawBytes_ButNotNormalizedForm()
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName("block-single");
        byte[] original = await GoldenPdf.RenderAsync(c.Html, c.Options);

        // Produce a logically-identical PDF whose FlateDecode streams are re-encoded with a different
        // deflate level. This is exactly the kind of difference a different runtime / OS zlib build
        // introduces: same decompressed content, different compressed bytes, hence different /Length
        // and xref offsets.
        byte[] reEncoded = ReEncodeFlateStreams(original);

        reEncoded.SequenceEqual(original).Should().BeFalse(
            "the re-encoded PDF must differ at the raw-byte level (otherwise this test proves nothing)");

        byte[] normOriginal = GoldenPdf.NormalizeStructure(original);
        byte[] normReEncoded = GoldenPdf.NormalizeStructure(reEncoded);

        normReEncoded.SequenceEqual(normOriginal).Should().BeTrue(
            "PDFs differing only in FlateDecode encoding must normalize to identical canonical forms "
            + $"(normalized original {normOriginal.Length} bytes, normalized re-encoded {normReEncoded.Length} bytes)");
    }

    /// <summary>
    /// Rebuilds a PDF byte stream where every <c>/FlateDecode</c> stream is inflated then re-deflated at
    /// a non-default level, and the trailing xref/startxref offsets are recomputed. The decompressed
    /// content is byte-for-byte unchanged; only the compression encoding (and the offsets it shifts) move.
    /// </summary>
    private static byte[] ReEncodeFlateStreams(byte[] pdf)
    {
        byte[] streamKw = Encoding.ASCII.GetBytes("stream\n");
        byte[] endstreamKw = Encoding.ASCII.GetBytes("\nendstream");
        byte[] flateMarker = Encoding.ASCII.GetBytes("/FlateDecode");

        int xrefStart = LastIndexOf(pdf, Encoding.ASCII.GetBytes("\nxref\n"), pdf.Length - 1);
        int bodyEnd = xrefStart >= 0 ? xrefStart + 1 : pdf.Length;

        var body = new MemoryStream(pdf.Length);
        int pos = 0;
        while (pos < bodyEnd)
        {
            int streamAt = IndexOf(pdf, streamKw, pos, bodyEnd);
            if (streamAt < 0)
            {
                body.Write(pdf, pos, bodyEnd - pos);
                break;
            }

            int dataStart = streamAt + streamKw.Length;
            int endAt = IndexOf(pdf, endstreamKw, dataStart, bodyEnd);
            if (endAt < 0)
            {
                body.Write(pdf, pos, bodyEnd - pos);
                break;
            }

            int objAt = LastIndexOf(pdf, Encoding.ASCII.GetBytes(" obj\n"), streamAt);
            int dictStart = objAt >= 0 ? objAt : pos;
            bool isFlate = IndexOf(pdf, flateMarker, dictStart, streamAt) >= 0;
            int dataLen = endAt - dataStart;

            if (isFlate)
            {
                byte[] inflated = Inflate(pdf, dataStart, dataLen);
                byte[] reDeflated = DeflateAt(inflated, CompressionLevel.NoCompression);

                // Emit dict prefix with the compressed /Length rewritten to the new size (not /Length1).
                string prefix = Encoding.Latin1.GetString(pdf, pos, dataStart - pos);
                prefix = System.Text.RegularExpressions.Regex.Replace(
                    prefix, @"/Length\s+\d+", "/Length " + reDeflated.Length.ToString());
                byte[] prefixBytes = Encoding.Latin1.GetBytes(prefix);

                body.Write(prefixBytes, 0, prefixBytes.Length);
                body.Write(reDeflated, 0, reDeflated.Length);
                body.Write(endstreamKw, 0, endstreamKw.Length);
            }
            else
            {
                body.Write(pdf, pos, dataStart - pos);
                body.Write(pdf, dataStart, dataLen);
                body.Write(endstreamKw, 0, endstreamKw.Length);
            }

            pos = endAt + endstreamKw.Length;
        }

        // The body's object byte offsets have now shifted. Recompute a minimal valid xref + startxref so
        // the result is still a parseable PDF (the normalizer drops the table anyway, but keeping the file
        // well-formed makes the test honest about what it produces).
        byte[] bodyBytes = body.ToArray();
        return AppendRecomputedXref(bodyBytes, pdf, xrefStart);
    }

    private static byte[] AppendRecomputedXref(byte[] body, byte[] original, int originalXrefStart)
    {
        if (originalXrefStart < 0)
        {
            return body;
        }

        // Reuse the original trailer (deterministic: /Root /Info /ID /Size) but recompute object offsets.
        var offsets = new SortedDictionary<int, long>();
        byte[] objKw = Encoding.ASCII.GetBytes(" 0 obj\n");
        for (int i = 0; i + objKw.Length <= body.Length; i++)
        {
            if (!Match(body, objKw, i))
            {
                continue;
            }

            int numStart = i;
            while (numStart > 0 && body[numStart - 1] >= '0' && body[numStart - 1] <= '9')
            {
                numStart--;
            }

            if (numStart == i)
            {
                continue;
            }

            int id = int.Parse(Encoding.ASCII.GetString(body, numStart, i - numStart));
            offsets[id] = numStart;
        }

        // `body` already ends with the '\n' that originally preceded "xref" (we kept through xrefStart+1),
        // so append "xref\n" directly — prefixing another '\n' would double the newline and make the
        // re-encoded PDF differ from the original by one spurious byte after normalization.
        int maxId = offsets.Keys.Max();
        var sb = new StringBuilder();
        sb.Append("xref\n0 ").Append(maxId + 1).Append('\n');
        sb.Append("0000000000 65535 f \n");
        for (int i = 1; i <= maxId; i++)
        {
            if (offsets.TryGetValue(i, out long off))
            {
                sb.Append(off.ToString("D10")).Append(" 00000 n \n");
            }
            else
            {
                sb.Append("0000000000 65535 f \n");
            }
        }

        // Carry over the original trailer dictionary verbatim.
        int trailerAt = IndexOf(original, Encoding.ASCII.GetBytes("trailer\n"), originalXrefStart, original.Length);
        int trailerDictStart = IndexOf(original, Encoding.ASCII.GetBytes("<<"), trailerAt, original.Length);
        int trailerDictEnd = IndexOf(original, Encoding.ASCII.GetBytes(">>"), trailerDictStart, original.Length) + 2;
        string trailerDict = Encoding.Latin1.GetString(original, trailerDictStart, trailerDictEnd - trailerDictStart);

        long xrefPos = body.Length; // body ends just before the "xref" keyword we append
        sb.Append("trailer\n").Append(trailerDict).Append('\n');
        sb.Append("startxref\n").Append(xrefPos).Append("\n%%EOF\n");

        byte[] tail = Encoding.Latin1.GetBytes(sb.ToString());
        var outMs = new MemoryStream(body.Length + tail.Length);
        outMs.Write(body, 0, body.Length);
        outMs.Write(tail, 0, tail.Length);
        return outMs.ToArray();
    }

    private static byte[] Inflate(byte[] buffer, int offset, int length)
    {
        using var input = new MemoryStream(buffer, offset, length, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        using var outMs = new MemoryStream(length * 3 + 64);
        zlib.CopyTo(outMs);
        return outMs.ToArray();
    }

    private static byte[] DeflateAt(byte[] data, CompressionLevel level)
    {
        using var outMs = new MemoryStream(data.Length / 2 + 64);
        using (var zlib = new ZLibStream(outMs, level, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return outMs.ToArray();
    }

    private static bool Match(byte[] haystack, byte[] needle, int at)
    {
        for (int j = 0; j < needle.Length; j++)
        {
            if (haystack[at + j] != needle[j])
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start, int endExclusive)
    {
        int limit = endExclusive - needle.Length;
        for (int i = start; i <= limit; i++)
        {
            if (Match(haystack, needle, i))
            {
                return i;
            }
        }

        return -1;
    }

    private static int LastIndexOf(byte[] haystack, byte[] needle, int fromInclusive)
    {
        int from = Math.Min(fromInclusive, haystack.Length - needle.Length);
        for (int i = from; i >= 0; i--)
        {
            if (Match(haystack, needle, i))
            {
                return i;
            }
        }

        return -1;
    }
}
