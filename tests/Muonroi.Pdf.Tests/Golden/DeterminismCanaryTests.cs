using System.Linq;
using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// SC1 intra-run determinism canary: renders every registered corpus case TWICE through the same
/// render path <see cref="GoldenPdf"/> uses and asserts the two byte arrays are identical. Distinct
/// from baseline comparison — catches intra-run nondeterminism even for cases without a baseline,
/// and automatically covers every case later plans append to <see cref="GoldenCorpus.AllCases"/>.
/// Belongs to the non-parallel <see cref="PdfRenderCollection"/> (PdfSharpCore FontFactory race).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class DeterminismCanaryTests
{
    [Theory]
    [MemberData(nameof(GoldenCorpus.AllCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task CorpusCase_RendersByteDeterministically(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);

        byte[] a = await GoldenPdf.RenderAsync(c.Html, c.Options);
        byte[] b = await GoldenPdf.RenderAsync(c.Html, c.Options);

        a.SequenceEqual(b).Should().BeTrue(
            $"'{name}' must be byte-deterministic within a run (SC1 canary)");
    }
}
