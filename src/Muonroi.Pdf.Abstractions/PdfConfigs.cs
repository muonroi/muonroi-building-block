namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// A single font file entry registered via <c>PdfConfigs:FontResolver:Fonts</c>.
/// </summary>
public sealed class PdfFontEntry
{
    /// <summary>CSS font-family name (e.g. "Arial", "Times New Roman").</summary>
    public string Family { get; init; } = "";

    /// <summary>
    /// Path to the TTF/OTF file. Relative paths are resolved against the application
    /// <c>ContentRootPath</c>; absolute paths are used as-is.
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>CSS numeric font-weight (e.g. 400, 700). Default 400.</summary>
    public int Weight { get; init; } = 400;

    /// <summary>CSS font-style. Default <see cref="FontStyle.Normal"/>.</summary>
    public FontStyle Style { get; init; } = FontStyle.Normal;
}

/// <summary>
/// Configuration for <c>DefaultFontResolver</c>, bound from
/// <c>PdfConfigs:FontResolver</c>.
/// </summary>
public sealed class PdfFontResolverConfig
{
    /// <summary>Ordered list of font registrations.</summary>
    public List<PdfFontEntry> Fonts { get; init; } = new();

    /// <summary>
    /// Maps CSS generic family names to registered family names.
    /// Looked up when an exact <see cref="PdfFontEntry.Family"/> match is not found.
    /// </summary>
    public Dictionary<string, string> GenericFamilyMap { get; init; } = new()
    {
        ["serif"] = "Times New Roman",
        ["sans-serif"] = "Arial",
        ["monospace"] = "Courier New"
    };

    /// <summary>
    /// When <see langword="true"/> (default), a request that has no family match at all
    /// returns the first registered font as a last-resort fallback (with a warning logged).
    /// Set to <see langword="false"/> to return <see langword="null"/> on no match.
    /// </summary>
    public bool FallbackToFirstRegistered { get; init; } = true;
}

/// <summary>
/// Tunables that control how <c>LegacyPrintPolicy</c> handles layout features that
/// are not supported by the engine's block-stack renderer (flex, grid).
/// Bound from <c>PdfConfigs:Policy</c>. Default values preserve the existing strict
/// (fail-loud) charter behavior.
/// </summary>
public sealed class PdfPolicySettings
{
    /// <summary>
    /// When <c>false</c> (default): <c>display:flex/grid</c> and related flex/grid CSS
    /// properties are treated as hard errors that abort rendering — fail-loud per charter.
    /// <para>
    /// When <c>true</c> (opt-in soft-degrade window): <c>display:flex/inline-flex/grid/inline-grid</c>
    /// emits a <see cref="Muonroi.Pdf.Abstractions.Policy.PolicySeverity.Warning"/> violation
    /// instead of an <see cref="Muonroi.Pdf.Abstractions.Policy.PolicySeverity.Error"/> and
    /// the engine treats the element as <c>display:block</c>. Flex/grid sub-properties
    /// (<c>flex-grow</c>, <c>justify-content</c>, <c>gap</c>, etc.) are silently dropped with
    /// at most one aggregate warning per page. Rendering is <em>not</em> aborted.
    /// </para>
    /// </summary>
    public bool SoftDegradeUnknownDisplay { get; init; } = false;
}

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
    /// Font resolver configuration. Optional — an empty <see cref="PdfFontResolverConfig.Fonts"/>
    /// list is valid; the engine falls back to its bundled Liberation fonts in that case.
    /// </summary>
    public PdfFontResolverConfig FontResolver { get; init; } = new();

    /// <summary>
    /// Policy-level tunables for the CSS policy gate.
    /// Bindable from the <c>"PdfConfigs:Policy"</c> configuration section.
    /// Default values preserve the existing strict (fail-loud) behavior.
    /// </summary>
    public PdfPolicySettings Policy { get; init; } = new();

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
