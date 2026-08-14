namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the block-layout corpus group. Each case renders through the
/// real <c>AddPdf</c> container and is asserted byte-exact against its committed embedded baseline.
/// Belongs to the non-parallel <see cref="PdfRenderCollection"/> (parallelism-safe).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class BlockLayoutGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.BlockCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task BlockLayoutCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }

    [Theory]
    [MemberData(nameof(GoldenCorpus.BlockLayoutFloatCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task BlockLayoutFloatCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
