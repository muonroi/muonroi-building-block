using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Extensions;
using NSubstitute;

namespace Muonroi.Pdf.Tests.Service;

/// <summary>
/// SC1 / DI-02 / DI-04: proves <c>AddPdf</c> registers the full pipeline, wires
/// <c>DefaultFontResolver</c> as the default <see cref="IFontResolver"/> (Phase 11.3),
/// and is idempotent when called twice.
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

    /// <summary>
    /// Phase 11.3 / FONT-01: AddPdf registers DefaultFontResolver as the default IFontResolver
    /// via TryAdd. A pre-registered resolver wins (see AddPdf_default_resolver_overridable in
    /// PdfServiceCollectionExtensionsTests). The test name is updated from the old "DoesNotRegister"
    /// contract (before DefaultFontResolver existed) to reflect the new wiring.
    /// </summary>
    [Fact]
    public void AddPdf_RegistersDefaultFontResolver()
    {
        IConfiguration config = PdfServiceTestHarness.ValidConfig();
        var services = new ServiceCollection();
        // BindConfiguration() resolves IConfiguration from DI — register the instance.
        services.AddSingleton(config);
        // Supply IHostEnvironment so DefaultFontResolver (which needs it) can be instantiated.
        var env = Substitute.For<IHostEnvironment>();
        env.ContentRootPath.Returns(AppContext.BaseDirectory);
        services.AddSingleton(env);
        services.AddPdf(config);

        using ServiceProvider provider = services.BuildServiceProvider();

        // FONT-01: DefaultFontResolver is the out-of-the-box resolver; resolve must succeed.
        provider.GetService<IFontResolver>().Should().NotBeNull(
            "AddPdf must TryAdd DefaultFontResolver as the default IFontResolver (Phase 11.3)");
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
