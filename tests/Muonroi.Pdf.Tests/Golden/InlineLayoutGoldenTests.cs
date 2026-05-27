using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the inline-layout corpus group (line wrap, baseline, vertical-align,
/// white-space). Each case is asserted byte-exact against its committed embedded baseline. Belongs to
/// the non-parallel <see cref="PdfRenderCollection"/> (PdfSharpCore FontFactory race).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class InlineLayoutGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.InlineCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task InlineLayoutCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
