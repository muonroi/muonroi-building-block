using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Pdf.Internal.Telemetry;

namespace Muonroi.Pdf.Tests.Telemetry;

/// <summary>
/// TEL-01: proves <see cref="PdfTelemetryDescriptor"/> has a parameterless ctor, exposes the
/// correct activity-source / meter names for OtelSetup discovery, and implements
/// <see cref="ITelemetryDescriptor"/>.
/// </summary>
public sealed class PdfTelemetryDescriptorTests
{
    [Fact]
    public void Descriptor_HasParameterlessCtor_AndCorrectNames()
    {
        var d = new PdfTelemetryDescriptor();

        d.Should().BeAssignableTo<ITelemetryDescriptor>();
        d.ActivitySourceNames.Should().ContainSingle().Which.Should().Be("Muonroi.BuildingBlock.Pdf");
        d.MeterNames.Should().ContainSingle().Which.Should().Be("Muonroi.BuildingBlock.Pdf");
    }
}
