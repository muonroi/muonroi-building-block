namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the HTML5 semantics corpus group (FIDELITY-04..07):
/// br line breaks, hr horizontal rules, ordered/unordered lists, and link annotations.
/// Each case renders through the real <c>AddPdf</c> container and is asserted byte-exact against its
/// committed embedded baseline. Belongs to the non-parallel <see cref="PdfRenderCollection"/>.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class Html5SemanticsGoldenTests
{
    [Theory]
    [Trait("Category", "Html5Semantics")]
    [MemberData(nameof(GoldenCorpus.Html5SemanticsCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task Html5SemanticsCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
