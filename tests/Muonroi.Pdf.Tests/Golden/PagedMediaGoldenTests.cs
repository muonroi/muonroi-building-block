namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the paged-media corpus group: page-break-before/after/inside-avoid,
/// multi-page overflow flow, @page margins, A5/Letter/Legal page sizes, landscape orientation,
/// repeating header/footer margin boxes, and counter(page)/counter(pages). Belongs to the non-parallel
/// <see cref="PdfRenderCollection"/> (parallelism-safe).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class PagedMediaGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.PagedMediaCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task PagedMediaCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
