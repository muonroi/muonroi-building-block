using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Pdf.Abstractions.Telemetry;

namespace Muonroi.Pdf.Internal.Telemetry;

/// <summary>
/// Telemetry discovery token for the PDF render pipeline. <c>OtelSetup</c> auto-discovers
/// every <see cref="ITelemetryDescriptor"/> via reflection and registers its activity-source and
/// meter names with the OpenTelemetry pipeline. The descriptor is hosted in the net8.0 engine
/// assembly (not the netstandard2.0 Abstractions project) because <see cref="ITelemetryDescriptor"/>
/// is defined in the net8.0 <c>Muonroi.Core.Abstractions</c> assembly.
/// </summary>
public sealed class PdfTelemetryDescriptor : ITelemetryDescriptor
{
    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames => [PdfTelemetryNames.ActivitySourceName];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames => [PdfTelemetryNames.ActivitySourceName];
}
