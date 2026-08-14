# Muonroi.Mediator
> Lightweight, zero-dependency mediator pattern for command/query dispatching with built-in ecosystem behaviors.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Mediator.svg)](https://www.nuget.org/packages/Muonroi.Mediator/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Mediator` package provides a highly optimized implementation of the Mediator pattern, heavily inspired by the popular MediatR library but tailored specifically for the Muonroi ecosystem. It decouples the sending of requests (commands, queries) and notifications (events) from the logic that handles them, enabling cleaner, more maintainable architectures like CQRS.

Unlike generic mediator libraries, `Muonroi.Mediator` comes with first-class support for Muonroi's cross-cutting concerns. It includes out-of-the-box pipeline behaviors for distributed tracing, tenant context validation, role-based authorization, FluentValidation integration, rule-engine enforcement, and centralized exception handling.

Use this package as the central nervous system for your application's use cases, ensuring that all domain interactions are routed through a consistent, heavily instrumented, and secure pipeline.

## Features

- **Request/Response Dispatching**: Send commands and queries with guaranteed single-handler resolution.
- **Notification Broadcasting**: Publish events to multiple handlers simultaneously or sequentially.
- **Async Streaming**: Support for `IAsyncEnumerable` stream requests, perfect for returning large data sets or streaming gRPC responses.
- **Extensible Pipeline Behaviors**: Wrap handlers with middleware (`IPipelineBehavior`) to handle cross-cutting concerns (logging, validation, transactions).
- **First-Class Ecosystem Integrations**:
  - `MDiagnosticsBehavior`: Automatically wraps requests in trace sessions.
  - `MTenantValidationBehavior`: Ensures tenant requests are only executed in the correct tenant context.
  - `MAuthorizationBehavior`: Evaluates `[MAuthorize]` attributes securely before execution.
  - `ValidationBehavior`: Automatically triggers FluentValidation validators and aborts on failure.
  - `MRuleEngineBehavior`: Plugs into `Muonroi.RuleEngine.Abstractions` to validate domain rules prior to mutation.

## Installation

```bash
dotnet add package Muonroi.Mediator
```

## Quick Start

### Basic Configuration

Register the mediator and its behaviors in your application startup. The `AddMuonroiEcosystem` method automatically registers the recommended order of pipeline behaviors.

```csharp
using Muonroi.Mediator.Mediator;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMMediator(options =>
{
    // Scan the current assembly for IRequestHandler, INotificationHandler, etc.
    options.Assemblies = new[] { Assembly.GetExecutingAssembly() };
    
    // Register the standard Muonroi ecosystem behaviors
    options.AddMuonroiEcosystem();
});
```

### Implementing a Request and Handler

Define a request (Command or Query) and its corresponding handler.

```csharp
using Muonroi.Mediator.Mediator.Interfaces;
using System.Threading.Tasks;

// The Query Request
public class GetUserQuery : IRequest<UserDto>
{
    public Guid UserId { get; set; }
}

// The Request Handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        // Fetch and return user...
        return new UserDto { Id = request.UserId, Name = "Alice" };
    }
}
```

### Dispatching Requests

Inject the `IMediator` into your controllers or endpoints to dispatch requests.

```csharp
using Microsoft.AspNetCore.Mvc;
using Muonroi.Mediator.Mediator.Interfaces;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var result = await _mediator.Send(new GetUserQuery { UserId = id });
        return Ok(result);
    }
}
```

## Advanced Usage

### Working with Validation

By leveraging the ecosystem's `ValidationBehavior`, you can define FluentValidation rules that are automatically evaluated *before* your handler runs.

```csharp
using FluentValidation;

public class GetUserQueryValidator : AbstractValidator<GetUserQuery>
{
    public GetUserQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("User ID is required.");
    }
}
```
If validation fails, the pipeline throws a `ValidationException`, preventing the handler from executing and allowing a centralized exception filter to return a `400 Bad Request`.

### Notifications (Events)

Notifications allow you to decouple side effects. Multiple handlers can subscribe to a single notification.

```csharp
public class UserCreatedEvent : INotification
{
    public Guid UserId { get; set; }
}

public class EmailUserCreatedHandler : INotificationHandler<UserCreatedEvent>
{
    public Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Send welcome email...
        return Task.CompletedTask;
    }
}

public class AuditUserCreatedHandler : INotificationHandler<UserCreatedEvent>
{
    public Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        // Log to audit trail...
        return Task.CompletedTask;
    }
}

// Publishing the event:
await _mediator.Publish(new UserCreatedEvent { UserId = newUser.Id });
```

### Custom Pipeline Behaviors

You can implement `IPipelineBehavior<TRequest, TResponse>` to wrap handler execution with custom cross-cutting concerns like logging or transactions.

```csharp
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);
        return response;
    }
}

// Registration
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

## API Reference

### Core Interfaces
- `IRequest<TResponse>`: Marker interface to represent a request with a response.
- `IRequestHandler<TRequest, TResponse>`: Defines a handler for a request.
- `INotification`: Marker interface to represent an event.
- `INotificationHandler<TNotification>`: Defines a handler for an event.
- `IMediator`: Defines methods to `Send`, `Publish`, and `CreateStream`.
- `IPipelineBehavior<TRequest, TResponse>`: Pipeline middleware executed around a handler.

### Muonroi Extensions
- `IMTenantRequest`: Interface enforcing that a request is scoped to a specific tenant ID. Intercepted by `MTenantValidationBehavior`.
- `IMRuleRequest`: Interface enforcing that rule engine processing occurs before execution.
- `[MAuthorize(Roles="...")]`: Attribute applied to Requests to enforce claims-based authorization via `MAuthorizationBehavior`.

## Integration

`Muonroi.Mediator` is heavily integrated with the rest of the Muonroi Building Blocks:
- **Muonroi.Core.Abstractions**: Resolves diagnostics and trace sessions.
- **Muonroi.Tenancy.Abstractions**: Asserts tenant context for multitenant pipelines.
- **Muonroi.RuleEngine.Abstractions**: Executes domain logic checks before mutating state.
- **FluentValidation**: Discovers and executes validators automatically.

## Ecosystem Combinations

> Great standalone. Becomes **significantly more powerful** when combined.

### + Tenancy → Cross-Tenant Isolation
MTenantValidationBehavior: commands scoped to a tenant, cross-tenant access blocked.

### + RuleEngine.Core → Domain Validation
MRuleEngineBehavior: domain rules evaluated before handler executes.

### + Diagnostics → Tracing
MDiagnosticsBehavior: every Send() wrapped in a trace node automatically.

### + BackgroundJobs & Auth
Fire-and-forget commands via Hangfire/Quartz, with MAuthorizationBehavior enforcing claims-based authorization.

### Full CQRS stack
```csharp
builder.Services
    .AddMMediator(config)
    .AddTenantContext(config)
    .AddRuleEngine(config)
    .AddMDiagnostics(config);
```

## Samples

See the working example in [Quickstart.Mediator](../../samples/Quickstart.Mediator).

## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
