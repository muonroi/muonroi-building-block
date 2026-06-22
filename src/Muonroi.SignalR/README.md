# Muonroi.SignalR

> Real-time UI engine schema notifications over SignalR with optional per-tenant hub filtering.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.SignalR.svg)](https://www.nuget.org/packages/Muonroi.SignalR/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-COMMERCIAL)

`Muonroi.SignalR` wires ASP.NET Core SignalR into the Muonroi open-core ecosystem. It ships a `Hub` that clients subscribe to for live schema change events, a broadcaster (`IUiEngineSchemaNotifier`) that pushes `SchemaChanged` messages to all subscribers, and a hub filter (`TenantHubFilter`) that enforces per-tenant context and license checks when multi-tenancy is enabled.

## Installation

```bash
dotnet add package Muonroi.SignalR --prerelease
```

## Quick Start

```csharp
using Muonroi.Logging;
using Muonroi.SignalR.SignalR;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Required: IMLog<T> used internally by MUiEngineSchemaNotifier.
builder.Services.AddLogging(lb => lb.AddMuonroiLogging());

// Registers SignalR + TenantHubFilter when MultiTenantConfigs:Enabled = true.
builder.Services.AddSignalRWithTenant(builder.Configuration);

// Broadcasts schema-change events to the "mui-engine-schema-watchers" group.
builder.Services.AddSingleton<IUiEngineSchemaNotifier, MUiEngineSchemaNotifier>();

WebApplication app = builder.Build();

// Clients connect here and call SubscribeToSchemaChanges() to join the watcher group.
app.MapHub<MUiEngineHub>("/hubs/ui-engine");

app.Run();
```

Trigger a broadcast from any service:

```csharp
public class SchemaPublishService(IUiEngineSchemaNotifier notifier)
{
    public Task PublishAsync(MUiEngineSchemaVersion version, CancellationToken ct)
        => notifier.NotifySchemaChangedAsync(version, ct);
}
```

Clients receive the `SchemaChanged` event with `(string schemaHash, MUiEngineSchemaVersion version)` arguments.

## Features

- `AddSignalRWithTenant(IConfiguration)` — single call that invokes `AddSignalR()` and conditionally registers `TenantHubFilter` when `MultiTenantConfigs:Enabled` is `true`.
- `MUiEngineHub` — typed `Hub` with `SubscribeToSchemaChanges()` / `UnsubscribeFromSchemaChanges()` group management. Hub group name: `"mui-engine-schema-watchers"`.
- `MUiEngineSchemaNotifier` — implements `IUiEngineSchemaNotifier`; resolves `IHubContext<MUiEngineHub>` at broadcast time and sends `SchemaChanged` to the watcher group. Gracefully skips if the hub context is unavailable.
- `TenantHubFilter` — per-invocation `IHubFilter` that resolves tenant ID via `ITenantIdResolver`, enforces the `Premium.MultiTenant` license feature, and sets `TenantContext.CurrentTenantId` for the duration of the call.

## Configuration

### DI registration

```csharp
builder.Services.AddSignalRWithTenant(builder.Configuration);
builder.Services.AddSingleton<IUiEngineSchemaNotifier, MUiEngineSchemaNotifier>();
```

### appsettings.json

The `TenantHubFilter` is registered only when `MultiTenantConfigs:Enabled` is `true`. The section name is `"MultiTenantConfigs"` (from `MultiTenantConfigs.SectionName` in `Muonroi.Tenancy.Core`).

```json
{
  "MultiTenantConfigs": {
    "Enabled": true
  }
}
```

When `Enabled` is `false` (or the section is absent), the hub filter is not registered and no tenant resolution occurs.

## API Reference

| Type | Purpose |
|------|---------|
| `SignalRServiceCollectionExtensions.AddSignalRWithTenant` | Extension method — registers SignalR and conditionally adds `TenantHubFilter` |
| `MUiEngineHub` | SignalR `Hub`; exposes `SubscribeToSchemaChanges()` and `UnsubscribeFromSchemaChanges()` |
| `IUiEngineSchemaNotifier` | Contract for broadcasting `MUiEngineSchemaVersion` payloads to connected clients |
| `MUiEngineSchemaNotifier` | Default implementation of `IUiEngineSchemaNotifier`; sends `SchemaChanged` via `IHubContext<MUiEngineHub>` |
| `TenantHubFilter` | `IHubFilter` that sets `TenantContext.CurrentTenantId` per hub invocation and validates tenant/license requirements |
| `MUiEngineSchemaVersion` | Payload carrying `Version`, `SchemaHash`, `OpenApiHash`, and `GeneratedAtUtc` |

## Samples

- [Quickstart.SignalR](../../samples/Quickstart.SignalR/) — minimal ASP.NET Core API demonstrating `AddSignalRWithTenant`, `MUiEngineHub` mapping, and `IUiEngineSchemaNotifier` registration.

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `IUiEngineSchemaNotifier` (in `Muonroi.Core.Abstractions.Interfaces`) and `MUiEngineSchemaVersion` / `MUiEngineSchemaVersion` payload types.
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — provides `MultiTenantConfigs`, `ITenantIdResolver`, and `TenantContext` used by `TenantHubFilter`.
- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — supplies `ITenantIdResolver` and `TenantContext` interfaces consumed by the filter.
- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — provides `ILicenseGuard` and `FreeTierFeatures` used for license enforcement in `TenantHubFilter`.
- [`Muonroi.Core`](../Muonroi.Core/) — core runtime utilities referenced by this package.

## License

This package is distributed under the **Muonroi Commercial License**. A valid license key is required for use. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL) or contact [leanhphi1706@gmail.com](mailto:leanhphi1706@gmail.com).
