# Muonroi.Core.Abstractions

> Core contracts, interfaces, and base types shared by every package in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Core.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Core.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships **contracts only** — interfaces, base classes, options, and exception types. It contains no runtime service registrations. All Muonroi packages depend on it as their shared vocabulary; the runtime implementations live in [`Muonroi.Core`](../Muonroi.Core/).

## Installation

```bash
dotnet add package Muonroi.Core.Abstractions --prerelease
```

## Quick Start

Add this package when you are building a library or adapter that needs to depend on Muonroi contracts without pulling in full runtime implementations. Implement or consume the key interfaces directly:

```csharp
using Muonroi.Core.Abstractions.Context;

// Implement IContextResolver to feed tenant/user identity into the execution context.
public sealed class HttpContextResolver(IHttpContextAccessor accessor) : IContextResolver
{
    public string? ResolveTenantId() =>
        accessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();

    public string? ResolveUserId() =>
        accessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string? ResolveUsername() =>
        accessor.HttpContext?.User.Identity?.Name;
}

// Consume ISystemExecutionContext wherever you need the current tenant/user/correlation context.
public class OrderService(ISystemExecutionContextAccessor ctx)
{
    public void PlaceOrder()
    {
        ISystemExecutionContext context = ctx.Get();
        string tenantId = context.TenantId ?? throw new InvalidOperationException("No tenant");
        string correlationId = context.CorrelationId;
        // ...
    }
}
```

For the full runtime wiring (DI registration, middleware, `AsyncLocal` accessor), see [`Muonroi.Core`](../Muonroi.Core/).

## Features

- **Execution context contracts** — `ISystemExecutionContext` / `ISystemExecutionContextAccessor` carry tenant ID, user ID, username, correlation ID, access token, API key, permissions, and source type across async call stacks via `AsyncLocal<T>`.
- **Context resolution** — `IContextResolver` interface for plugging in custom tenant/user resolvers; `NullContextResolver` provided as a default no-op.
- **Tenant policy** — `ITenantContextPolicy` enforces tenant context presence; `DefaultTenantContextPolicy` reads from `IContextResolver` and throws `MissingTenantContextException` / `MissingUserContextException` when required context is absent.
- **Structured responses** — `MResponse<T>` wraps results and errors uniformly across all API boundaries; `MVoidMethodResult` for command endpoints; `MErrorResult` / `MErrorResponse` for consistent error payloads.
- **Domain entity base** — `MEntity` provides `Id`, `EntityId` (Guid), soft-delete flags, audit timestamps, and a domain-event collection (`AddDomainEvent` / `ClearDomainEvents`).
- **Domain events** — `IMDomainEvent` / `INotification` marker interfaces for MediatR-style event dispatch.
- **Typed exceptions** — `MNotFoundException`, `MConflictException`, `MUnauthorizedException`, `MArgumentException`, `MInternalException` (captures `[CallerMemberName]` / `[CallerFilePath]`) all derive from a common `MException` base.
- **Diagnostics contracts** — `IMTraceContext` / `ITraceSession` / `ITraceSessionStore` for structured, session-based distributed tracing; `[MTraceable]` / `[MTraceSensitive]` attributes for opt-in instrumentation.
- **Ecosystem registry** — `IMEcosystemRegistry` tracks which `MCapability` flags are active; each package self-registers during DI setup.
- **Auth helpers** — `AuthOptions` / `AuthClaimMap` for configurable JWT claim mapping; `MAuthenticateTokenHelper<TPermission>` for token generation.
- **Validation base** — `MValidationObject` (base of `MEntity`) provides collected validation errors with a fluent pattern.
- **JSON serialization** — `IMJsonSerializeService` contract for pluggable JSON serialization; `MDateTimeConverter` for consistent DateTime handling.
- **UiEngine catalog contracts** — `ICatalogSnapshotStore`, `ICatalogScanService`, and the `MUiEngineCatalog*` model graph for the UI engine integration layer.

## Configuration

This package declares no DI registration extension. Configure it through the implementation package:

```csharp
// In your host, register Muonroi.Core (the implementation):
builder.Services.AddMuonroiCore(); // defined in Muonroi.Core
```

`AuthOptions` can be bound from `appsettings.json`:

```json
{
  "Auth": {
    "ClaimMap": {
      "UserIdentifier": "sub",
      "TenantId": "tid",
      "Permission": "permissions"
    }
  }
}
```

```csharp
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
```

## API Reference

| Type | Purpose |
|------|---------|
| `ISystemExecutionContext` | Read-only view of tenant, user, correlation, permissions, and source type for the current request |
| `ISystemExecutionContextAccessor` | Get/set/clear the `AsyncLocal`-backed execution context |
| `SystemExecutionContext` | Default implementation with a `.With(...)` copy-and-update method and a static `.Empty` sentinel |
| `IContextResolver` | Plug in custom tenant/user resolution logic (HTTP headers, gRPC metadata, etc.) |
| `NullContextResolver` | No-op resolver; returns `null` for all methods |
| `ITenantContextPolicy` | Enforce tenant/user context presence; throws typed exceptions on violation |
| `DefaultTenantContextPolicy` | Default policy backed by `IContextResolver` and optional `IConfiguration` |
| `MissingTenantContextException` | Thrown when tenant context is required but absent |
| `MissingUserContextException` | Thrown when user context is required but absent |
| `MResponse<T>` | Unified response envelope: `Result`, `Error`, `StatusCode`, `AddErrors` |
| `MVoidMethodResult` | Base response for void/command operations |
| `MErrorResult` | Single structured error with `ErrorCode` and `ErrorValues` |
| `MEntity` | Base entity: `Id`, `EntityId` (Guid), soft-delete, audit timestamps, domain events |
| `IMDomainEvent` | Marker interface for domain events collected on `MEntity` |
| `MNotFoundException` | 404-mapped domain exception; records `EntityName` and `EntityId` in `Details` |
| `MConflictException` | 409-mapped domain exception |
| `MUnauthorizedException` | 401-mapped security exception |
| `MInternalException` | 500-mapped internal exception; auto-captures caller member/file/line |
| `MArgumentException` | Argument validation exception |
| `IMEcosystemRegistry` | Query or register `MCapability` flags activated at startup |
| `IMTraceContext` | Begin/access distributed trace sessions scoped to async flow |
| `ITraceSession` | Active trace session with node recording |
| `ITraceSessionStore` | Persist and retrieve `MTraceSessionRecord` data |
| `AuthOptions` / `AuthClaimMap` | JWT claim key mapping for tenant, user, permissions |
| `MAuthenticateTokenHelper<TPermission>` | Generate signed JWT tokens from a `MTokenPayload` |
| `IMJsonSerializeService` | Pluggable JSON serialize/deserialize contract |
| `MValidationObject` | Base class for validation-aware objects |
| `ICatalogSnapshotStore` | Store/retrieve UiEngine catalog snapshots |
| `ICatalogScanService` | Scan and register UiEngine API/rule descriptors |

## Samples

No dedicated sample targets this package directly. The contracts are consumed by all runtime samples in this repository:

- [Muonroi.Experience.Sample](../../samples/Muonroi.Experience.Sample/) — demonstrates context and response patterns via the full runtime stack

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core`](../Muonroi.Core/) — runtime implementation: DI registration, `AddMuonroiCore()`, `SystemExecutionContextAccessor` wiring, JSON services
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — logging contracts depended on by this package

## License

Licensed under the [Apache License, Version 2.0](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE).
