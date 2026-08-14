namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the flex-layout corpus group (Phase 18, FLEX-07). Each case renders
/// through the real <c>AddPdf</c> container with <c>AllowModernLayout=true</c> (the modern-layout
/// opt-in) and is asserted structurally against its committed embedded baseline. The flex cases are
/// deliberately NOT in <see cref="GoldenCorpus.AllCases"/> — under the default policy a
/// <c>display:flex</c> document throws <c>PdfPolicyException</c>, so they must only ever render through
/// this flag-aware path. Belongs to the non-parallel <see cref="PdfRenderCollection"/>.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class FlexLayoutGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.FlexCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task FlexLayoutCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options, allowModernLayout: true);
    }
}
