namespace Muonroi.Pdf.Abstractions.Telemetry;

/// <summary>
/// String constants for PDF pipeline telemetry — activity source name, metric names, and tag keys.
/// </summary>
public static class PdfTelemetryNames
{
    /// <summary>Activity source name for all PDF render spans.</summary>
    public const string ActivitySourceName = "Muonroi.BuildingBlock.Pdf";
    /// <summary>Counter metric name for PDF render operations.</summary>
    public const string OperationMetric = "pdf.operation";
    /// <summary>Histogram metric name for PDF page count per render.</summary>
    public const string PageCountMetric = "pdf.page_count";
    /// <summary>Tag key for the template identifier (snake_case).</summary>
    public const string TemplateIdTag = "pdf.template_id";
    /// <summary>Tag key for the tenant identifier (snake_case).</summary>
    public const string TenantIdTag = "tenant.id";

    /// <summary>
    /// Counter metric name incremented once per page when <c>LegacyPrintPolicy</c>
    /// soft-degrade substitutes a flex or grid element as <c>display:block</c>.
    /// Tag key <c>"kind"</c> carries <c>"flex"</c> or <c>"grid"</c>.
    /// </summary>
    public const string PolicySoftDegradeMetric = "muonroi_pdf_policy_soft_degrade_total";
}
