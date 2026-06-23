# Muonroi.AspNetCore.RuleEngine

> ASP.NET Core integration that wires the Muonroi Rule Engine into Auto CRUD controllers, business-rule orchestration, and tenant-scoped rule change management — in a single call.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AspNetCore.RuleEngine.svg)](https://www.nuget.org/packages/Muonroi.AspNetCore.RuleEngine/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.AspNetCore.RuleEngine` bridges `Muonroi.RuleEngine.Core` / `Muonroi.RuleEngine.Runtime` with ASP.NET Core. It provides `AddRuleEngineInfrastructure`, which registers the rule store, change-management stores, and generic controller wiring in one call. `MGenericController<TEntity, TDbContext>` automatically serves paginated list, get-by-id, create, update, and soft-delete endpoints for every `MEntity` subclass found in the supplied assemblies, executing business rules before and after each mutation.

> **License gate**: `AddRuleEngineStore` calls `EnsureFeatureOrThrow(Premium.RuleEngine)` at startup. A license that includes the `RuleEngine` premium feature must be present for the application to start.

## Installation

```bash
dotnet add package Muonroi.AspNetCore.RuleEngine --prerelease
```

## Quick Start

```csharp
using System.Reflection;
using Muonroi.AspNetCore.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Registers in one call:
//   - rule engine store (bound from "RuleStoreConfigs" configuration section)
//   - IRuleChangeStore        -> InMemoryRuleChangeStore
//   - IRuleChangeProposalStore -> InMemoryRuleChangeProposalStore
//   - MVC generic controller wiring (GenericControllerRouteConvention +
//     GenericControllerFeatureProvider) for MGenericController<TEntity, TDbContext>
builder.Services.AddRuleEngineInfrastructure(
    builder.Configuration,
    Assembly.GetExecutingAssembly()); // scanned for MEntity subclasses + MDbContext

WebApplication app = builder.Build();
app.MapControllers();
app.Run();
```

### Adding business rules for CRUD

```csharp
// Register a RuleOrchestrator for ProductEntity with one validation rule and one hook.
builder.Services.AddCrudRules<ProductEntity>(rules =>
{
    rules.AddCrudRule<ProductEntity, ProductValidationRule>();
});
builder.Services.AddCrudHook<ProductEntity, AuditHook>();
builder.Services.AddCrudRuleListener<ProductEntity, ProductRuleEventListener>();
```

`ProductValidationRule` must implement `IRule<CrudContext<ProductEntity>>`.  
The orchestrator runs matching rules at `HookPoint.BeforeRule` and `HookPoint.AfterRule` for each Create/Update/Delete call. If a rule sets `context.CancelOperation = true` or populates `context.ValidationErrors`, the controller returns `400 Bad Request` with the error message — the database write never occurs.

## Features

- **Single-call infrastructure setup** — `AddRuleEngineInfrastructure` registers the rule store, both change-management stores, and all MVC generic controller plumbing.
- **Auto CRUD** — `MGenericController<TEntity, TDbContext>` generates `GET /api/v{version}/[controller]`, `GET /{id}`, `POST`, `PUT/{id}`, and `DELETE/{id}` for every non-abstract `MEntity` subclass discovered in the provided assemblies.
- **Business rule hooks** — rules execute at `BeforeRule` and `AfterRule` hook points for Create, Update, and Delete operations; returning a failure cancels the operation without touching the database.
- **Mass-assignment protection** — the Update endpoint guards system fields (`EntityId`, `CreationTime`, `TenantId`, etc.) from being overwritten.
- **Soft delete with audit trail** — Delete sets `IsDeleted`, `DeletionTime`, and `DeletedUserId`; records are never physically removed.
- **Multi-tenant isolation** — for entities implementing `ITenantScoped`, queries and writes are automatically filtered to the current tenant (requires `MultiTenantConfigs:Enabled = true` and a Premium.MultiTenant license).
- **Permission enforcement** — decorating a derived controller with `[GenericCrudPermission]` enables RBAC checks backed by role-permission queries with multi-level cache support.
- **Rule change management** — `IRuleChangeStore` tracks per-tenant, per-endpoint rule ordering with full history and rollback. `IRuleChangeProposalStore` supports a propose → approve/reject workflow.
- **OpenTelemetry** — `UiEngineTelemetryDescriptor` exports OTLP metrics via the registered `OpenTelemetry.Exporter.OpenTelemetryProtocol` dependency.

## Configuration

```json
{
  "RuleStoreConfigs": {
    // populated by Muonroi.RuleEngine.Runtime's AddRuleEngineStore internals
  },
  "MultiTenantConfigs": {
    "Enabled": false,
    "RequireTenantClaimForAuthenticatedUser": true
  }
}
```

The `RuleStoreConfigs` section is consumed by `AddRuleEngineStore` (from `Muonroi.RuleEngine.Runtime`). Refer to that package's documentation for all available keys.

## API Reference

| Type | Purpose |
|------|---------|
| `RuleEngineInfrastructureExtensions.AddRuleEngineInfrastructure` | Main entry point — registers store, change stores, and MVC generic controller wiring |
| `CrudRuleExtensions.AddCrudRules<TEntity>` | Registers a `RuleOrchestrator<CrudContext<TEntity>>` with rules, hooks, and listeners |
| `CrudRuleExtensions.AddCrudRule<TEntity, TRule>` | Registers a single `IRule<CrudContext<TEntity>>` |
| `CrudRuleExtensions.AddCrudHook<TEntity, THook>` | Registers a `IHookHandler<CrudContext<TEntity>>` |
| `CrudRuleExtensions.AddCrudRuleListener<TEntity, TListener>` | Registers an `IRuleEventListener<CrudContext<TEntity>>` |
| `MGenericController<TEntity, TDbContext>` | Auto CRUD controller base; override any action to customize behavior |
| `CrudContext<TEntity>` | Context object passed to rules — holds `Entity`, `OriginalEntity`, `OperationType`, `UserId`, `TenantId`, `ValidationErrors`, `CancelOperation`, `CancellationReason`, and `Metadata` |
| `CrudOperationType` | Enum: `Create`, `Update`, `Delete`, `Read` |
| `IRuleChangeStore` | Per-tenant, per-endpoint rule ordering: `GetCurrentAsync`, `ApplyAsync`, `RollbackAsync`, `GetHistoryAsync` |
| `IRuleChangeProposalStore` | Rule change proposal workflow: `ProposeAsync`, `GetAsync`, `ApproveAsync`, `RejectAsync`, `ListPendingAsync` |
| `GenericControllerFeatureProvider` | `IApplicationFeatureProvider<ControllerFeature>` — discovers `MEntity` subclasses and registers generic controllers |
| `GenericControllerRouteConvention` | Strips the `Entity` suffix from controller names for clean routes |

## Samples

- [Quickstart.AspNetCore.RuleEngine](../../samples/Quickstart.AspNetCore.RuleEngine/) — end-to-end wiring of `AddRuleEngineInfrastructure`, rule change ordering via `IRuleChangeStore`, and generic controller auto-discovery.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS) — runtime rule orchestration requires a `Premium.RuleEngine` license at startup

## Related Packages

- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — contracts: `IRule<TContext>`, `RuleResult`, `FactBag`, `HookPoint`, `CrudContext` base types
- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — `RuleOrchestrator<TContext>`, fluent builder, workflow runner
- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — persistence-backed rule store, canary rollout, HMAC/RSA signing, hot-reload
- [`Muonroi.AspNetCore`](../Muonroi.AspNetCore/) — base ASP.NET Core infrastructure (`AddInfrastructure`, `MDbContext`, middleware)

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for full terms.  
The Rule Engine feature (`Premium.RuleEngine`) is license-gated and requires a valid Muonroi license at application startup.
