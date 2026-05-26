using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;
using SixLabors.Fonts;

namespace Muonroi.Pdf.Tests.Font;

public sealed class VietnameseDiacriticTests
{
    private const string FontFamily = "Noto Sans";

    private static (SixLaborsTextMetrics Metrics, FontCollection Collection) BuildMetrics()
    {
        byte[] fontBytes = LoadTestFontBytes();
        var collection = new FontCollection();
        collection.Add(new MemoryStream(fontBytes));
        return (new SixLaborsTextMetrics(collection), collection);
    }

    [Fact]
    public void VietnamesePrecomposed_CharWidth_Positive()
    {
        var (metrics, _) = BuildMetrics();

        float widthECircumflexAcute = metrics.GetCharWidth('ế', FontFamily, 12f, false, false);
        widthECircumflexAcute.Should().BeGreaterThan(0f, because: "U+1EBF (ế) is a glyph present in Noto Sans");

        float widthEDotBelow = metrics.GetCharWidth('ẹ', FontFamily, 12f, false, false);
        widthEDotBelow.Should().BeGreaterThan(0f, because: "U+1EB9 (ẹ) is a glyph present in Noto Sans");
    }

    [Fact]
    public void MixedLatinVietnamese_LineHeight_Positive()
    {
        var (metrics, _) = BuildMetrics();

        float lineHeight = metrics.GetLineHeight(FontFamily, 12f);
        lineHeight.Should().BeGreaterThan(0f);

        float ascender = metrics.GetAscender(FontFamily, 12f);
        ascender.Should().BeGreaterThan(0f);
    }

    [Fact]
    public void SurrogateChar_GlyphCollector_Skipped()
    {
        var (_, collection) = BuildMetrics();

        var inlineBox = new InlineBox
        {
            Text = "𐀀",
            FontFamily = FontFamily,
            FontSize = 12f
        };

        var element = new PositionedElement
        {
            Source = inlineBox,
            Position = new Rect(0, 0, 100, 20),
            PageIndex = 0
        };

        var page = new PositionedPage();
        page.Elements.Add(element);

        var pageList = new PositionedPageList();
        pageList.Pages.Add(page);

        var collector = new GlyphCollector();
        IReadOnlyDictionary<string, IReadOnlySet<int>> result = collector.Collect(pageList, collection);

        bool hasNoSurrogates = !result.TryGetValue(FontFamily, out IReadOnlySet<int>? codepoints)
            || codepoints.Count == 0;
        hasNoSurrogates.Should().BeTrue(because: "surrogate chars are skipped by IsSurrogate guard");
    }

    private static byte[] LoadTestFontBytes()
    {
        using Stream? stream = typeof(VietnameseDiacriticTests).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf");
        if (stream is null)
            throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
