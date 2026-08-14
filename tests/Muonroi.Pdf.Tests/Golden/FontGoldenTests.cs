namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the font corpus group: embedded subset, bold/italic variants,
/// size scaling, and @font-face resolution. Belongs to the non-parallel <see cref="PdfRenderCollection"/>
/// (parallelism-safe).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class FontGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.FontCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task FontCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
