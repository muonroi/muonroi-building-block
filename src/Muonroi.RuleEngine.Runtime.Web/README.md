# Muonroi.RuleEngine.Runtime.Web

> REST + SignalR governance layer for the Muonroi Rule Engine: managed ruleset CRUD, dry-run testing, real-time change broadcasts, and UI-engine manifest integration — all behind a single `AddRuleEngineRuntimeWeb` call.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.Runtime.Web.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.Runtime.Web/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-COMMERCIAL)

This package wires the runtime rule engine into an ASP.NET Core host. It adds controller endpoints for listing, exporting, validating, activating, and auditing rulesets; a SignalR hub that broadcasts per-tenant change events; a dry-run service for testing ruleset payloads without persistence; a hot-reload client that connects to Control Plane and invalidates the local cache on change; and a UI-engine manifest contributor that registers all ruleset screens, actions, and data sources into the Muonroi UI engine.

Requires a **Licensed** tier activation proof. The package depends on `Muonroi.RuleEngine.Runtime` for the core engine and `Muonroi.Integration.Abstractions` for UI-engine manifest support.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.Runtime.Web --prerelease
```

## Quick Start

### Control Plane host — expose the runtime API and SignalR hub

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRuleEngineRuntimeWeb(builder.Configuration);

var app = builder.Build();

app.MapControllers();
app.MapHub<RuleSetChangeHub>("/hubs/ruleset-changes");

app.Run();
```

`AddRuleEngineRuntimeWeb` registers the ruleset store, tracing pipeline, all REST controllers, the `RuleSetChangeHub` SignalR hub, the `RuleSetHubNotifier` background service, the dry-run service, and the UI-engine manifest contributor.

### Consumer app — receive hot-reload events

On a separate consumer (e.g. a service that executes rules), implement `IRuleSetChangeHandlerClient` and register the hot-reload background service:

```csharp
// Handler implementation
public sealed class MyRuleSetChangeHandler : IRuleSetChangeHandlerClient
{
    public Task OnRuleSetChangedAsync(
        RuleSetChangeEvent changeEvent,
        CancellationToken cancellationToken)
    {
        // Invalidate your local ruleset cache for changeEvent.TenantId / changeEvent.WorkflowName
        return Task.CompletedTask;
    }
}

// Registration
builder.Services.AddSingleton<IRuleSetChangeHandlerClient, MyRuleSetChangeHandler>();

builder.Services.AddRuleSetHotReload(opt =>
{
    opt.ControlPlaneUrl = "https://control-plane.example.com";
    opt.TenantId = "tenant-abc";               // single-tenant consumer
    // opt.SubscribeAllTenants = true;          // multi-tenant consumer — takes precedence over TenantId
    opt.ReconnectDelay = TimeSpan.FromSeconds(10);
    opt.AccessTokenFactory = () => Task.FromResult<string?>(myTokenProvider.GetToken());
});
```

## Features

- REST endpoints at `GET/POST/PUT/DELETE /api/v1/rule-engine/rulesets` for full ruleset lifecycle management (list, version history, export, validate, activate, audit, canary rollout)
- `POST /api/v1/rule-engine/rulesets/{workflow}/dry-run` — execute a ruleset payload (JSON, XML, or DMN) against input facts and get per-rule traces and output facts, without persisting anything
- SignalR hub (`RuleSetChangeHub`) broadcasts per-tenant change events; clients join `tenant:{id}` groups or the `all-tenants` group
- `AddRuleSetHotReload` — background client that connects to Control Plane, joins the configured tenant group(s), and calls `IRuleSetChangeHandlerClient.OnRuleSetChangedAsync` on each broadcast
- UI-engine manifest contributor (`RuntimeRuleSetManifestContributor`) registers list and editor screens, navigation nodes, and all associated actions and data sources into the Muonroi UI engine manifest
- Rule flow contract endpoints (`GET /api/v1/rule-flow/contract`) for the flow designer's I/O schema discovery

## Configuration

`AddRuleEngineRuntimeWeb` takes `IConfiguration` and internally binds rule tracing options from the `RuleTracing` section. No additional options class is needed for the main registration.

`AddRuleSetHotReload` is configured via `Action<RuleSetHotReloadOptions>`:

```json
// appsettings.json (values matched by code — configure programmatically via the Action delegate)
{
  "RuleSetHotReload": {
    "ControlPlaneUrl": "https://control-plane.example.com",
    "TenantId": "tenant-abc",
    "SubscribeAllTenants": false,
    "ReconnectDelay": "00:00:10"
  }
}
```

| Option | Type | Purpose |
|--------|------|---------|
| `ControlPlaneUrl` | `string?` | Base Control Plane URL or full hub URL |
| `TenantId` | `string?` | Single-tenant subscription group. Ignored when `SubscribeAllTenants` is `true` |
| `SubscribeAllTenants` | `bool` | Subscribe to the global `all-tenants` group; events carry `TenantId` per event |
| `AccessTokenFactory` | `Func<Task<string?>>?` | Bearer token factory for authenticated hubs |
| `ReconnectDelay` | `TimeSpan` | Retry interval after a failed connection (default: 10 s) |

## API Reference

| Type | Purpose |
|------|---------|
| `RuleEngineRuntimeWebExtensions.AddRuleEngineRuntimeWeb` | Registers all runtime web services, controllers, SignalR, and UI-engine contributor |
| `RuleSetHotReloadExtensions.AddRuleSetHotReload` | Registers the SignalR hot-reload background client on a consumer host |
| `RuleSetHotReloadOptions` | Options for `AddRuleSetHotReload`: hub URL, tenant subscription, auth, reconnect delay |
| `IRuleSetChangeHandlerClient` | Implement to receive `RuleSetChangeEvent` when a ruleset changes on Control Plane |
| `IRuleDryRunService` | Executes ruleset payloads (JSON/XML/DMN) against input facts, returns traces and output facts |
| `RuleDryRunResult` | Dry-run output: `RulesMatched`, `Traces`, `EvaluationTime`, `Errors`, `OutputFacts` |
| `RuleExecutionTrace` | Per-rule trace: `RuleName`, `Matched`, `FailReason`, `ChangedFactKeys`, `ElapsedMs` |
| `RuleSetFormat` | Enum: `Json`, `Xml`, `Dmn` — supported dry-run payload formats |
| `RuleSetChangeHub` | SignalR hub — `JoinTenantGroup`, `JoinAllTenantsGroup`, `LeaveTenantGroup` |
| `IMRuleFlowContractProvider` | Resolves rule/flow I/O contracts for the flow designer; override to customize lookup |
| `RuntimeRuleSetManifestContributor` | UI-engine contributor — registers list/editor screens, navigation, actions, data sources |

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation (`LicenseTier.Licensed`). See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).

## Related Packages

- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — core runtime engine, ruleset store, canary rollout, FEEL/DMN adapters; required dependency
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — shared contracts (`IRule`, `FactBag`, `OrchestratorResult`)
- [`Muonroi.Integration.Abstractions`](../Muonroi.Integration.Abstractions/) — UI-engine manifest contracts (`IUiEngineManifestContributor`)
- [`Muonroi.RuleEngine.DecisionTable.Web`](../Muonroi.RuleEngine.DecisionTable.Web/) — REST endpoints for decision-table governance (parallel web extension for the decision-table subsystem)

## License

Commercial license — requires a valid Muonroi license activation at `LicenseTier.Licensed` or above. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL) for terms.
