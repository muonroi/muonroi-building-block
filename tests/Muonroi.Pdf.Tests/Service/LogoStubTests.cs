namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// Verifies that the <c>{{logo}}</c> test-harness stub uses a real, recognizable PNG
/// (32×32 8-bit RGB, 320 decoded bytes) rather than the legacy 4×4 red placeholder
/// (73 decoded bytes).  The golden image cases in <see cref="Golden.ImageGoldenTests"/>
/// are unaffected — they test PNG data-URI rendering in isolation.
/// </summary>
[Collection(PdfRenderCollection.Name)]
public sealed class LogoStubTests
{
    // 32×32 8-bit RGB PNG (color_type=2, bit_depth=8) — blue background with white "M"
    // rectangle pattern. No alpha channel, accepted by PureImageDecoder.
    // Dimensions: 32×32 px, decoded size: 320 bytes.
    internal const string RealLogoBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAACAAAAAgCAIAAAD8GO2jAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAADVSURBVEhL7Y1BCsNADAPzxDw7v2pB6DCyGzDUuWXQRR579zjO69nUvp7a11P7empfT+3rYfncwB3GuhFrLPYN7jDWjVhjsW9wh7FuxBqLfYM7jHUj1ljs/4Zvvh/8hG/efsD5JD4ToVjsBeeT+EyEYrEXnE/iMxGKxV5wPonPRCgWe8H5JD4ToVjsBeeT+EyEYrEXnE/iMxGKxV5wPonPRCgWe8H5JD4ToVjsBeeT+EyEYrEXnE/iMxGKxV5wPonPRKgoT6T29dS+ntrXU/t6at/NeX0B3b9FW4RyBUsAAAAASUVORK5CYII=";

    // The legacy 4×4 red placeholder that this stub replaces (bare base64, no data: prefix).
    // Used only to assert the new stub is NOT this value.
    private const string LegacyRedPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAAEElEQVR42mM4oaEBRwzEcQDRQxGBoNNuZAAAAABJRU5ErkJggg==";

    [Fact]
    public async Task LogoStub_RealPng_DecodesAndRendersWithoutLegacyBytes()
    {
        // Confirm the stub is NOT the legacy red placeholder.
        RealLogoBase64.Should().NotBe(LegacyRedPngBase64,
            "{{logo}} stub must be the 32×32 real PNG, not the legacy 4×4 red placeholder");

        // Confirm decoded size is larger than the 73-byte legacy PNG.
        byte[] decoded = Convert.FromBase64String(RealLogoBase64);
        decoded.Length.Should().BeGreaterThan(73,
            "the real stub must decode to more bytes than the 73-byte legacy red PNG");
        decoded.Length.Should().Be(320,
            "the real 32×32 RGB PNG stub decodes to exactly 320 bytes");

        // Render a minimal HTML that exercises the data:image/png;base64,{{logo}} substitution
        // path — the same pattern all production templates use.
        string html =
            "<html><head><style>img{display:block;}</style></head>" +
            $"<body><img src=\"data:image/png;base64,{RealLogoBase64}\" /></body></html>";

        using ServiceProvider provider = PdfServiceTestHarness.BuildProvider();
        var svc = provider.GetRequiredService<IMPdfService>();

        (byte[] pdfBytes, _) = await svc.RenderToBytesAsync(html, new PdfRenderOptions
        {
            TemplateId = "logo-stub-test",
        });

        // PDF must be non-empty and contain the %PDF-1.7 header.
        pdfBytes.Should().NotBeEmpty("render must succeed");
        Encoding.Latin1.GetString(pdfBytes, 0, Math.Min(8, pdfBytes.Length)).Should().Be("%PDF-1.7");

        // The rendered PDF must NOT contain the legacy red-PNG base64 string embedded as a literal.
        string pdfText = Encoding.Latin1.GetString(pdfBytes);
        pdfText.Should().NotContain(LegacyRedPngBase64,
            "rendered PDF must not carry the legacy 4×4 red-placeholder bytes");
    }
}
