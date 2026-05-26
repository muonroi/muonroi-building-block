namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// IConfiguration-bound options class for the PDF rendering pipeline.
/// Bind with <c>services.Configure&lt;PdfConfigs&gt;(config.GetSection(PdfConfigs.SectionName))</c>.
/// </summary>
public sealed class PdfConfigs
{
    public const string SectionName = "PdfConfigs";

    public PdfLimits Limits { get; set; } = new();

    public bool RequirePolicySignature { get; set; } = false;

    /// <summary>
    /// Compile-time hard limits enforced before and during rendering.
    /// </summary>
    public sealed class PdfLimits
    {
        public const long MaxHtmlBytes = 8_388_608;
        public const int MaxDomDepth = 256;
        public const int MaxElementCount = 100_000;
        public const long MaxImagePixels = 25_000_000;
        public const int MaxPages = 1_000;
        public const long MaxRenderDurationMs = 15_000;
        public const int MaxFontFiles = 32;
    }
}
