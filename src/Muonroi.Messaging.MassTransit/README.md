# Muonroi.Messaging.MassTransit

## Description
Provides a MassTransit-backed implementation of the Muonroi messaging abstractions for distributed messaging over RabbitMQ, Azure Service Bus, etc.

## Features
- Seamless integration with `Muonroi.Messaging.Abstractions`.
- Robust message retry and failure handling.
- Distributed tracing and observability support.

## Minimal Usage
```csharp
services.AddMuonroiMessaging(builder => 
{
    builder.UseMassTransit(cfg => 
    {
        cfg.UsingRabbitMq((ctx, rabbit) => rabbit.Host("localhost"));
    });
});
```
