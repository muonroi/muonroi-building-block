# Muonroi.Messaging.MassTransit

> MassTransit adapter for the Muonroi messaging platform — wires RabbitMQ or Kafka to the Muonroi execution context, tenancy, quota, rule engine, and EF Core outbox in a single `AddMessageBus` call.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Messaging.MassTransit.svg)](https://www.nuget.org/packages/Muonroi.Messaging.MassTransit/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-COMMERCIAL)

This package implements the contracts defined in `Muonroi.Messaging.Abstractions` on top of MassTransit. It auto-configures consume, publish, and send filter pipelines that propagate tenant identity, enforce quota, apply rule-engine routing, and emit ECS-structured logs and OpenTelemetry traces — without requiring per-consumer boilerplate. Both RabbitMQ and Kafka transports are supported; the active transport is selected via configuration.

> **Commercial package** — a valid Muonroi license that includes the `MessageBus` premium feature is required. The `AddMessageBus` call will throw at startup if the license check fails.

## Installation

```bash
dotnet add package Muonroi.Messaging.MassTransit --prerelease
```

## Quick Start

Add the section to `appsettings.json`:

```json
{
  "MessageBusConfigs": {
    "BusType": "RabbitMq",
    "RabbitMq": {
      "Host": "localhost",
      "VirtualHost": "/",
      "Username": "guest",
      "Password": "guest"
    },
    "Runtime": {
      "RetryCount": 3,
      "RetryIntervalMs": 500,
      "PrefetchCount": 32,
      "ConcurrentMessageLimit": 16,
      "EnableInMemoryOutbox": true,
      "EndpointPrefix": "myapp"
    }
  }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddMessageBus(
    builder.Configuration,
    consumersAssembly: typeof(Program).Assembly);

// Optional: persistent EF Core outbox relay
builder.Services.AddOutboxRelay();
```

Implement a consumer by inheriting `MuonroiConsumerBase<TMessage>`:

```csharp
public class OrderCreatedConsumer(
    ISystemExecutionContextAccessor contextAccessor,
    IMLog<OrderCreated> log)
    : MuonroiConsumerBase<OrderCreated>(contextAccessor, log)
{
    protected override Task HandleAsync(
        ConsumeContext<OrderCreated> context,
        ISystemExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        log.Info("Order {OrderId} received for tenant {TenantId}",
            context.Message.OrderId, executionContext.TenantId);
        return Task.CompletedTask;
    }
}
```

Publish a message with full auth context propagation:

```csharp
await publishEndpoint.PublishWithAuthContext(
    new OrderCreated { OrderId = id },
    contextAccessor,
    tenantContextPolicy,
    cancellationToken);
```

## Features

- **Dual-transport**: RabbitMQ (`MassTransit.RabbitMQ`) and Kafka (`MassTransit.Kafka`) — selected by `BusType` in config; no code changes required to switch.
- **Automatic context propagation**: `AmqpContextConsumeFilter` and `MuonroiContextPublishFilter`/`MuonroiContextSendFilter` stamp tenant ID, user ID, correlation ID, and access token into every message's headers.
- **Tenant context restore on consume**: `TenantContextConsumeFilter` rebuilds `ISystemExecutionContext` from headers so `HandleAsync` always receives a fully-populated context.
- **Quota enforcement**: `TenantQuotaMessagingFilter` (publish + send) blocks messages when the tenant's messaging quota is exhausted; enabled via `EnableQuotaEnforcement: true`.
- **Rule-engine routing**: `RuleEngineRoutingFilter` evaluates the Muonroi Rule Engine per message and can redirect or drop messages; enabled via `EnableRuleEngineRouting: true`. Redis-backed dynamic routing table also available (`EnableRedisRoutingTable: true`).
- **ECS structured logging**: `EcsConsumeLoggingFilter`, `EcsPublishLoggingFilter`, `EcsSendLoggingFilter` emit ECS-compliant log entries on every message transit.
- **OpenTelemetry**: traces (`MassTransit` + `MessageBusRuntimeTelemetry` sources) and metrics (`MassTransit` + `MessageBusRuntimeTelemetry` meters) registered automatically; includes ASP.NET Core and HTTP client instrumentation.
- **Health checks**: `RabbitMqHealthCheck` or `KafkaHealthCheck` registered automatically based on the active transport.
- **Runtime policies**: retry (interval), prefetch count, concurrency limit, and MassTransit in-memory outbox applied uniformly to all endpoints via `ApplyRuntimePolicies`.
- **Persistent outbox relay**: `AddOutboxRelay` registers `OutboxRelayBackgroundService`, which polls `IEventOutboxStore` (EF Core) and publishes pending `EventOutbox` records to the bus in configurable batches.
- **Saga bridge**: `IMuonroiMassTransitSaga` combines `IMuonroiSaga` (vendor-neutral) with `MassTransit.ISaga` so saga state classes satisfy both contracts from a single implementation.
- **Message router**: `AddMessageRouter<TMessage, TRouter>` registers an `IMessageRouter<TMessage>` for rule-based routing without touching the consumer class.
- **Token masking**: `MaskAccessTokenInHeaders: true` suppresses raw access tokens from outbound headers for zero-trust deployments.

## Configuration

### `MessageBusConfigs` (`MessageBusConfigs` section)

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `BusType` | `BusType` | — | `RabbitMq` or `Kafka` |
| `RabbitMq` | `RabbitMqConfigs` | `null` | RabbitMQ connection settings |
| `Kafka` | `KafkaConfigs` | `null` | Kafka connection settings |
| `Runtime` | `MessageBusRuntimeConfigs` | see below | Retry, concurrency, outbox |
| `OutboxRelay` | `OutboxRelayConfigs` | see below | Persistent outbox relay |
| `MaskAccessTokenInHeaders` | `bool` | `false` | Suppress raw tokens in headers |
| `EnableQuotaEnforcement` | `bool` | `false` | Block messages on quota exceeded |
| `EnableRuleEngineRouting` | `bool` | `false` | Route via Rule Engine per message |
| `EnableRedisRoutingTable` | `bool` | `false` | Redis-backed dynamic routing |

### `MessageBusRuntimeConfigs`

| Property | Default | Description |
|----------|---------|-------------|
| `RetryCount` | `3` | Message retry attempts |
| `RetryIntervalMs` | `500` | Interval between retries (ms) |
| `PrefetchCount` | `32` | Per-consumer prefetch |
| `ConcurrentMessageLimit` | `16` | Max concurrent messages |
| `EnableInMemoryOutbox` | `true` | MassTransit in-memory deduplication outbox |
| `EndpointPrefix` | `""` | Kebab-case endpoint name prefix |

### `OutboxRelayConfigs`

| Property | Default | Description |
|----------|---------|-------------|
| `Enabled` | `true` | Enable the background relay |
| `PollingIntervalMs` | `5000` | How often to poll (ms) |
| `BatchSize` | `100` | Max events per poll cycle |
| `MaxRetryFailedCount` | `5` | Max failed-relay retries |

### `RabbitMqConfigs`

| Property | Default | Description |
|----------|---------|-------------|
| `Host` | `""` | RabbitMQ hostname |
| `VirtualHost` | `"/"` | AMQP virtual host |
| `Username` | `""` | Authentication username |
| `Password` | `""` | Authentication password |
| `Port` | `5672` | AMQP port |
| `UseSsl` | `false` | Enable TLS |
| `SslServerName` | `""` | TLS server name |
| `HeartbeatSeconds` | `30` | AMQP heartbeat interval |
| `PublisherConfirmation` | `true` | Require publisher confirms |
| `DeadLetterExchange` | `""` | DLX for failed messages |
| `UseQuorumQueues` | `false` | Use quorum queues |

### `KafkaConfigs`

| Property | Default | Description |
|----------|---------|-------------|
| `Host` | `""` | Kafka broker host |
| `Topic` | `""` | Default topic |
| `GroupId` | `"consumer-group"` | Consumer group ID |
| `ClientId` | `"muonroi-messagebus"` | Kafka client ID |
| `EnableAutoCommit` | `true` | Auto-commit offsets |
| `SecurityProtocol` | `""` | e.g. `SASL_SSL` |
| `SaslMechanism` | `""` | e.g. `PLAIN` |
| `SaslUsername` / `SaslPassword` | `""` | SASL credentials |
| `DeadLetterTopic` | `""` | DLT for failed messages |

## API Reference

| Type | Purpose |
|------|---------|
| `MassTransitHandler` | Static class — `AddMessageBus` and `AddOutboxRelay` extension methods |
| `MuonroiConsumerBase<TMessage>` | Abstract base consumer; override `HandleAsync` to implement business logic |
| `MessageBusConfigs` | Root configuration class; section name `"MessageBusConfigs"` |
| `MessageBusRuntimeConfigs` | Retry, concurrency, outbox, and endpoint prefix settings |
| `OutboxRelayConfigs` | Persistent outbox polling settings |
| `RabbitMqConfigs` | RabbitMQ connection and topology settings |
| `KafkaConfigs` | Kafka connection and SASL settings |
| `BusType` | Enum: `RabbitMq`, `Kafka` |
| `IBusConfigurator` | Internal transport strategy interface (`RabbitMqBusConfigurator`, `KafkaBusConfigurator`) |
| `IMuonroiMassTransitSaga` | Saga contract bridging `IMuonroiSaga` and `MassTransit.ISaga` |
| `MassTransitFilterExtensions` | `AddMessageRouter<TMessage,TRouter>`, `ApplyRuntimePolicies`, `UseMuonroiInbox` |
| `PublishEndpointAuthExtensions` | `PublishWithAuthContext<T>` and `PublishWithContext<T>` — publish with full Muonroi envelope headers |
| `OutboxRelayBackgroundService` | `BackgroundService` + `IOutboxRelayService`; polls EF Core outbox and publishes to bus |

## Samples

No dedicated sample exists for this package in the repository. The Quick Start snippet above is derived directly from the public API surface.

## Compatibility

- Target framework: `net8.0`
- License: **Commercial** — requires a Muonroi license with the `MessageBus` premium feature enabled

## Related Packages

- [`Muonroi.Messaging.Abstractions`](../Muonroi.Messaging.Abstractions/) — vendor-neutral contracts (`IMessageRouter<T>`, `IMuonroiSaga`, `IOutboxRelayService`, `IEventOutboxStore`) that this package implements
- [`Muonroi.Data.EntityFrameworkCore.Events`](../Muonroi.Data.EntityFrameworkCore.Events/) — provides `IEventOutboxStore` and `EventOutbox` for the persistent outbox relay
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — provides `ITenantContextPolicy` and tenant resolution used by the consume filter
- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — supplies the rule engine invoked by `RuleEngineRoutingFilter`
- [`Muonroi.Observability`](../Muonroi.Observability/) — shared OpenTelemetry setup extended by this package's tracing and metrics

## License

Commercial — see [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL). A license with the `Premium.MessageBus` feature is validated at startup via `EnsureFeatureOrThrow`.
