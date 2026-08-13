using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.Abstractions.License;
using System.Reflection;
using MassTransit;

namespace Muonroi.Messaging.MassTransit.Messaging;

/// <summary>
/// Represents the Mass Transit Handler.
/// </summary>
public static class MassTransitHandler
{
    /// <summary>
    /// Executes the Add Message Bus operation.
    /// </summary>
    public static IServiceCollection AddMessageBus(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly? consumersAssembly = null,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        services.EnsureFeatureOrThrow(FreeTierFeatures.Premium.MessageBus);
        services.TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
        services.TryAddSingleton<IContextResolver, NullContextResolver>();
        services.TryAddSingleton<ITenantContextPolicy, DefaultTenantContextPolicy>();

        MessageBusConfigs configs = new();
        configuration.GetSection(MessageBusConfigs.SectionName).Bind(configs);
        configs.Runtime ??= new MessageBusRuntimeConfigs();

        IBusConfigurator strategy = configs.BusType switch
        {
            BusType.RabbitMq => new RabbitMqBusConfigurator(),
            BusType.Kafka => new KafkaBusConfigurator(),
            _ => MGuard.Fail<IBusConfigurator>("Unsupported bus type")
        };

        _ = services.AddMassTransit(x =>
        {
            if (consumersAssembly != null)
            {
                x.AddConsumers(consumersAssembly);
            }

            if (!string.IsNullOrWhiteSpace(configs.Runtime.EndpointPrefix))
            {
                x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(configs.Runtime.EndpointPrefix, false));
            }
            else
            {
                x.SetKebabCaseEndpointNameFormatter();
            }

            x.AddConsumeFilter(typeof(AmqpContextConsumeFilter<>));
            x.AddConsumeFilter(typeof(TenantContextConsumeFilter<>));
            x.AddConsumeFilter(typeof(RuleEngineRoutingFilter<>));
            x.AddConsumeFilter(typeof(EcsConsumeLoggingFilter<>));
            
            x.AddPublishFilter(typeof(MuonroiContextPublishFilter<>));
            x.AddPublishFilter(typeof(TenantQuotaMessagingFilter<>));
            x.AddPublishFilter(typeof(EcsPublishLoggingFilter<>));
            
            x.AddSendFilter(typeof(MuonroiContextSendFilter<>));
            x.AddSendFilter(typeof(TenantQuotaMessagingFilter<>));
            x.AddSendFilter(typeof(EcsSendLoggingFilter<>));
            
            x.ApplyRuntimePolicies(configs.Runtime);

            configure?.Invoke(x);

            strategy.Configure(x, configs);
        });

        services.AddOpenTelemetry()
            .WithTracing(t =>
            {
                t.AddSource("MassTransit");
                t.AddSource(MessageBusRuntimeTelemetry.ActivitySourceName);
                t.AddAspNetCoreInstrumentation();
                t.AddHttpClientInstrumentation();
            })
            .WithMetrics(m =>
            {
                m.AddMeter("MassTransit");
                m.AddMeter(MessageBusRuntimeTelemetry.MeterName);
                m.AddRuntimeInstrumentation();
            });

        IHealthChecksBuilder checks = services.AddHealthChecks();
        switch (configs.BusType)
        {
            case BusType.RabbitMq when configs.RabbitMq != null:
                checks.AddCheck("rabbitmq", new RabbitMqHealthCheck(configs.RabbitMq));
                break;
            case BusType.Kafka when configs.Kafka != null:
                checks.AddCheck("kafka", new KafkaHealthCheck(configs.Kafka));
                break;
        }

        return services;
    }

    /// <summary>
    /// Executes the Add Outbox Relay operation.
    /// </summary>
    public static IServiceCollection AddOutboxRelay(this IServiceCollection services)
    {
        // Register as singleton first so it can be resolved by concrete type for IOutboxRelayService.
        // AddHostedService<T> only registers as IHostedService — GetRequiredService<T> would fail
        // without the explicit singleton registration below.
        services.AddSingleton<OutboxRelayBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<OutboxRelayBackgroundService>());
        services.TryAddTransient<IOutboxRelayService>(sp => sp.GetRequiredService<OutboxRelayBackgroundService>());
        return services;
    }
}
