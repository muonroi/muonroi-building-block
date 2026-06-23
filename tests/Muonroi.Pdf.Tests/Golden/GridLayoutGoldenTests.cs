using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the grid-layout corpus group (Phase 19, GRID-07). Each case renders
/// through the real <c>AddPdf</c> container with <c>AllowModernLayout=true</c> (the modern-layout
/// opt-in) and is asserted structurally against its committed embedded baseline. The grid cases are
/// deliberately NOT in <see cref="GoldenCorpus.AllCases"/> — under the default policy a
/// <c>display:grid</c> document throws <c>PdfPolicyException</c> (<c>forbidden.display.grid</c>), so
/// they must only ever render through this flag-aware path. Reuses the existing flag-aware
/// <see cref="GoldenPdf.VerifyAsync(string, string, Muonroi.Pdf.Abstractions.PdfRenderOptions, bool, System.Threading.CancellationToken, string)"/>
/// overload (no new overload, no change to the flag-less byte-identity guard). Belongs to the
/// non-parallel <see cref="PdfRenderCollection"/>.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class GridLayoutGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.GridCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task GridLayoutCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options, allowModernLayout: true);
    }
}
