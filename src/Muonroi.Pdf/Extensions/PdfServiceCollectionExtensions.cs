using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;
using Muonroi.Pdf.Governance.Policies;
using Muonroi.Pdf.Internal.Image;
using Muonroi.Pdf.Internal.Security;
using Muonroi.Pdf.Internal.Service;
using Muonroi.Pdf.Internal.Telemetry;
using Muonroi.Pdf.Internal.Writer;

namespace Muonroi.Pdf.Extensions;

/// <summary>
/// Composition-root entry point for the Muonroi PDF engine (PKG-02, DI-01). A consuming host
/// calls <see cref="AddPdf"/> once to register every pipeline service, bind and validate
/// <see cref="PdfConfigs"/> at startup, and expose <see cref="PdfTelemetryDescriptor"/> to
/// OtelSetup. Lives in <c>Muonroi.Pdf.Extensions</c> (not <c>Microsoft.Extensions.DependencyInjection</c>)
/// per package convention.
/// </summary>
public static class PdfServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full HTML/CSS → PDF pipeline. Idempotent: all registrations use
    /// <c>TryAdd*</c>, so calling <see cref="AddPdf"/> twice — or a caller pre-registering an
    /// override (e.g. a custom <see cref="IFontResolver"/>) — does not duplicate or throw (SC1).
    /// <see cref="PdfConfigs"/> is bound from the <c>"PdfConfigs"</c> section and validated with
    /// <c>ValidateOnStart()</c>; a non-positive limit fails fast at host build time (SC5).
    /// </summary>
    public static IServiceCollection AddPdf(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // DI-03 / SC5 / T-06-07: bind PdfConfigs and fail fast at startup on any non-positive limit.
        services
            .AddOptions<PdfConfigs>()
            .BindConfiguration(PdfConfigs.SectionName)
            .Validate(cfg =>
                cfg.Limits.MaxHtmlBytes > 0 &&
                cfg.Limits.MaxDomDepth > 0 &&
                cfg.Limits.MaxElementCount > 0 &&
                cfg.Limits.MaxImagePixels > 0 &&
                cfg.Limits.MaxPages > 0 &&
                cfg.Limits.MaxRenderDurationMs > 0 &&
                cfg.Limits.MaxFontFiles > 0,
                "PdfConfigs: all limits must be positive integers")
            .ValidateOnStart();

        // DI-02 / DI-04 / T-06-08: default adapters. TryAdd keeps AddPdf idempotent and lets a
        // caller override any implementation by registering it before AddPdf. No default
        // IFontResolver (Decision 7): MPdfService's optional ctor param resolves to null.
        services.TryAddSingleton<IHtmlParser, AngleSharpHtmlParser>();
        services.TryAddSingleton<ICssCascadeEngine, AngleSharpCascadeEngine>();
        services.TryAddSingleton<IPdfCssPolicy, DefaultStrictPolicy>();
        services.TryAddSingleton<IImageDecoder, PureImageDecoder>();
        services.TryAddSingleton<IResourceResolver, ThrowingResourceResolver>();
        services.TryAddSingleton<IPdfWriter, PdfSharpCoreWriter>();
        services.TryAddSingleton<IMPdfService, MPdfService>();

        // TEL-01: register the descriptor as ITelemetryDescriptor so OtelSetup discovers the
        // activity source and meter. TryAddEnumerable lets it coexist with descriptors from
        // other packages in the IEnumerable<ITelemetryDescriptor> collection.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITelemetryDescriptor, PdfTelemetryDescriptor>());

        return services;
    }
}
