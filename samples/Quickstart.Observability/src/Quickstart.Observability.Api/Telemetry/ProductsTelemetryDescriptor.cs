using System.Diagnostics;
using System.Diagnostics.Metrics;
using Muonroi.Core.Abstractions.Interfaces;

namespace Quickstart.Observability.Api.Telemetry;

/// <summary>
/// Telemetry descriptor for the Products domain.
///
/// OtelSetup.AddObservability performs a reflection-based discovery of every
/// non-abstract class that implements <see cref="ITelemetryDescriptor"/> across
/// all loaded assemblies (D-01 pattern).  Simply registering this class in the
/// same assembly is enough — no explicit DI registration is required.
///
/// The descriptor hands the <see cref="ActivitySource"/> and <see cref="Meter"/>
/// names to the OpenTelemetry pipeline so that spans and metrics emitted by this
/// assembly are captured automatically.
/// </summary>
public sealed class ProductsTelemetryDescriptor : ITelemetryDescriptor
{
    // -------------------------------------------------------------------------
    // Source / meter names — keep them as public constants so the controller
    // and any other class in this assembly can reference them without repeating
    // the string literal.
    // -------------------------------------------------------------------------

    /// <summary>ActivitySource name for the Products domain.</summary>
    public const string ActivitySourceName = "Quickstart.Observability.Products";

    /// <summary>Meter name for the Products domain.</summary>
    public const string MeterName = "Quickstart.Observability.Products";

    // -------------------------------------------------------------------------
    // Shared, long-lived instances.
    // ActivitySource and Meter are cheap to create but must outlive individual
    // requests — hold them as static readonly fields.
    // -------------------------------------------------------------------------

    /// <summary>
    /// The shared <see cref="ActivitySource"/> used to create product-domain spans.
    /// Use <c>Source.StartActivity("OperationName")</c> at the beginning of any
    /// operation you want to appear as a span in your tracing backend.
    /// Always null-check the returned <see cref="Activity"/> — it is null when no
    /// listener is attached (e.g. no OTLP exporter configured).
    /// </summary>
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// The shared <see cref="Meter"/> used to create product-domain instruments.
    /// Create <see cref="Counter{T}"/> and <see cref="Histogram{T}"/> from this
    /// meter; the instruments are automatically picked up by the OpenTelemetry
    /// metrics pipeline.
    /// </summary>
    public static readonly Meter ProductsMeter = new(MeterName, "1.0.0");

    // -------------------------------------------------------------------------
    // Instruments — created once, used many times.
    // -------------------------------------------------------------------------

    /// <summary>Counts every request that reaches the Products API.</summary>
    public static readonly Counter<long> RequestCount =
        ProductsMeter.CreateCounter<long>(
            "products.requests.count",
            unit: "{request}",
            description: "Total number of requests handled by the Products controller.");

    /// <summary>Records end-to-end duration of each Products API request in milliseconds.</summary>
    public static readonly Histogram<double> RequestDuration =
        ProductsMeter.CreateHistogram<double>(
            "products.request.duration",
            unit: "ms",
            description: "End-to-end duration of Products API requests in milliseconds.");

    /// <summary>Counts product lookups that resulted in a cache miss (simulated here).</summary>
    public static readonly Counter<long> CacheMissCount =
        ProductsMeter.CreateCounter<long>(
            "products.cache.misses",
            unit: "{miss}",
            description: "Number of product lookups that bypassed the cache.");

    // -------------------------------------------------------------------------
    // ITelemetryDescriptor — discovered via reflection by OtelSetup
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    public IEnumerable<string> ActivitySourceNames { get; } = [ActivitySourceName];

    /// <inheritdoc />
    public IEnumerable<string> MeterNames { get; } = [MeterName];
}
