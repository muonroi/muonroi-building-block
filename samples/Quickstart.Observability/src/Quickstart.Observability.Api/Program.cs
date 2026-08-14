// =============================================================================
// Quickstart.Observability — Program.cs
//
// Demonstrates two approaches to wiring up OpenTelemetry in a Muonroi service:
//
//   Approach A — OtelSetup.AddObservability (full Muonroi setup)
//     Requires a Muonroi Premium license (EnsureFeatureOrThrow is called
//     internally).  Enables:
//       • Automatic ITelemetryDescriptor discovery across all loaded assemblies
//       • TenantActivityEnricher (adds tenant.id to every span)
//       • MuonroiTraceProcessor (enriches error spans with MException details)
//       • MuonroiMetrics central meter (guard violations, exceptions, retries)
//       • AspNetCore, HttpClient, Runtime instrumentation
//       • OTLP exporter when OpenTelemetry:OtlpEndpoint is set
//     Toggle:  set "Observability:UseMuonroiSetup": true in appsettings.json
//
//   Approach B — Minimal OpenTelemetry setup (default, no license needed)
//     Manually wires the same ActivitySource and Meter names that
//     ProductsTelemetryDescriptor exposes, so the sample works out-of-the-box
//     for anyone cloning the repo.
//
// Switch between them with appsettings.json:
//   "Observability": { "UseMuonroiSetup": false }   ← default: Approach B
//   "Observability": { "UseMuonroiSetup": true  }   ← Approach A (needs license)
// =============================================================================

using Muonroi.Core.Abstractions.Context;
using Muonroi.Governance.License;
using Muonroi.Logging;
using Muonroi.Observability;
using Muonroi.Observability.OpenTelemetry;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Quickstart.Observability.Api.Logging;
using Quickstart.Observability.Api.Telemetry;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ---- System execution context (required by TenantActivityEnricher / MLog) ----
// SystemExecutionContextAccessor is a lightweight AsyncLocal<> wrapper.
// In a real multi-tenant service you would populate it from JWT claims or a
// middleware; here it is registered so the dependency graph resolves cleanly.
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

// ---- Muonroi structured logging (IMLog<T>) ------------------------------------
builder.Logging.AddMuonroiLogging();

// ---- Application services -----------------------------------------------------
builder.Services.AddSingleton<ProductsLogger>();

// ---- OpenTelemetry setup — choose Approach A or B ----------------------------
bool useMuonroiSetup = builder.Configuration
    .GetValue<bool>("Observability:UseMuonroiSetup");

if (useMuonroiSetup)
{
    // ------------------------------------------------------------------
    // Approach A: OtelSetup.AddObservability (full Muonroi setup)
    //
    // Prerequisites:
    //   1. AddLicenseProtection must be called before AddObservability so
    //      that LicenseState is registered in DI (EnsureFeatureOrThrow
    //      resolves it at startup).
    //   2. A valid Muonroi Premium license must be active.
    //
    // If the license is absent or the tier is insufficient, AddObservability
    // throws LicenseException.  The catch block falls back to Approach B so
    // the sample remains runnable.
    // ------------------------------------------------------------------
    try
    {
        // AddLicenseProtection registers LicenseState.  Substitute your real
        // license key source here (environment variable, Azure Key Vault, etc.).
        builder.Services.AddLicenseProtection(builder.Configuration);

        // AddObservability reads "OpenTelemetry" config section, discovers all
        // ITelemetryDescriptor implementations in loaded assemblies, registers
        // AspNetCore / HttpClient / gRPC / Runtime instrumentation, and
        // optionally exports to OTLP.
        builder.Services.AddObservability(builder.Configuration);

        Console.WriteLine("[Observability] Using full Muonroi setup (Approach A).");
    }
    catch (LicenseException lex)
    {
        // ---------------------------------------------------------------
        // Fallback: log the licensing requirement and continue with the
        // minimal setup so the sample stays runnable without a license.
        // ---------------------------------------------------------------
        Console.WriteLine(
            $"[Observability] WARNING: Muonroi Premium license required for full setup. " +
            $"Falling back to minimal OpenTelemetry setup. Reason: {lex.Message}");

        AddMinimalOpenTelemetry(builder);
    }
}
else
{
    // ------------------------------------------------------------------
    // Approach B: Minimal OpenTelemetry setup (default, no license)
    //
    // Manually registers:
    //   • AspNetCore instrumentation (request tracing)
    //   • HttpClient instrumentation
    //   • Runtime instrumentation (GC, thread-pool metrics)
    //   • Products ActivitySource + Meter from ProductsTelemetryDescriptor
    //   • OTLP exporter when OpenTelemetry:OtlpEndpoint is non-empty
    // ------------------------------------------------------------------
    AddMinimalOpenTelemetry(builder);
    Console.WriteLine("[Observability] Using minimal OpenTelemetry setup (Approach B).");
}

// ---- MVC + Swagger -----------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Quickstart.Observability API",
        Version = "v1",
        Description =
            "Demonstrates Muonroi.Observability features:\n" +
            "• ITelemetryDescriptor auto-discovery\n" +
            "• ActivitySource custom spans with tags and child spans\n" +
            "• Counter<long> and Histogram<double> instruments\n" +
            "• IMLog<T> structured logging with BeginProperty scopes\n" +
            "• MuonroiTraceProcessor exception tagging\n\n" +
            "Endpoints:\n" +
            "  GET /api/products         — list all products (root span + metrics)\n" +
            "  GET /api/products/{id}    — single product (child span, cache-miss event)\n" +
            "  GET /api/products/error-demo — intentional error (exception span tagging)\n" +
            "  GET /health               — service name + version"
    });
});

// ---- Build and configure the pipeline ----------------------------------------
WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Quickstart.Observability v1");
    c.RoutePrefix = string.Empty; // Swagger UI at root
});

app.MapControllers();

 await app.RunAsync();

// =============================================================================
// Helper — Approach B minimal setup
// =============================================================================
static void AddMinimalOpenTelemetry(WebApplicationBuilder b)
{
    // Read the same config section that OtelSetup uses so the sample is
    // consistent regardless of which approach is active.
    Muonroi.Observability.OpenTelemetryConfigs configs = new();
    b.Configuration.GetSection(Muonroi.Observability.OpenTelemetryConfigs.SectionName).Bind(configs);

    string serviceName = configs.ServiceName ?? "Quickstart.Observability";

    b.Services.AddOpenTelemetry()
        .ConfigureResource(rb => rb.AddService(serviceName))
        .WithTracing(tracer =>
        {
            tracer
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                // Register the Products ActivitySource so spans are captured.
                .AddSource(ProductsTelemetryDescriptor.ActivitySourceName);

            if (!string.IsNullOrWhiteSpace(configs.OtlpEndpoint))
            {
                tracer.AddOtlpExporter(o => { o.Endpoint = new Uri(configs.OtlpEndpoint!); });
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                // Register the Products Meter so instruments are captured.
                .AddMeter(ProductsTelemetryDescriptor.MeterName)
                // The central Muonroi meter (guard violations, exceptions, retries).
                .AddMeter(MuonroiMetrics.Meter.Name);

            if (!string.IsNullOrWhiteSpace(configs.OtlpEndpoint))
            {
                metrics.AddOtlpExporter(o => { o.Endpoint = new Uri(configs.OtlpEndpoint!); });
            }
        });
}
