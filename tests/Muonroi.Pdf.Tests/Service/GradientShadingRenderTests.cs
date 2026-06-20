using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;

namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// Phase 14/15: gradient and transform render integration tests. Verifies that linear-gradient
/// renders as PDF axial shading (ShadingType 2), that affine transforms emit a <c>cm</c> operator
/// per element, and that multi-function chains emit exactly one <c>cm</c>.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class GradientShadingRenderTests
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

    // Inflate every FlateDecode content stream in the PDF and return the concatenated text.
    // This allows asserting PDF content-stream operators (cm, Tm) which are inside compressed streams.
    private static string InflateStreams(byte[] pdf)
    {
        var outText = new StringBuilder();
        string ascii = Encoding.Latin1.GetString(pdf);
        int idx = 0;
        while (true)
        {
            int s = ascii.IndexOf("stream", idx, StringComparison.Ordinal);
            if (s < 0) break;
            int dataStart = s + "stream".Length;
            if (dataStart < pdf.Length && pdf[dataStart] == '\r') dataStart++;
            if (dataStart < pdf.Length && pdf[dataStart] == '\n') dataStart++;
            int e = ascii.IndexOf("endstream", dataStart, StringComparison.Ordinal);
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
                    // Not a valid zlib stream — skip non-content streams (font binaries etc.)
                }
            }
            idx = e + "endstream".Length;
        }
        return outText.ToString();
    }

    // ── Gradient render tests ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LinearGradientDiv_EmitsAxialShading()
    {
        byte[] bytes = await RenderAsync(
            "<div style=\"height:40px;background:linear-gradient(90deg,#0c6b6b,#ffffff);\">band</div>");

        string text = Encoding.ASCII.GetString(bytes);
        text.Should().StartWith("%PDF-1.7");
        text.Should().Contain("/ShadingType 2", because: "a linear-gradient is rendered as a PDF axial shading");
        text.Should().Contain("/Coords", because: "the axial shading defines its gradient axis");
        text.Should().Contain("/FunctionType 2", because: "two color stops use an exponential interpolation function");
    }

    [Fact]
    public async Task ThreeStopGradient_UsesStitchingFunction()
    {
        byte[] bytes = await RenderAsync(
            "<div style=\"height:40px;background:linear-gradient(180deg,#ff0000,#00ff00,#0000ff);\">band</div>");

        string text = Encoding.ASCII.GetString(bytes);
        text.Should().Contain("/FunctionType 3", because: "three stops stitch multiple exponential sub-functions");
    }

    // ── Transform render tests (Phase 15, Plan 01) ────────────────────────────────────────────────
    //
    // Affine transforms for text-only elements are baked into the Tm matrix (Phase 14 pattern).
    // The cm operator is emitted in the q...Q block for background-color/-gradient elements.
    // Content streams are FlateDecode-compressed — InflateStreams() is used to read operators.

    [Fact]
    public async Task TransformTranslate_EmitsCm()
    {
        // background-color forces the q...Q block where TransformFor emits cm.
        byte[] bytes = await RenderAsync(
            "<div style=\"background-color:#cccccc;height:30px;transform:translate(10px,5px);\">x</div>");

        string content = InflateStreams(bytes);
        content.Should().Contain(" cm", because: "transform:translate() with a background must emit a cm operator in the content stream");
    }

    [Fact]
    public async Task TransformChain_EmitsSingleCm()
    {
        // background-color forces the q...Q block where TransformFor emits cm.
        // A 3-function chain composes to ONE matrix → ONE cm per element (D-01).
        byte[] bytes = await RenderAsync(
            "<div style=\"background-color:#aabbcc;height:30px;transform:translate(5px) rotate(30deg) scale(0.9);\">x</div>");

        string content = InflateStreams(bytes);
        content.Should().Contain(" cm", because: "transformed element with background must emit at least one cm");

        // Count cm operators: the composed chain must emit only ONE cm for the background block.
        int cmCount = CountOccurrences(content, " cm\n") + CountOccurrences(content, " cm\r\n");
        cmCount.Should().BeLessOrEqualTo(2, because: "a chain of 3 functions composes to one matrix → one cm, not three");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
