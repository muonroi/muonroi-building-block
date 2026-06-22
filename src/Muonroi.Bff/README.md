# Muonroi.Bff

> Hardened Backend-for-Frontend authentication for ASP.NET Core SPAs: cookie auth, CSRF protection, and server-side refresh-token storage in one call.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Bff.svg)](https://www.nuget.org/packages/Muonroi.Bff/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-red.svg)](../../LICENSE-COMMERCIAL)

SPAs that rely on `localStorage` or `sessionStorage` for tokens are vulnerable to XSS. `Muonroi.Bff` solves this by configuring cookie-based authentication (`HttpOnly`, `Secure`, `SameSite=Strict`), antiforgery (CSRF) protection with the same hardened cookie policy, and a server-side `ITokenStore` that keeps refresh tokens away from the browser entirely. The package plugs into the Muonroi ecosystem and supports both in-memory (single-instance) and Redis-backed (distributed) token storage.

## Installation

```bash
dotnet add package Muonroi.Bff --prerelease
```

## Quick Start

```csharp
using Muonroi.Bff;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Registers cookie auth + antiforgery + ITokenStore (in-memory by default).
// Pass useRedisTokenStore: true for multi-instance / distributed deployments.
builder.Services.AddBffAuthentication(useRedisTokenStore: false);

builder.Services.AddAuthorization();
builder.Services.AddControllers();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

Switch to the Redis-backed store for multi-instance deployments:

```csharp
// Requires Muonroi.Caching (IMCacheService) to be registered.
builder.Services.AddBffAuthentication(useRedisTokenStore: true);
```

Configure the Redis token TTL in `appsettings.json` (defaults to 30 days if absent):

```json
{
  "Authentication": {
    "RefreshTokenLifetimeMinutes": 43200
  }
}
```

## Features

- Single `AddBffAuthentication()` call wires cookie authentication, antiforgery, and a token store.
- Cookie policy enforces `HttpOnly = true`, `SecurePolicy = Always`, `SameSite = Strict` for both the auth cookie and the antiforgery cookie.
- `InMemoryTokenStore` — zero dependencies, suitable for testing or single-instance deployments.
- `RedisTokenStore` — distributed store backed by `IMCacheService`; automatically tenant-scoped via `CacheEntryOptions.TenantScoped = true`; TTL resolved from `Authentication:RefreshTokenLifetimeMinutes` or `Bff:RefreshTokenLifetimeMinutes`.
- `ITokenStore` is public — plug in a custom implementation by registering it before calling `AddBffAuthentication`, or by registering it directly after.

## Configuration

### DI Registration

```csharp
// In-memory (default — no extra dependencies)
services.AddBffAuthentication();

// Redis-backed (requires IMCacheService from Muonroi.Caching)
services.AddBffAuthentication(useRedisTokenStore: true);
```

### appsettings.json keys (Redis store)

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `Authentication:RefreshTokenLifetimeMinutes` | `int` | `43200` | Refresh-token TTL in minutes (30 days) |
| `Bff:RefreshTokenLifetimeMinutes` | `int` | `43200` | Alternative key; `Authentication:*` takes precedence |

## API Reference

| Type | Purpose |
|------|---------|
| `BffAuthenticationExtensions.AddBffAuthentication(bool)` | Registers cookie auth, antiforgery, and `ITokenStore` in one call |
| `ITokenStore` | Server-side refresh-token store contract (`StoreRefreshTokenAsync`, `GetRefreshTokenAsync`, `RemoveRefreshTokenAsync`) |
| `InMemoryTokenStore` | `ITokenStore` backed by `ConcurrentDictionary`; single-instance only |
| `RedisTokenStore` | `ITokenStore` backed by `IMCacheService`; tenant-scoped; distributed-safe |

## Samples

- [Quickstart.Bff](../../samples/Quickstart.Bff/) — Minimal ASP.NET Core API demonstrating `AddBffAuthentication()` with the in-memory token store and Swagger UI

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation (see `LICENSE-COMMERCIAL`)

## Related Packages

- [`Muonroi.Caching.Abstractions`](../Muonroi.Caching.Abstractions/) — provides `IMCacheService` consumed by `RedisTokenStore`
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `MGuard` input validation used by `RedisTokenStore`

## License

This package is distributed under a **Commercial license**. A valid Muonroi license key is required for production use. See [`LICENSE-COMMERCIAL`](../../LICENSE-COMMERCIAL) for terms.
