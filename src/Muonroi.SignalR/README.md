# Muonroi.SignalR
> Multi-tenant SignalR integration with real-time UI schema notification support for Muonroi.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.SignalR.svg)](https://www.nuget.org/packages/Muonroi.SignalR/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

`Muonroi.SignalR` provides extensions and filters for seamlessly integrating ASP.NET Core SignalR into the Muonroi Building Block ecosystem. It bridges real-time web socket communications with Muonroi's core tenets: multi-tenancy, strict authorization, and dynamic UI schema delivery.

A primary feature of this package is the `TenantHubFilter`, which ensures that all real-time hub invocations are strictly scoped to the active tenant and validated against the enterprise license guard. Additionally, it ships with an out-of-the-box `MUiEngineHub` designed to stream UI schema modifications to connected clients dynamically.

## Features

- **Multi-Tenant Hub Filtering**: The `TenantHubFilter` intercepts all SignalR hub method invocations to enforce tenant resolution, token validation (`MTokenInfo`), and licensing (`ILicenseGuard`).
- **Seamless DI Integration**: `AddSignalRWithTenant` makes registering SignalR with tenant-safety a single-line operation.
- **UI Engine Integration**: Includes `MUiEngineHub` and `IUiEngineSchemaNotifier` for broadcasting real-time frontend schema updates to clients, enabling dynamic, hot-reloading user interfaces.

## Installation

```bash
dotnet add package Muonroi.SignalR
```

## Quick Start

### 1. Registering SignalR

During application startup, register SignalR using the provided extension method. This automatically attaches the `TenantHubFilter` to all Hubs globally.

```csharp
using Muonroi.SignalR.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Adds SignalR and configures the TenantHubFilter
builder.Services.AddSignalRWithTenant(builder.Configuration);
```

### 2. Mapping the Hub

Map the default `MUiEngineHub` to allow clients to subscribe to schema changes.

```csharp
var app = builder.Build();

app.UseRouting();

// Map the provided hub
app.MapHub<MUiEngineHub>("/hubs/mui-engine");

 await app.RunAsync();
```

### 3. Broadcasting Schema Updates

Inject the `IUiEngineSchemaNotifier` into your backend services (like a schema designer controller) to notify connected clients when a schema changes.

```csharp
using Muonroi.SignalR.Notifications;

public class SchemaDesignService
{
    private readonly IUiEngineSchemaNotifier _schemaNotifier;

    public SchemaDesignService(IUiEngineSchemaNotifier schemaNotifier)
    {
        _schemaNotifier = schemaNotifier;
    }

    public async Task PublishNewSchemaAsync(string schemaId, string newDefinition)
    {
        // Save to DB...
        
        // Notify all clients subscribed to schema changes
        await _schemaNotifier.NotifySchemaChangedAsync(schemaId, newDefinition);
    }
}
```

## API Reference

### `TenantHubFilter`
Implements `IHubFilter`. Intercepts `InvokeMethodAsync` to resolve the current tenant (via `ITenantIdResolver`), validates the user's token (`MTokenInfo`), and ensures the tenant's license (`ILicenseGuard`) is valid before allowing the hub method to execute. If validation fails, the connection is typically terminated or rejected.

### `MUiEngineHub`
A pre-built `Hub` allowing clients to call `SubscribeToSchemaChanges()` and `UnsubscribeFromSchemaChanges()`. It adds the connection to a specific group (`MSchemaWatcherGroup`).

### `IUiEngineSchemaNotifier`
Contract for broadcasting events to the `MUiEngineHub`. 
- `NotifySchemaChangedAsync(string schemaId, string newDefinition)`

### `SignalRServiceCollectionExtensions`
- `AddSignalRWithTenant(IServiceCollection, IConfiguration)`: Core setup method for `Startup.cs` or `Program.cs`.

## Client Usage (JavaScript Example)

Connected clients can subscribe using the standard `@microsoft/signalr` package:

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/mui-engine", {
        accessTokenFactory: () => "YOUR_JWT_TOKEN" // Required by TenantHubFilter
    })
    .build();

connection.on("SchemaChanged", (schemaId, newDefinition) => {
    console.log(`Schema ${schemaId} updated! Reloading UI...`);
    // update your React/Vue/Angular state
});

await connection.start();
await connection.invoke("SubscribeToSchemaChanges");
```

## Ecosystem Combinations

> Works great standalone. Becomes **significantly more powerful** when combined.

### + Tenancy -> Hub connections filtered by tenant: broadcast only reaches correct tenant's clients
Ensure that real-time messages are securely isolated across tenant boundaries using TenantHubFilter.

### + Auth -> JWT bearer auth for hub connections
Use the MTokenInfo to authorize connections and map them to specific users.

### + RuleEngine.Runtime.Web -> RuleSetChangeHub notifies clients when rules hot-reload
Broadcast live updates to administrative dashboards whenever a decision table or rule set is modified.

### + Caching.Redis -> Redis backplane for multi-pod SignalR scale-out
Use the Redis backplane to ensure broadcasts reach all connected clients across a horizontally scaled cluster.

### + Observability -> Hub message counts tracked per tenant as OTel metrics
Measure concurrent connections and message throughput on a per-tenant basis natively.

### Full Stack
`csharp
// combined registration
builder.Services.AddSignalRWithTenant(builder.Configuration);
builder.Services.AddMuonroiAuth();
builder.Services.AddSignalR().AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis"));
`

## Samples
- samples/RealTimeDashboard/
- samples/RuleSourceGen/


## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
