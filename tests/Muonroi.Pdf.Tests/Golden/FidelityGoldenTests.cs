using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the fidelity-layout corpus group (FIDELITY-01..03):
/// text-align center/right/justify, line-height unitless/px, text-decoration underline/strikethrough.
/// Each case renders through the real <c>AddPdf</c> container and is asserted byte-exact against its
/// committed embedded baseline. Belongs to the non-parallel <see cref="PdfRenderCollection"/>.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class FidelityGoldenTests
{
    [Theory]
    [Trait("Category", "FidelityLayout")]
    [MemberData(nameof(GoldenCorpus.FidelityLayoutCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task FidelityLayoutCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
