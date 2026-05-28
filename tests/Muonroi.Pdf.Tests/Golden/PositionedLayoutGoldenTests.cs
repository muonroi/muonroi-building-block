using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the positioned-layout corpus group: position:absolute deferred-pass
/// in a position:relative containing block. Added by Plan 06 (Wave 3b).
/// Belongs to the non-parallel <see cref="PdfRenderCollection"/> (parallelism-safe).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class PositionedLayoutGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.PositionedLayoutCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task PositionedLayoutCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
