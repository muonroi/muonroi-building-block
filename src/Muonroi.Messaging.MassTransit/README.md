# Muonroi.Messaging.MassTransit

> MassTransit integration implementing Muonroi messaging contracts with RabbitMQ and Kafka support, pipeline filters, and OTel instrumentation.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Messaging.MassTransit.svg)](https://www.nuget.org/packages/Muonroi.Messaging.MassTransit/)
[![License](https://img.shields.io/badge/license-Commercial-red.svg)](../../LICENSE-COMMERCIAL)

## Overview

The `Muonroi.Messaging.MassTransit` package provides a robust implementation of the `Muonroi.Messaging.Abstractions` contracts using the MassTransit framework. It abstracts away the boilerplate of configuring message brokers while injecting Muonroi's cross-cutting ecosystem concerns (such as multi-tenancy, rate limiting, and observability) directly into the messaging pipeline.

This package natively supports RabbitMQ and Kafka as underlying transports via `RabbitMqBusConfigurator` and `KafkaBusConfigurator`. It employs MassTransit's powerful middleware pipeline to inject specialized filters that handle trace context propagation, tenant context extraction, quota enforcement, and integration with the rule engine.

## Features

- **Multi-Broker Support**: Configuration wrappers (`RabbitMqBusConfigurator`, `KafkaBusConfigurator`) for rapid, environment-driven broker setup based on `BusType`.
- **Tenant Context Propagation**: `TenantContextConsumeFilter` and `MuonroiContextPublishFilter` ensure that the current Tenant ID is injected into outgoing message headers and correctly hydrated in the consuming service's context.
- **Observability**: Includes pipeline filters (`EcsConsumeLoggingFilter`, `EcsPublishLoggingFilter`) that enrich spans and logs with ECS-compliant metadata.
- **Quota Integration**: `TenantQuotaMessagingFilter` enforces message processing rate limits on a per-tenant basis.
- **Rule Engine Routing**: `RuleEngineRoutingFilter` evaluates dynamic routing rules to control message flow at runtime.
- **Idempotent Inbox**: `MuonroiInboxFilter` ensures exactly-once processing guarantees.
- **Outbox Relay**: Implements `OutboxRelayBackgroundService` to reliably poll and dispatch events from the persistent outbox to the message broker.

## Installation

```bash
dotnet add package Muonroi.Messaging.MassTransit
```

## Quick Start

### Configuring the Bus

Register MassTransit and the Muonroi pipeline filters in your host initialization.

```csharp
using Microsoft.Extensions.DependencyInjection;
using MassTransit;
using Muonroi.Messaging.MassTransit;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(Program).Assembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        // Bind Muonroi pipeline filters
        cfg.UseMuonroiFilters(context);
        
        cfg.Host(builder.Configuration["MessageBus:RabbitMq:Host"], "/", h =>
        {
            h.Username(builder.Configuration["MessageBus:RabbitMq:Username"]);
            h.Password(builder.Configuration["MessageBus:RabbitMq:Password"]);
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

### Creating a Consumer

Inherit from `MuonroiConsumerBase<T>`. Due to the injected pipeline filters, the `ITenantContext` will be automatically populated before your handler executes.

```csharp
using MassTransit;
using Muonroi.Messaging.MassTransit;
using Muonroi.Tenancy.Abstractions;

public class OrderPlacedConsumer : MuonroiConsumerBase<OrderPlacedIntegrationEvent>
{
    private readonly ITenantContextAccessor _tenantAccessor;

    public OrderPlacedConsumer(ITenantContextAccessor tenantAccessor)
    {
        _tenantAccessor = tenantAccessor;
    }

    public override async Task Consume(ConsumeContext<OrderPlacedIntegrationEvent> context)
    {
        var tenantId = _tenantAccessor.Current.TenantId;
        // Process message with tenant isolation...
    }
}
```

## Ecosystem Combinations

### + Muonroi.Tenancy.Core â†’ Automated Context Propagation
By combining these packages, `TenantContextConsumeFilter` ensures that backend worker processes instantly know which tenant's context they are running under when a message is pulled from the queue, preventing accidental cross-tenant data corruption without any custom code in your consumers.

### + Muonroi.Governance.Abstractions â†’ Tenant-Specific Throttling
Adding Governance allows `TenantQuotaMessagingFilter` to limit consumption rates based on the tenant's tier (e.g., Free vs. Enterprise), preventing noisy-neighbor issues during traffic spikes.

### + Muonroi.RuleEngine.CEP â†’ Dynamic Message Routing
Enables `RuleEngineRoutingFilter` to dynamically redirect or drop messages at runtime based on complex event processing rules defined by administrators.

### Full Reliable Messaging
```csharp
builder.Services
    .AddMassTransit(config)
    .AddTenantContext(config)
    .AddMuonroiObservability(config)
    .AddEntityFrameworkOutbox(config);
```

## Samples

- [`Quickstart.Messaging.MassTransit`](../../samples/Quickstart.Messaging.MassTransit)

## License

Commercial License â€” see [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).
