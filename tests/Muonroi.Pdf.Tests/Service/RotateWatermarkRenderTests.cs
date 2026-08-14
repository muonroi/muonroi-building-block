namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// Phase 14 (Group C): transform:rotate() renders a block and its text as a rigid group via a
/// rotation matrix. The text Tm lives in the FlateDecode content stream, so the test inflates it and
/// asserts the rotation linear part appears (cos(45°) = 0.707107) — and that an un-rotated render
/// does not.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class RotateWatermarkRenderTests
{
    private const string FontFace =
        "<style>@font-face{font-family:serif;src:url(test.ttf);}</style>";

    private static async Task<byte[]> RenderAsync(string bodyInner)
    {
        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();
        string html = "<html><head>" + FontFace + "</head><body>" + bodyInner + "</body></html>";
        using var ms = new MemoryStream();
        await svc.RenderAsync(html, ms, new PdfRenderOptions { TemplateId = PdfServiceTestHarness.TemplateId }, default);
        return ms.ToArray();
    }

    // Inflate every FlateDecode stream and concatenate as text so content-stream operators
    // (Tm/cm) can be asserted. Font/binary streams that aren't valid text are simply included as
    // their inflated bytes (Latin1) — harmless for substring checks.
    private static string InflateStreams(byte[] pdf)
    {
        var outText = new StringBuilder();
        string ascii = Encoding.Latin1.GetString(pdf);
        int idx = 0;
        while (true)
        {
            int s = ascii.IndexOf("stream", idx, System.StringComparison.Ordinal);
            if (s < 0) break;
            int dataStart = s + "stream".Length;
            if (dataStart < pdf.Length && pdf[dataStart] == '\r') dataStart++;
            if (dataStart < pdf.Length && pdf[dataStart] == '\n') dataStart++;
            int e = ascii.IndexOf("endstream", dataStart, System.StringComparison.Ordinal);
            if (e < 0) break;
            int len = e - dataStart;
            if (len > 0)
            {
                try
                {
                    using var ms = new MemoryStream(pdf, dataStart, len);
                    using var z = new ZLibStream(ms, CompressionMode.Decompress);
                    using var dst = new MemoryStream();
                    z.CopyTo(dst);
                    outText.Append(Encoding.Latin1.GetString(dst.ToArray()));
                }
                catch (InvalidDataException)
                {
                    // Not a valid zlib stream (or trailing bytes) — skip; not a content stream we need.
                }
            }
            idx = e + "endstream".Length;
        }
        return outText.ToString();
    }

    [Fact]
    public async Task RotatedWatermark_EmitsRotationMatrix()
    {
        byte[] bytes = await RenderAsync(
            "<div style=\"transform:rotate(45deg);\">NHAP</div>");

        string content = InflateStreams(bytes);
        content.Should().Contain("0.707107",
            because: "rotate(45deg) bakes cos(45°)=0.707107 into the text Tm matrix");
    }

    [Fact]
    public async Task NonRotated_HasNoRotationMatrix()
    {
        byte[] bytes = await RenderAsync("<div>NHAP</div>");

        string content = InflateStreams(bytes);
        content.Should().NotContain("0.707107",
            because: "un-rotated text uses an identity/axis-aligned text matrix");
    }
}
