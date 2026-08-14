namespace Muonroi.Pdf.Internal.Telemetry;

/// <summary>
/// Process-lifetime telemetry singletons for the PDF render pipeline: the <see cref="ActivitySource"/>
/// for spans plus the <see cref="Meter"/>-backed operation counter and page-count histogram.
/// All members are <c>static readonly</c> and are never disposed — disposal would raise
/// <see cref="ObjectDisposedException"/> on every subsequent render (threat T-06-02).
/// </summary>
internal static class PdfMetrics
{
    /// <summary>Activity source consumed by MPdfService (Plan 06-03) to start render spans.</summary>
    internal static readonly ActivitySource Source = new(PdfTelemetryNames.ActivitySourceName);

    private static readonly Meter _meter = new(PdfTelemetryNames.ActivitySourceName);

    /// <summary>Counts PDF render operations.</summary>
    internal static readonly Counter<long> OperationCounter = _meter.CreateCounter<long>(
        PdfTelemetryNames.OperationMetric,
        unit: "{render}",
        description: "Counts PDF render operations.");

    /// <summary>Distribution of page count per PDF render.</summary>
    internal static readonly Histogram<int> PageCountHistogram = _meter.CreateHistogram<int>(
        PdfTelemetryNames.PageCountMetric,
        unit: "{page}",
        description: "Distribution of page count per PDF render.");

    /// <summary>
    /// Counts pages where LegacyPrintPolicy soft-degrade was triggered.
    /// Tag key <c>"kind"</c>: <c>"flex"</c> or <c>"grid"</c>.
    /// </summary>
    internal static readonly Counter<long> PolicySoftDegradeCounter = _meter.CreateCounter<long>(
        PdfTelemetryNames.PolicySoftDegradeMetric,
        unit: "{page}",
        description: "Counts pages where LegacyPrintPolicy soft-degrade substituted flex/grid as block.");
}
