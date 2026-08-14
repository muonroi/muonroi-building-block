namespace Muonroi.Pdf.Abstractions;

/// <summary>
/// Per-call rendering options. Independent of any singleton configuration; safe to vary per request.
/// </summary>
/// <remarks>
/// Tenant context flows ambiently via <c>ITenantContext</c> resolved from DI — do not
/// thread a tenant identifier through this options record.
/// </remarks>
public sealed record PdfRenderOptions
{
    /// <summary>Target page size. Default <see cref="PdfPageSize.A4"/>.</summary>
    public PdfPageSize PageSize { get; init; } = PdfPageSize.A4;

    /// <summary>Page orientation. Default <see cref="PdfOrientation.Portrait"/>.</summary>
    public PdfOrientation Orientation { get; init; } = PdfOrientation.Portrait;

    /// <summary>Page margins in millimetres. Default 10 mm uniform.</summary>
    public PdfMargins Margins { get; init; } = PdfMargins.Default10mm;

    /// <summary>Optional running header.</summary>
    public PdfHeaderFooter? Header { get; init; }

    /// <summary>Optional running footer.</summary>
    public PdfHeaderFooter? Footer { get; init; }

    /// <summary>Source HTML encoding. Default UTF-8.</summary>
    public string DefaultEncoding { get; init; } = "utf-8";

    /// <summary>
    /// User stylesheet appended after document stylesheets. Cascade order matches CSS spec
    /// (author origin), unlike wkhtmltopdf which placed it last regardless of specificity.
    /// </summary>
    public string? UserStyleSheet { get; init; }

    /// <summary>
    /// CSS subset policy. When null, the engine uses the policy registered via DI;
    /// in absence of a registered policy, <c>DefaultStrictPolicy</c> applies.
    /// </summary>
    public IPdfCssPolicy? Policy { get; init; }

    /// <summary>
    /// Per-call font resolver override. When null, the engine uses the resolver registered via DI.
    /// </summary>
    public IFontResolver? FontResolver { get; init; }

    /// <summary>
    /// Per-call resource resolver override. When null, the engine uses the resolver registered via DI.
    /// </summary>
    public IResourceResolver? ResourceResolver { get; init; }

    /// <summary>
    /// Correlation identifier emitted in telemetry tags. Not used for tenant scoping (use <c>ITenantContext</c>).
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Template identifier emitted in telemetry tags (hashed, no content). Optional but recommended.
    /// </summary>
    public string? TemplateId { get; init; }
}
