using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Ecosystem;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Abstractions.Engine;
using Muonroi.Pdf.Abstractions.Policy;
using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;
using Muonroi.Pdf.Governance.Policies;
using Muonroi.Pdf.Internal.Font;
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
    /// <para>
    /// Auto-wires Muonroi.Logging + <see cref="ISystemExecutionContextAccessor"/>. Pre-register
    /// before <see cref="AddPdf"/> to override (TryAdd contract).
    /// </para>
    /// </summary>
    [RequiresUnreferencedCode("Binding configuration to strongly typed options may require dynamic code.")]
    [RequiresDynamicCode("Binding configuration to strongly typed options may require dynamic code at runtime.")]
    public static IServiceCollection AddPdf(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // LOG-01: Wire Muonroi.Logging only when not already registered, because AddMuonroiLogging
        // uses AddSingleton (not TryAdd) — calling it twice would register duplicate singletons.
        // The MCapability.Logging flag, set by AddMuonroiLogging itself, is the canonical guard.
        IMEcosystemRegistry registry = services.GetOrCreateRegistry();
        if (!registry.Has(MCapability.Logging))
        {
            services.AddLogging(b => b.AddMuonroiLogging());
        }

        // CTX-01 / SC1: TryAdd keeps AddPdf idempotent; a pre-registered accessor wins.
        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

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
        // caller override any implementation by registering it before AddPdf.
        // FONT-01: DefaultFontResolver reads PdfConfigs:FontResolver; pre-register IFontResolver to override.
        services.TryAddSingleton<IFontResolver, DefaultFontResolver>();
        services.TryAddSingleton<IHtmlParser, AngleSharpHtmlParser>();
        services.TryAddSingleton<ICssCascadeEngine, AngleSharpCascadeEngine>();
        // Locked decision (Phase 08.7): LegacyPrintPolicy is the default Profile v1 gate.
        // DefaultStrictPolicy remains available for explicit opt-in (ultra-strict consumers).
        services.TryAddSingleton<IPdfCssPolicy, LegacyPrintPolicy>();
        services.TryAddSingleton<IImageDecoder, PureImageDecoder>();
        services.TryAddSingleton<IResourceResolver, ThrowingResourceResolver>();
        services.TryAddSingleton<IPdfWriter, OwnedPdfWriter>();
        services.TryAddSingleton<IMPdfService, MPdfService>();

        // TEL-01: register the descriptor as ITelemetryDescriptor so OtelSetup discovers the
        // activity source and meter. TryAddEnumerable lets it coexist with descriptors from
        // other packages in the IEnumerable<ITelemetryDescriptor> collection.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITelemetryDescriptor, PdfTelemetryDescriptor>());

        return services;
    }
}
