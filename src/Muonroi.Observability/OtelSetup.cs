using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Observability.OpenTelemetry.Compat;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using Muonroi.Observability.Logging;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Observability.OpenTelemetry;

namespace Muonroi.Observability;

/// <summary>
/// Centralized OpenTelemetry setup that adds tenant awareness to resources
/// and spans.
/// </summary>
public static class OtelSetup
{
    private const string GrpcActivitySourceName = "Muonroi.BuildingBlock.Grpc";
    private const string GrpcMeterName = "Muonroi.BuildingBlock.Grpc";
    private const string MessageBusActivitySourceName = "Muonroi.BuildingBlock.MessageBus";
    private const string MessageBusMeterName = "Muonroi.BuildingBlock.MessageBus";
    private const string DistributedCacheActivitySourceName = "Muonroi.BuildingBlock.DistributedCache";
    private const string DistributedCacheMeterName = "Muonroi.BuildingBlock.DistributedCache";

    /// <summary>
    /// Registers OpenTelemetry tracing and metrics for the host.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration source.</param>
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.AuditTrail);
        services.TryAddSingleton<TenantIdEnricher>();
        services.TryAddSingleton<ServiceContextEnricher>();
        OpenTelemetryConfigs configs = new();
        configuration.GetSection(OpenTelemetryConfigs.SectionName).Bind(configs);

        // Discovery pattern for telemetry descriptors (D-01)
        var descriptors = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(ITelemetryDescriptor).IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
            .Select(Activator.CreateInstance)
            .Cast<ITelemetryDescriptor>()
            .ToList();

        var activitySources = descriptors.SelectMany(d => d.ActivitySourceNames).Distinct().ToList();
        var meters = descriptors.SelectMany(d => d.MeterNames).Distinct().ToList();

        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(configs.ServiceName ?? "MuonroiService"))
            .WithTracing(tracer =>
            {
                _ = tracer
                    .AddAspNetCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource("MassTransit")
                    .AddSource("BackgroundJob")
                    .AddSource(GrpcActivitySourceName)
                    .AddSource(MessageBusActivitySourceName)
                    .AddSource(DistributedCacheActivitySourceName);

                // Add discovered sources
                foreach (var source in activitySources)
                {
                    tracer.AddSource(source);
                }

                tracer.AddProcessor(sp => new TenantActivityEnricher(sp))
                      .AddProcessor(new MuonroiTraceProcessor()); // D-02 helper

                if (!string.IsNullOrWhiteSpace(configs.OtlpEndpoint))
                {
                    _ = tracer.AddOtlpExporter(o => { o.Endpoint = new Uri(configs.OtlpEndpoint); });
                }
            })
            .WithMetrics(metrics =>
            {
                _ = metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("MassTransit")
                    .AddMeter(GrpcMeterName)
                    .AddMeter(MessageBusMeterName)
                    .AddMeter(DistributedCacheMeterName)
                    .AddMeter(MuonroiMetrics.Meter.Name); // D-03 central meter

                // Add discovered meters
                foreach (var meter in meters)
                {
                    metrics.AddMeter(meter);
                }

                if (!string.IsNullOrWhiteSpace(configs.OtlpEndpoint))
                {
                    _ = metrics.AddOtlpExporter(o => { o.Endpoint = new Uri(configs.OtlpEndpoint); });
                }
            });

        return services;
    }

    private sealed class TenantActivityEnricher(IServiceProvider sp) : BaseProcessor<Activity>
    {
        /// <summary>
        /// Adds tenant.id tag to each activity using DI-resolved context accessor.
        /// </summary>
        public override void OnStart(Activity activity)
        {
            ISystemExecutionContextAccessor? accessor = sp.GetService<ISystemExecutionContextAccessor>();
            string? tenantId = accessor?.Get()?.TenantId;
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                activity.SetTag("tenant.id", tenantId);
            }
        }
    }
}
