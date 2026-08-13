# Muonroi.Mediator

## Description
An in-process messaging and mediator implementation for CQRS and loosely coupled communication between components.

## Features
- Command and Query dispatching.
- Pipeline behaviors for cross-cutting concerns (logging, validation).
- Easy integration with dependency injection.

## Minimal Usage
```csharp
var mediator = serviceProvider.GetRequiredService<IMediator>();
var result = await mediator.Send(new GetUserQuery(userId));
```
