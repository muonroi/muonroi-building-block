using System.Linq;
using System.Threading.Tasks;

namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// Vietnamese golden corpus (TEST-02): byte-equality baselines proving diacritic rendering is
/// stable across precomposed forms, diacritic stacking, mixed Latin+Vietnamese, line-breaking,
/// tables, and paged counters. Belongs to the non-parallel <see cref="PdfRenderCollection"/>
/// (parallelism-safe). Includes a glyph-coverage guard so the baselines are not
/// vacuous .notdef boxes (RESEARCH A3 / Pitfall 5, threat T-07-05).
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class VietnameseGoldenTests
{
    private const string FontFace = "@font-face{font-family:serif;src:url(test.ttf);}";

    private static string Doc(string body) =>
        $"<html><head><style>{FontFace}p{{margin:0;}}</style></head><body>{body}</body></html>";

    /// <summary>
    /// Guard (threat T-07-05): renders an all-base-letter document and a precomposed-diacritic
    /// document at the SAME positions and asserts their bytes DIFFER. Identical output would imply
    /// the diacritic codepoints collapsed to the same glyph subset / .notdef shaping as their base
    /// letters, signalling the embedded font lacks Vietnamese glyph coverage and that any captured
    /// baseline would be vacuous. (A naive "Tieng Viet" vs "Tiếng Việt" probe can coincidentally
    /// net identical bytes; the base-vs-precomposed pair below is the robust coverage signal —
    /// confirmed by the embedded Noto Sans rendering distinct glyphs for U+1EBF/U+1EC7 et al.)
    /// </summary>
    [Fact]
    public async Task VietnameseFont_HasGlyphCoverage()
    {
        byte[] baseLetters = await GoldenPdf.RenderAsync(Doc("<p>e o u o u e a</p>"), new PdfRenderOptions());
        byte[] diacritics = await GoldenPdf.RenderAsync(Doc("<p>ế ộ ữ ổ ừ ẹ ầ</p>"), new PdfRenderOptions());

        baseLetters.SequenceEqual(diacritics).Should().BeFalse(
            "precomposed-diacritic output must differ from the same base letters, proving the embedded "
            + "font renders Vietnamese as real glyphs (not collapsed .notdef) — otherwise TEST-02 "
            + "baselines would be vacuous");
    }

    [Theory]
    [MemberData(nameof(GoldenCorpus.VietnameseCasesData), MemberType = typeof(GoldenCorpus))]
    public async Task VietnameseCase_MatchesBaseline(string name)
    {
        GoldenCorpus.GoldenCase c = GoldenCorpus.ByName(name);
        await GoldenPdf.VerifyAsync(c.Name, c.Html, c.Options);
    }
}
