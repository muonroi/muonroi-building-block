# Muonroi.Messaging.Abstractions

## Description
Provides core abstractions for asynchronous message passing, event publishing, and distributed messaging.

## Features
- Standardized interfaces for `IMessageBus` and event handlers.
- Strongly typed message contracts.
- Independent of underlying transport mechanisms.

## Minimal Usage
```csharp
public interface IUserCreatedEvent : IEvent { }

public class UserCreatedEventHandler : IEventHandler<IUserCreatedEvent>
{
    public Task HandleAsync(IUserCreatedEvent @event, CancellationToken cancellationToken)
    {
        // Handle event
    }
}
```
