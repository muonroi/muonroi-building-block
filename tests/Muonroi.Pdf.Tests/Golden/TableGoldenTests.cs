using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Golden byte-equality tests for the table corpus group: 2x2, colspan, rowspan, combined
/// colspan+rowspan, border-collapse:separate + border-spacing, and auto vs fixed column widths.
/// Belongs to the non-parallel <see cref="PdfRenderCollection"/> (PdfSharpCore FontFactory race).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class TableGoldenTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.TableCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task TableCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
