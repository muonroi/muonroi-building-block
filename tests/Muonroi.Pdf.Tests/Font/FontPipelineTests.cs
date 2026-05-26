using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Tests.Helpers;
using NSubstitute;

namespace Muonroi.Pdf.Tests.Font;

public sealed class FontPipelineTests
{
    private static readonly PdfConfigs.PdfLimits _limits = new();

    [Fact]
    public async Task MaxFontFiles_ExceededBeforeResolve_ThrowsPdfInputLimitException()
    {
        var fontFaces = Enumerable.Range(0, 33)
            .Select(i => new FontFaceDeclaration($"Family{i}", FontWeight.Normal, FontStyle.Normal))
            .ToList();
        var doc = new FakeStyledDocument(new FakeStyledNode("html"), fontFaces: fontFaces);
        var resolver = Substitute.For<IFontResolver>();

        var pipeline = new FontPipeline();

        var ex = await Assert.ThrowsAsync<PdfInputLimitException>(
            () => pipeline.ResolveAsync(doc, resolver, _limits, CancellationToken.None));

        ex.RuleId.Should().Be("FONT-MAX-FILES");
        await resolver.DidNotReceive().ResolveAsync(Arg.Any<FontRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NullResolverResult_FontSkipped_NoException()
    {
        var fontFaces = new List<FontFaceDeclaration>
        {
            new("SomeFamily", FontWeight.Normal, FontStyle.Normal)
        };
        var doc = new FakeStyledDocument(new FakeStyledNode("html"), fontFaces: fontFaces);
        var resolver = Substitute.For<IFontResolver>();
        resolver.ResolveAsync(Arg.Any<FontRequest>(), Arg.Any<CancellationToken>())
            .Returns((ReadOnlyMemory<byte>?)null);

        var pipeline = new FontPipeline();
        var (metrics, _, _) = await pipeline.ResolveAsync(doc, resolver, _limits, CancellationToken.None);

        float width = metrics.GetCharWidth('A', "SomeFamily", 12f, false, false);
        width.Should().BeGreaterThan(0f, because: "fallback returns fontSize * 0.6f = 7.2f");
    }

    [Fact]
    public async Task ValidTtfBytes_FontCollectionBuilt_MetricsNotEstimated()
    {
        byte[] fontBytes = LoadTestFontBytes();
        var fontFaces = new List<FontFaceDeclaration>
        {
            new("Noto Sans", FontWeight.Normal, FontStyle.Normal)
        };
        var doc = new FakeStyledDocument(new FakeStyledNode("html"), fontFaces: fontFaces);
        var resolver = Substitute.For<IFontResolver>();
        resolver.ResolveAsync(Arg.Any<FontRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ReadOnlyMemory<byte>(fontBytes));

        var pipeline = new FontPipeline();
        var (metrics, fontBytesMap, _) = await pipeline.ResolveAsync(doc, resolver, _limits, CancellationToken.None);

        fontBytesMap.Should().ContainKey("Noto Sans");
        float width = metrics.GetCharWidth('A', "Noto Sans", 12f, false, false);
        width.Should().BeGreaterThan(0f, because: "a real TTF font is loaded in the collection");
    }

    [Fact]
    public async Task MaxFontFiles_AtLimit_NoException()
    {
        var fontFaces = Enumerable.Range(0, 32)
            .Select(i => new FontFaceDeclaration($"Family{i}", FontWeight.Normal, FontStyle.Normal))
            .ToList();
        var doc = new FakeStyledDocument(new FakeStyledNode("html"), fontFaces: fontFaces);
        var resolver = Substitute.For<IFontResolver>();
        resolver.ResolveAsync(Arg.Any<FontRequest>(), Arg.Any<CancellationToken>())
            .Returns((ReadOnlyMemory<byte>?)null);

        var pipeline = new FontPipeline();

        Func<Task> act = () => pipeline.ResolveAsync(doc, resolver, _limits, CancellationToken.None);
        await act.Should().NotThrowAsync(because: "exactly 32 fonts is at the limit, not over");
    }

    private static byte[] LoadTestFontBytes()
    {
        using Stream? stream = typeof(FontPipelineTests).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf");
        if (stream is null)
            throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
