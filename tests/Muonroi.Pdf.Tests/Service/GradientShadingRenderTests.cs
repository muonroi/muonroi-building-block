using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Abstractions.Exceptions;

namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// Phase 14 (Group B): a linear-gradient background renders as a PDF axial shading. The page object
/// (uncompressed) carries the <c>/Shading</c> resource dictionary, so the emitted bytes contain the
/// ShadingType-2 + FunctionType markers. radial-gradient is still rejected by policy.
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

    [Fact]
    public async Task RadialGradient_IsRejectedByPolicy()
    {
        Func<Task> act = () => RenderAsync(
            "<div style=\"height:40px;background:radial-gradient(#fff,#000);\">band</div>");

        await act.Should().ThrowAsync<PdfPolicyException>(
            because: "radial-gradient remains unsupported");
    }
}
