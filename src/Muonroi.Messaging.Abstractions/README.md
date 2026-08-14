# Muonroi.Messaging.Abstractions
> Contracts, event definitions, and routing abstractions for distributed messaging in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Messaging.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Messaging.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Messaging.Abstractions` package provides the core contracts and base classes required to implement distributed, asynchronous messaging. By standardizing the concepts of Domain Events, Integration Events, Message Envelopes, and Outbox/Inbox patterns, this package ensures that microservices can communicate reliably without being tightly coupled to a specific underlying message broker (like RabbitMQ or Azure Service Bus).

This package defines *what* a message is and *how* it should be routed and tracked, while concrete implementations (like `Muonroi.Messaging.MassTransit`) handle the actual transport. 

Use this package when you are defining the shared integration contracts for your microservices, or when building custom routing and outbox logic.

## Features

- **Standardized Event Types**: Provides distinct base classes for `DomainEvent` (in-process) and `IntegrationEvent` (cross-process) to enforce architectural boundaries.
- **Message Envelopes**: `IMuonroiMessageEnvelope` ensures every message carries required metadata, such as Correlation IDs, Trace IDs, and Tenant context, enabling seamless distributed tracing.
- **Idempotency Guarantees**: Includes the `[Idempotent]` attribute and `IMessageInboxStore` contracts to prevent duplicate message processing (exactly-once semantics).
- **Outbox Pattern Contracts**: `IOutboxRelayService` and `EventOutbox` provide the blueprint for transactional outbox implementations, ensuring atomicity between database writes and message publishing.
- **Dynamic Routing**: Exposes `IMessageRouter`, `IMessageRoutingRule`, and `IDynamicRoutingTableStore` to allow complex, content-based message routing at runtime.
- **Internal Entity Events**: Pre-defined events like `MEntityCreatedEvent` and `MEntityChangedEvent` for automatic CDC (Change Data Capture) style broadcasts.

## Installation

```bash
dotnet add package Muonroi.Messaging.Abstractions
```

## Quick Start

### Defining an Integration Event

Inherit from the base `IntegrationEvent` class to define an event that will be broadcasted to other services.

```csharp
using Muonroi.Messaging.Abstractions.Events;
using System;

public class OrderPlacedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
    public string CustomerId { get; init; }
    public decimal TotalAmount { get; init; }

    public OrderPlacedIntegrationEvent(Guid orderId, string customerId, decimal totalAmount)
        : base() // Base constructor automatically sets EventId, CreationDate
    {
        OrderId = orderId;
        CustomerId = customerId;
        TotalAmount = totalAmount;
    }
}
```

### Enforcing Idempotency

When creating a handler in your consuming service, you can mark it as idempotent. The underlying infrastructure (when implemented) will use the `IMessageInboxStore` to ensure this handler is only executed once per message ID.

```csharp
using Muonroi.Messaging.Abstractions.Attributes;
using System.Threading.Tasks;

[Idempotent(StateTtlDays = 7)]
public class OrderPlacedHandler
{
    public async Task HandleAsync(OrderPlacedIntegrationEvent @event)
    {
        // This logic is guaranteed to run exactly once per event.Id
        await _inventoryService.ReserveStockAsync(@event.OrderId);
    }
}
```

## API Reference

### Event Types
- `IIntegrationEvent`: Marker interface for events crossing service boundaries.
- `IntegrationEvent`: Base class providing `Id`, `CreationDate`, and metadata headers.
- `DomainEvent`: Base class for events strictly confined to a single domain/bounded context, typically dispatched via `Muonroi.Mediator`.

### Envelopes and Routing
- `IMuonroiMessageEnvelope<T>`: Wraps a raw message with standardized headers (`TraceId`, `TenantId`, `CorrelationId`).
- `IMessageRouter`: Evaluates a message against configured rules to determine its destination(s).
- `IMessageRoutingRule`: A specific condition (e.g., "if TenantId == 'X', route to Topic Y").
- `IDynamicRoutingTableStore`: A persistent store for updating routing rules at runtime without redeploying.

### Reliability (Outbox/Inbox)
- `EventOutbox`: An entity representing a serialized event waiting to be dispatched.
- `IOutboxRelayService`: A background worker contract responsible for polling the Outbox and dispatching via the broker.
- `IMessageInboxStore`: A contract for recording processed message IDs to achieve idempotency.

### Sagas
- `IMuonroiSaga`: Base interface for long-running, stateful message orchestration (Sagas/Process Managers).

## Integration

`Muonroi.Messaging.Abstractions` provides the blueprints utilized by:
- **Muonroi.Messaging.MassTransit**: Implements the actual transport, connecting these abstractions to RabbitMQ/Azure Service Bus.
- **Muonroi.Tenancy.Abstractions**: Ensures `IntegrationEvent` instances automatically propagate tenant IDs.
- **Muonroi.Core.Abstractions**: Integrates with trace contexts to populate the `TraceId` on outgoing envelopes.

## Ecosystem Combinations

> Great standalone. Becomes **significantly more powerful** when combined.

### + Messaging.MassTransit → Transport Implementation
MassTransit implements IMessageBus, IEventPublisher contracts.

### + Data.EntityFrameworkCore → Reliable Outbox
IOutboxRepository stored via EF, IDomainEvent raised in DbContext.SaveChanges.

### + Tenancy → Distributed Context
IIntegrationEvent carries TenantId in headers automatically.

### Full Messaging Stack
```csharp
builder.Services
    .AddMessagingAbstractions(config)
    .AddMassTransit(config);
```

## Samples

See the working example in [Quickstart.Messaging.Abstractions](../../samples/Quickstart.Messaging.Abstractions).

## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
