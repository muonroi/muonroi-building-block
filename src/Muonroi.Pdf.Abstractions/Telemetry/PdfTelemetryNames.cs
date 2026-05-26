namespace Muonroi.Pdf.Abstractions.Telemetry;

/// <summary>
/// String constants for PDF pipeline telemetry — activity source name, metric names, and tag keys.
/// </summary>
public static class PdfTelemetryNames
{
    public const string ActivitySourceName = "Muonroi.BuildingBlock.Pdf";
    public const string OperationMetric = "pdf.operation";
    public const string PageCountMetric = "pdf.page_count";
    public const string TemplateIdTag = "pdf.template_id";
    public const string TenantIdTag = "tenant.id";
}
