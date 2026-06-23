# Muonroi.Mediator

> In-process mediator for the Muonroi ecosystem: command/query dispatching, streaming queries, fan-out notifications, and a composable pipeline of built-in and custom behaviors.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Mediator.svg)](https://www.nuget.org/packages/Muonroi.Mediator/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Mediator` is the Muonroi Building Block's mediator implementation. It wires commands, queries, and events through a configurable pipeline of `IPipelineBehavior<,>` stages — covering FluentValidation, role/permission authorization, multi-tenant context checks, structured diagnostics, and exception handling — all in a single `AddMMediator` call. Handlers are discovered automatically by assembly scanning, keeping controllers and endpoints free of business logic.

## Installation

```bash
dotnet add package Muonroi.Mediator --prerelease
```

## Quick Start

Register the mediator and its ecosystem pipeline in `Program.cs`:

```csharp
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging;
using Muonroi.Mediator.Mediator;
using System.Reflection;

// Structured logging
builder.Services.AddLogging(lb => lb.AddMuonroiLogging());

// Execution-context propagator (required by auth + tenant behaviors)
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();

// Mediator with full ecosystem pipeline
builder.Services.AddMMediator(options =>
{
    options.Assemblies = [Assembly.GetExecutingAssembly()];

    // Built-in pipeline: ExceptionHandler → Diagnostics → TenantValidation
    //                   → Authorization → Validation → Pre/PostProcessor
    options.AddMuonroiEcosystem();

    // Optional: add custom outer behaviors after
    options.AddBehavior(typeof(TimingBehavior<,>));
});

// FluentValidation — validators resolve automatically via ValidationBehavior
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
```

Define a command and its handler:

```csharp
// Command (returns OrderDto)
public sealed class CreateOrderCommand : IRequest<OrderDto>
{
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
}

// Handler — discovered automatically via assembly scan
public sealed class CreateOrderCommandHandler(IMediator mediator)
    : IRequestHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = new OrderDto(Guid.NewGuid(), request.ProductName, request.Quantity,
                                 request.UnitPrice, "Pending", DateTimeOffset.UtcNow);
        // Fan-out notification to all INotificationHandler<OrderCreatedNotification>
        await mediator.Publish(new OrderCreatedNotification(order.Id, order.ProductName, order.CreatedAt),
                               cancellationToken);
        return order;
    }
}
```

Dispatch from a controller or minimal-API endpoint:

```csharp
[HttpPost]
public async Task<IActionResult> CreateOrder(
    [FromBody] CreateOrderCommand command, CancellationToken cancellationToken)
{
    OrderDto order = await mediator.Send(command, cancellationToken);
    return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
}
```

## Features

- **Command/query dispatching** — `IMediator.Send<TResponse>()` routes to exactly one `IRequestHandler<TRequest, TResponse>`; unit-returning commands via `IRequest` (resolves to `IRequest<Unit>`).
- **Fan-out notifications** — `IMediator.Publish()` dispatches to all registered `INotificationHandler<TNotification>` implementations.
- **Streaming queries** — `IMediator.CreateStream<TResponse>()` returns `IAsyncEnumerable<TResponse>` backed by `IStreamRequestHandler<TRequest, TResponse>`; ASP.NET Core streams results without buffering.
- **Composable pipeline** — `IPipelineBehavior<TRequest, TResponse>` stages are registered in declared order; `AddMuonroiEcosystem()` registers the canonical set in one call.
- **FluentValidation integration** — `ValidationBehavior<,>` resolves all `IValidator<TRequest>` instances, aggregates failures, and throws `MValidationException` before the handler runs.
- **Role/permission authorization** — `MAuthorizationBehavior<,>` enforces `[MAuthorize(Roles = "...", Permissions = "...")]` against `ISystemExecutionContext.Permissions`; throws `MForbiddenException` or `MUnauthorizedException`.
- **Multi-tenant context validation** — `MTenantValidationBehavior<,>` hydrates `IRequestContextBag` from `ISystemExecutionContextAccessor` and blocks tenant-protected requests (`IMTenantRequest`) when `TenantId` is absent.
- **Structured diagnostics** — `MDiagnosticsBehavior<,>` integrates with `IMTraceContext`/`ITraceSessionStore` (no-op defaults registered automatically when `AddMuonroiDiagnostics()` is not called).
- **Exception handling** — `MExceptionHandlerBehavior<,>` catches and re-routes to `IRequestExceptionHandler<TRequest, TResponse, TException>` implementations found in scanned assemblies.
- **Pre/post processors** — `IRequestPreProcessor<TRequest>` and `IRequestPostProcessor<TRequest, TResponse>` run automatically around the handler via `MPreProcessorBehavior<,>` / `MPostProcessorBehavior<,>`.
- **Notification dispatch strategies** — `MNotificationStrategy.Sequential` (default), `ParallelWaitAll`, or `ParallelNoWait`; per-notification override via `IMStrategyNotification`.
- **Base handler class** — `MBaseCommandHandler` provides injected mapper, logger, mediator, execution-context accessors, and timestamp helpers as protected members.

## Configuration

### `MMediatorOptions`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Assemblies` | `Assembly[]` | `[]` | Assemblies scanned for handlers, processors, and exception handlers. |
| `DefaultNotificationStrategy` | `MNotificationStrategy` | `Sequential` | Dispatch strategy for notifications that do not implement `IMStrategyNotification`. |

```csharp
builder.Services.AddMMediator(options =>
{
    options.Assemblies = [Assembly.GetExecutingAssembly()];
    options.DefaultNotificationStrategy = MNotificationStrategy.ParallelWaitAll;
    options.AddMuonroiEcosystem();           // built-in pipeline
    options.AddBehavior<TimingBehavior<,>>(); // custom outer behavior
});
```

### Custom pipeline behavior

```csharp
public sealed class TimingBehavior<TRequest, TResponse>(ILogger<TimingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        TResponse result = await next();
        logger.LogInformation("{Request} completed in {Elapsed}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
        return result;
    }
}
```

Register after `AddMuonroiEcosystem()` to make it the outermost wrapper.

### Authorization attribute

```csharp
[MAuthorize(Permissions = "orders:delete")]
public sealed class DeleteOrderCommand : IRequest
{
    public Guid OrderId { get; init; }
}
```

`MAuthorizationBehavior<,>` reads `ISystemExecutionContext.Permissions` from `ISystemExecutionContextAccessor` and throws `MForbiddenException` when the constraint is not satisfied.

## API Reference

| Type | Purpose |
|------|---------|
| `IMediator` | Core contract: `Send`, `Publish`, `CreateStream` |
| `IRequest<TResponse>` | Marker for commands/queries that return a value |
| `IRequest` | Marker for unit-returning commands (`IRequest<Unit>`) |
| `IRequestHandler<TRequest, TResponse>` | Implements handling for one request type |
| `INotification` | Marker for fan-out events |
| `INotificationHandler<TNotification>` | Handles one notification type; multiple per notification allowed |
| `IStreamRequest<TResponse>` | Marker for streaming queries |
| `IStreamRequestHandler<TRequest, TResponse>` | Produces `IAsyncEnumerable<TResponse>` |
| `IPipelineBehavior<TRequest, TResponse>` | Pipeline stage; compose via `MMediatorOptions.AddBehavior()` |
| `IRequestPreProcessor<TRequest>` | Runs before the handler; auto-invoked by `MPreProcessorBehavior<,>` |
| `IRequestPostProcessor<TRequest, TResponse>` | Runs after the handler; auto-invoked by `MPostProcessorBehavior<,>` |
| `IRequestExceptionHandler<TRequest, TResponse, TException>` | Handles specific exception types thrown by a handler |
| `IMTenantRequest<TResponse>` | Tag interface; enforces non-null `TenantId` in execution context |
| `IMStrategyNotification` | Override per-notification dispatch strategy |
| `MAuthorizeAttribute` | Declares role/permission requirements on a request class |
| `MMediatorOptions` | Configuration object passed to `AddMMediator` |
| `MBaseCommandHandler` | Abstract base providing mapper, logger, mediator, and context helpers |
| `MNotificationStrategy` | Enum: `Sequential`, `ParallelWaitAll`, `ParallelNoWait` |
| `MForbiddenException` | Thrown by `MAuthorizationBehavior` on permission failure |
| `MUnauthorizedException` | Thrown when tenant or auth context is missing |
| `Unit` | Void-equivalent return value for unit-returning requests |

## Samples

- [Quickstart.Mediator](../../samples/Quickstart.Mediator/) — End-to-end ASP.NET Core API demonstrating `IRequest`, `INotification`, `IStreamRequest`, `IPipelineBehavior`, pre/post processors, and `[MAuthorize]`.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core`](../Muonroi.Core/) — Core models, guards, and execution-context infrastructure consumed by the mediator pipeline.
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — Contracts for `ISystemExecutionContextAccessor`, `ITraceSessionStore`, and exception types used by behaviors.
- [`Muonroi.Logging`](../Muonroi.Logging/) — `IMLog<T>` structured logging used by `MBaseCommandHandler` and `LoggingBehavior`.
- [`Muonroi.Mapper`](../Muonroi.Mapper/) — `IMapper` integrated into `MBaseCommandHandler`.
- [`Muonroi.Caching.Abstractions`](../Muonroi.Caching.Abstractions/) — Cache contracts available to handlers via the DI container.
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — Rule contracts consumed by `MRuleEngineBehavior<,>`.
- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — Tenancy contracts used by `MTenantValidationBehavior<,>`.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
