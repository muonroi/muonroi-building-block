using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Muonroi.Pdf.Internal.Font;
using NSubstitute;

namespace Muonroi.Pdf.Tests.Font;

/// <summary>
/// Phase 11.3 — unit tests for <see cref="DefaultFontResolver"/>.
/// Uses the bundled TestFont.ttf extracted to a temp directory so the resolver can read
/// from disk (the constructor resolves paths against IHostEnvironment.ContentRootPath).
/// </summary>
public sealed class DefaultFontResolverTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _fontPath;

    public DefaultFontResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MuonroiPdfResolverTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        // Extract TestFont.ttf embedded resource to disk so DefaultFontResolver can read it.
        using Stream? stream = typeof(DefaultFontResolverTests).Assembly
            .GetManifestResourceStream("Muonroi.Pdf.Tests.TestResources.TestFont.ttf")
            ?? throw new InvalidOperationException("TestFont.ttf embedded resource not found");
        _fontPath = Path.Combine(_tempDir, "TestFont.ttf");
        using var fs = File.Create(_fontPath);
        stream.CopyTo(fs);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    // ── helper: build resolver with given config ──────────────────────────────

    private DefaultFontResolver BuildResolver(PdfFontResolverConfig resolverConfig)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.ContentRootPath.Returns(_tempDir);

        var pdfConfigs = new PdfConfigs { FontResolver = resolverConfig };
        var options = Options.Create(pdfConfigs);

        return new DefaultFontResolver(options, env, NullLogger<DefaultFontResolver>.Instance);
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultFontResolver_resolves_exact_match()
    {
        var config = new PdfFontResolverConfig
        {
            Fonts =
            [
                new PdfFontEntry { Family = "TestFamily", Path = _fontPath, Weight = 400, Style = FontStyle.Normal }
            ],
            FallbackToFirstRegistered = false
        };

        var resolver = BuildResolver(config);
        var request = new FontRequest("TestFamily", FontWeight.Normal, FontStyle.Normal);

        ReadOnlyMemory<byte>? result = await resolver.ResolveAsync(request);

        result.Should().NotBeNull("exact match should resolve");
        result!.Value.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DefaultFontResolver_falls_back_serif_to_mapped_family()
    {
        // Register "Times New Roman" as a font. The default GenericFamilyMap maps
        // "serif" → "Times New Roman", so requesting "serif" should resolve it.
        var config = new PdfFontResolverConfig
        {
            Fonts =
            [
                new PdfFontEntry { Family = "Times New Roman", Path = _fontPath, Weight = 400, Style = FontStyle.Normal }
            ],
            GenericFamilyMap = new Dictionary<string, string>
            {
                ["serif"] = "Times New Roman",
                ["sans-serif"] = "Arial",
                ["monospace"] = "Courier New"
            },
            FallbackToFirstRegistered = false
        };

        var resolver = BuildResolver(config);
        var request = new FontRequest("serif");

        ReadOnlyMemory<byte>? result = await resolver.ResolveAsync(request);

        result.Should().NotBeNull("'serif' should map to 'Times New Roman' via GenericFamilyMap");
        result!.Value.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DefaultFontResolver_returns_null_when_registry_empty_and_no_fallback()
    {
        var config = new PdfFontResolverConfig
        {
            Fonts = [],
            FallbackToFirstRegistered = false
        };

        var resolver = BuildResolver(config);
        var request = new FontRequest("Arial");

        ReadOnlyMemory<byte>? result = await resolver.ResolveAsync(request);

        result.Should().BeNull("empty registry with FallbackToFirstRegistered=false must return null");
    }

    [Fact]
    public async Task DefaultFontResolver_returns_first_registered_when_fallback_enabled()
    {
        // Register one font under "RegisteredFamily". Request "UnknownFamily" with
        // FallbackToFirstRegistered=true — should return the registered font's bytes.
        var config = new PdfFontResolverConfig
        {
            Fonts =
            [
                new PdfFontEntry { Family = "RegisteredFamily", Path = _fontPath, Weight = 400, Style = FontStyle.Normal }
            ],
            GenericFamilyMap = new Dictionary<string, string>(), // no generic mappings
            FallbackToFirstRegistered = true
        };

        var resolver = BuildResolver(config);
        var request = new FontRequest("UnknownFamily");

        ReadOnlyMemory<byte>? result = await resolver.ResolveAsync(request);

        result.Should().NotBeNull("FallbackToFirstRegistered=true must return first registered font");
        result!.Value.Length.Should().BeGreaterThan(0);
    }
}
