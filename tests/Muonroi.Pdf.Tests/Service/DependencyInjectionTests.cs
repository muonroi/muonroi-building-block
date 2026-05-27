using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Extensions;

namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// SC1 / DI-02 / DI-04: proves <c>AddPdf</c> registers the full pipeline, registers no default
/// <see cref="IFontResolver"/>, and is idempotent when called twice.
/// </summary>
public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddPdf_RegistersAllPipelineServices()
    {
        IConfiguration config = PdfServiceTestHarness.ValidConfig();
        var services = new ServiceCollection();
        services.AddTestDoubles(config);
        services.AddPdf(config);

        using ServiceProvider provider = services.BuildServiceProvider();

        provider.GetService<IMPdfService>().Should().NotBeNull();
        provider.GetService<IHtmlParser>().Should().NotBeNull();
        provider.GetService<ICssCascadeEngine>().Should().NotBeNull();
        provider.GetService<IPdfCssPolicy>().Should().NotBeNull();
        provider.GetService<IImageDecoder>().Should().NotBeNull();
        provider.GetService<IResourceResolver>().Should().NotBeNull();
        provider.GetService<IPdfWriter>().Should().NotBeNull();
    }

    [Fact]
    public void AddPdf_DoesNotRegisterFontResolver()
    {
        var services = new ServiceCollection();
        services.AddPdf(PdfServiceTestHarness.ValidConfig());

        using ServiceProvider provider = services.BuildServiceProvider();

        // DI-02 / DI-04: no default IFontResolver — the caller must supply one explicitly.
        provider.GetService<IFontResolver>().Should().BeNull();
    }

    [Fact]
    public void AddPdf_CalledTwice_DoesNotDuplicate()
    {
        IConfiguration config = PdfServiceTestHarness.ValidConfig();
        var services = new ServiceCollection();
        services.AddTestDoubles(config);

        services.AddPdf(config);
        services.AddPdf(config);

        services.Count(d => d.ServiceType == typeof(IMPdfService)).Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IPdfWriter)).Should().Be(1);

        using ServiceProvider provider = services.BuildServiceProvider();
        provider.Invoking(p => p.GetRequiredService<IMPdfService>()).Should().NotThrow();
    }
}
