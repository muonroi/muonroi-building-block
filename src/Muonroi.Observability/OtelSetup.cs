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
        OpenTelemetryConfigs configs = new();
        configuration.GetSection(OpenTelemetryConfigs.SectionName).Bind(configs);

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
                    .AddSource(DistributedCacheActivitySourceName)
                    .AddProcessor(sp => new TenantActivityEnricher(sp));

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
                    .AddMeter(DistributedCacheMeterName);

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
