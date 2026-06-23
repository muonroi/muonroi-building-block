# Muonroi.Messaging.Abstractions

> Vendor-neutral message bus contracts — `IMuonroiMessageEnvelope`, domain events, saga, outbox, and routing interfaces — that decouple your domain from any broker or transport library.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Messaging.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Messaging.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships contracts only — no runtime behavior, no DI registrations, and no dependency on any message-bus library. Your domain code references these interfaces and records; the concrete transport wiring lives in the implementation package `Muonroi.Messaging.MassTransit`.

## Installation

```bash
dotnet add package Muonroi.Messaging.Abstractions --prerelease
```

## Quick Start

Implement a domain event and wire up a saga state — both depend only on this abstractions package:

```csharp
using Muonroi.Messaging.Abstractions.Contracts;
using Muonroi.Messaging.Abstractions.Events;

// 1. Define a domain event (implement IMDomainEvent via the provided base classes)
public record OrderCreatedEvent(Guid OrderId, string Product, decimal Total);

// 2. Publish with a typed envelope
var envelope = new MuonroiMessageEnvelope
{
    TenantId   = "tenant-42",
    UserId     = "usr-001",
    CorrelationId = Guid.NewGuid().ToString("N"),   // auto-generated if omitted
    SentAt     = DateTimeOffset.UtcNow              // auto-generated if omitted
};

// 3. Implement a tenant-aware saga state
public class OrderSaga : IMuonroiSaga
{
    public Guid     CorrelationId        { get; set; }
    public string?  TenantId             { get; set; }
    public DateTime CreationTime         { get; set; }
    public DateTime? LastModificationTime { get; set; }
}

// 4. Implement a message router to redirect or dead-letter messages
public class TenantRouter : IMessageRouter<OrderCreatedEvent>
{
    public int    Order => 10;
    public string Code  => "TENANT_ROUTER";

    public Task<IRoutingDecision> RouteAsync(
        OrderCreatedEvent message,
        IRoutingContext context,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(context.TenantId))
            return Task.FromResult(RoutingDecision.DeadLetter("Missing tenant"));

        return Task.FromResult(RoutingDecision.PassThrough);
    }
}
```

For end-to-end runtime behavior — broker setup, consumers, outbox relay, and telemetry — see [`Muonroi.Messaging.MassTransit`](../Muonroi.Messaging.MassTransit/) and the [Quickstart.Messaging sample](../../samples/Quickstart.Messaging/).

## Features

- **`IMuonroiMessageEnvelope` / `MuonroiMessageEnvelope`** — immutable message envelope carrying `TenantId`, `UserId`, `Username`, `CorrelationId`, `AccessToken`, and `SentAt`
- **Domain event base classes** — `MEntityCreatedEvent<T>`, `MEntityChangedEvent<T>`, `MEntityDeletedEvent<T>`, `MEntitiesCreatedEvent<T>`, `MEntitiesChangedEvent<T>`, `MEntitiesDeletedEvent<T>` implementing `IMDomainEvent`
- **Internal mediator events** — parallel `INotification`-based variants for in-process pub/sub via `Muonroi.Mediator`
- **`IMuonroiSaga`** — vendor-neutral saga state contract (tenant-scoped, `CorrelationId` primary key, timestamps); bridges to `MassTransit.ISaga` in the adapter package
- **`IMessageRouter<TMessage>` / `IRoutingContext`** — pluggable, ordered routing pipeline that returns an explicit `IRoutingDecision`
- **`RoutingDecision`** — immutable record with `PassThrough`, `RedirectTo(address)`, and `DeadLetter(reason)` factory methods
- **`IMessageRoutingRule<TMessage>`** — legacy approve/reject routing rule (extend `IRule<TMessage>` for FactBag-aware evaluation)
- **`IOutboxRelayService`** — contract for background outbox relay (`RelayPendingAsync`)
- **`EventOutbox` / `IEventOutboxStore`** — outbox entity and persistence contract with `Pending`, `Published`, `Failed` statuses
- **`[Idempotent]`** — attribute marking a consumer class as requiring inbox-based idempotent processing
- **`MessageBusTelemetryDescriptor`** — `ITelemetryDescriptor` exposing the `ActivitySource` and `Meter` names for OpenTelemetry wiring

## API Reference

| Type | Purpose |
|------|---------|
| `IMuonroiMessageEnvelope` | Message envelope contract (tenant, user, correlation, token, timestamp) |
| `MuonroiMessageEnvelope` | Sealed, init-only concrete envelope |
| `MEntityCreatedEvent<T>` | Domain event for a single entity creation |
| `MEntityChangedEvent<T>` | Domain event for a single entity update |
| `MEntityDeletedEvent<T>` | Domain event for a single entity deletion |
| `MEntitiesCreatedEvent<T>` | Domain event for bulk entity creation |
| `MEntitiesChangedEvent<T>` | Domain event for bulk entity update |
| `MEntitiesDeletedEvent<T>` | Domain event for bulk entity deletion |
| `IMuonroiSaga` | Vendor-neutral saga state (extends `ITenantScoped`) |
| `IMessageRouter<TMessage>` | Ordered routing decision for a message type |
| `IRoutingContext` | Ambient metadata provided to a router (tenant, correlation, headers) |
| `IRoutingDecision` | Routing outcome: pass-through, redirect, or dead-letter |
| `RoutingDecision` | Default immutable `IRoutingDecision` record with static factory methods |
| `IMessageRoutingRule<TMessage>` | Legacy approve/reject routing rule marker |
| `IOutboxRelayService` | Outbox background relay contract |
| `EventOutbox` | Persistent outbox record (`EventName`, `EventContent`, `Status`, `ErrorMessage`) |
| `EventOutboxStatus` | `Pending`, `Published`, `Failed` enum |
| `IEventOutboxStore` | Outbox persistence contract (`EventOutboxes`, `AddAsync`, `SaveChangesAsync`) |
| `IdempotentAttribute` | Marks a consumer class for inbox-based idempotency |
| `MessageBusTelemetryDescriptor` | Exposes `ActivitySource` and `Meter` names for OTel registration |

## Samples

- [Quickstart.Messaging](../../samples/Quickstart.Messaging/) — full messaging demo: `IMessageRouter` implementation, `MuonroiConsumerBase<T>` consumers, `AddMessageBus()` (RabbitMQ) or in-memory fallback, and outbox relay

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Messaging.MassTransit`](../Muonroi.Messaging.MassTransit/) — runtime implementation: `AddMessageBus()`, `AddOutboxRelay()`, `MuonroiConsumerBase<T>`, RabbitMQ transport, filters, and OpenTelemetry
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — `MEntity` base class used by domain event generics
- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — `ITenantScoped` extended by `IMuonroiSaga`
- [`Muonroi.Mediator`](../Muonroi.Mediator/) — `INotification` used by in-process internal event variants

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
