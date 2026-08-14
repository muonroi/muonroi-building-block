# Quickstart.Messaging.MassTransit
> Demonstrates canonical messaging publish/consume using MassTransit.

## What This Sample Demonstrates
- MassTransit InMemory transport
- `MuonroiConsumerBase<T>` for tenant-aware message consumption
- Publishing events from a controller
- `AddOutboxRelay` background service registration

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Messaging.MassTransit/src/Quickstart.Messaging.MassTransit.Api
dotnet run
```

Then open:
- API/Swagger: http://localhost:5000/swagger

## Key Files
- `Program.cs` — MassTransit registration and outbox setup
- `Consumers/OrderCreatedConsumer.cs` — Tenant-aware consumer example
- `Controllers/OrdersController.cs` — API endpoint publishing the event

## How It Works
The standard `AddMassTransit` method is used with `UsingInMemory` to provide zero-infrastructure messaging. 
The outbox relay is registered using `AddOutboxRelay()`. `MuonroiConsumerBase<T>` handles resolving context during consumption.
