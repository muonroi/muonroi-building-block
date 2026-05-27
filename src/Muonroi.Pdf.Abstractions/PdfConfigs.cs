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
    /// Resource limits enforced before and during rendering. Bindable from the
    /// <c>"PdfConfigs:Limits"</c> configuration section and validated at startup. The static
    /// <see cref="Defaults"/> instance carries the absolute backstop values the engine internals
    /// enforce regardless of any configured (possibly stricter) instance.
    /// </summary>
    public sealed class PdfLimits
    {
        /// <summary>Absolute default/backstop values enforced by engine internals.</summary>
        public static readonly PdfLimits Defaults = new();

        public long MaxHtmlBytes { get; set; } = 8_388_608;
        public int MaxDomDepth { get; set; } = 256;
        public int MaxElementCount { get; set; } = 100_000;
        public long MaxImagePixels { get; set; } = 25_000_000;
        public int MaxPages { get; set; } = 1_000;
        public long MaxRenderDurationMs { get; set; } = 15_000;
        public int MaxFontFiles { get; set; } = 32;
    }
}
