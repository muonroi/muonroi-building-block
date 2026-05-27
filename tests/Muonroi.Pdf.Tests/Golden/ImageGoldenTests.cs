using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the image corpus group: PNG/JPEG data-URIs, per-call resource
/// resolver, intrinsic + explicit sizing. Belongs to the non-parallel <see cref="PdfRenderCollection"/>
/// (PdfSharpCore FontFactory race).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class ImageGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.ImageCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task ImageCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
