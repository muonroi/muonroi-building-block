using System;
using System.Text;
using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Security regression lock (SEC-01/02): the hardened golden must produce a <c>%PDF-1.7</c> header and
/// MUST NOT carry a <c>/JavaScript</c> token, and the corpus-floor guard asserts TEST-01's >=40 case
/// minimum at runtime. Belongs to the non-parallel <see cref="PdfRenderCollection"/>.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class SecurityGoldenTests
{
    private const string HardenedCase = "security-hardened-no-js";

    [Fact]
    public async Task HardenedGolden_MatchesBaseline()
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(HardenedCase);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }

    [Fact]
    public async Task HardenedGolden_IsPdf17_WithNoJavaScriptToken()
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(HardenedCase);
        byte[] bytes = await GoldenPdf.RenderAsync(c.Html, c.Options);

        Encoding.ASCII.GetString(bytes, 0, 8).Should().Be("%PDF-1.7",
            because: "hardened output must declare the PDF-1.7 version header (SEC-02)");

        byte[] jsToken = Encoding.ASCII.GetBytes("/JavaScript");
        IndexOf(bytes, jsToken).Should().Be(-1,
            because: "hardened output must contain no /JavaScript action token (SEC-01)");
    }

    // TEST-01 floor: runtime-enforced so the >=40 corpus requirement cannot silently regress.
    [Fact]
    public void Corpus_MeetsTest01Floor()
    {
        GoldenCorpus.AllCases.Count.Should().BeGreaterThanOrEqualTo(40,
            because: "TEST-01 requires at least 40 structural golden cases across the v0.1 subset");
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }
}
