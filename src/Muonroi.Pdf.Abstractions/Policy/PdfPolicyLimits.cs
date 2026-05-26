namespace Muonroi.Pdf.Abstractions.Policy;

/// <summary>
/// Numerical limits enforced by an <see cref="IPdfCssPolicy"/>. Limits chosen to prevent
/// resource exhaustion attacks (XML bombs, decompression bombs, ReDoS) on untrusted templates.
/// </summary>
/// <remarks>
/// Default values match the security report defaults for <c>DefaultStrictPolicy</c>.
/// Override individual limits via C# <c>with</c> expressions (the record is immutable).
/// </remarks>
public sealed record PdfPolicyLimits
{
    /// <summary>Maximum HTML source size (bytes). Default 2 MiB.</summary>
    public long MaxHtmlBytes { get; init; } = 2L * 1024 * 1024;

    /// <summary>Maximum combined stylesheet size (bytes). Default 512 KiB.</summary>
    public long MaxStylesheetBytes { get; init; } = 512L * 1024;

    /// <summary>Maximum DOM nesting depth. Default 256.</summary>
    public int MaxDomDepth { get; init; } = 256;

    /// <summary>Maximum element count after parsing. Default 50,000.</summary>
    public int MaxElementCount { get; init; } = 50_000;

    /// <summary>Maximum attributes per element. Default 64.</summary>
    public int MaxAttributesPerElement { get; init; } = 64;

    /// <summary>Maximum attribute value length (chars). Default 8 KiB.</summary>
    public int MaxAttributeValueLength { get; init; } = 8 * 1024;

    /// <summary>Maximum selectors per stylesheet. Default 10,000.</summary>
    public int MaxSelectorsPerSheet { get; init; } = 10_000;

    /// <summary>Maximum bytes per embedded resource (data: URI or file://). Default 8 MiB.</summary>
    public long MaxEmbeddedResourceBytes { get; init; } = 8L * 1024 * 1024;

    /// <summary>Maximum image pixel count after decode. Default 25 megapixels.</summary>
    public long MaxImagePixels { get; init; } = 25_000_000;

    /// <summary>Maximum font file size (bytes). Default 4 MiB.</summary>
    public long MaxFontFileBytes { get; init; } = 4L * 1024 * 1024;

    /// <summary>Maximum output page count. Default 1,000.</summary>
    public int MaxPages { get; init; } = 1_000;

    /// <summary>Minimum computed font-size in points. Default 0.1.</summary>
    public double MinFontPt { get; init; } = 0.1;

    /// <summary>Maximum computed font-size in points. Default 1024.</summary>
    public double MaxFontPt { get; init; } = 1024.0;

    /// <summary>Render timeout (total parse + layout + write). Default 15 seconds.</summary>
    public TimeSpan RenderTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>Whether file:// scheme is allowed for <c>url()</c> values. Default false.</summary>
    public bool AllowFileScheme { get; init; }

    /// <summary>Strict-mode defaults — recommended for untrusted templates.</summary>
    public static readonly PdfPolicyLimits Strict = new();

    /// <summary>Relaxed defaults for trusted internal templates. Still bounded.</summary>
    public static readonly PdfPolicyLimits Relaxed = new()
    {
        MaxHtmlBytes = 8L * 1024 * 1024,
        MaxElementCount = 200_000,
        MaxPages = 5_000,
        RenderTimeout = TimeSpan.FromSeconds(60),
        AllowFileScheme = true
    };
}
