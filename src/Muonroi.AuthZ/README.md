# Muonroi.AuthZ

> Rule-engine-driven authorization for ASP.NET Core — composable policies, row-level security, and live hot-reload from the Control Plane, all without writing policy strings.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AuthZ.svg)](https://www.nuget.org/packages/Muonroi.AuthZ/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-red.svg)](LICENSE-COMMERCIAL)

Muonroi.AuthZ replaces hard-coded `[Authorize(Policy = "…")]` strings with composable `IRule<AuthorizationRuleContext>` implementations evaluated by the Muonroi Rule Engine. Authorization decisions are data-driven: add or remove rules without redeploying the host. Row-level security is provided through `IRuleRowFilter<T>`, and rule sets can be reloaded at runtime via a SignalR connection to the Control Plane.

## Installation

```bash
dotnet add package Muonroi.AuthZ --prerelease
```

## Quick Start

**1. Register the rule engine evaluator**

```csharp
// Program.cs
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IMCacheService, InMemoryCacheService>(); // swap for Redis in production

builder.Services.AddMAuthorizationRuleEngine();

// Register one or more authorization rules
builder.Services.AddScoped<IRule<AuthorizationRuleContext>, ManagerOnlyDeleteRule>();
```

**2. Write a rule**

```csharp
public sealed class ManagerOnlyDeleteRule : IRule<AuthorizationRuleContext>
{
    public string Code => "authz.manager-only-delete";

    public Task<RuleResult> EvaluateAsync(
        AuthorizationRuleContext ctx, FactBag facts, CancellationToken ct)
    {
        bool isDelete = string.Equals(ctx.Action, "delete", StringComparison.OrdinalIgnoreCase);
        if (!isDelete)
            return Task.FromResult(RuleResult.Passed());

        bool isManager = ctx.Roles.Contains("manager", StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(isManager
            ? RuleResult.Passed()
            : RuleResult.Failure($"User '{ctx.UserId}' lacks the 'manager' role required to delete '{ctx.Resource}'."));
    }
}
```

**3. Protect an endpoint**

```csharp
app.MapDelete("/orders/{id}", (string id) => Results.Ok())
   .RequireRuleEngineAuthorization("orders", "delete");
```

## Features

- Rule-engine-driven authorization via `IRule<AuthorizationRuleContext>` — compose, add, or remove rules without policy-name strings
- Integrates with ASP.NET Core `IAuthorizationHandler` pipeline; works with `[Authorize]` and minimal-API `.RequireAuthorization()`
- Row-level security through `IRuleRowFilter<T>` — rules narrow an `IQueryable<T>` per user/tenant
- SignalR-based hot-reload of rule sets from the Control Plane — no application restart required
- `AuthorizationRuleContext` carries `UserId`, `TenantId`, `Resource`, `Action`, `Roles`, and arbitrary `Claims` for RBAC and ABAC rules

## Configuration

### DI registration

```csharp
// Minimal — rule-engine evaluator only
builder.Services.AddMAuthorizationRuleEngine();

// Optional — hot-reload from Control Plane
builder.Services.AddMAuthorizationHotReload(options =>
{
    options.ControlPlaneUrl = "https://control-plane.example.com";
    options.TenantId = "tenant-abc";
    options.AccessTokenFactory = async () => await tokenProvider.GetTokenAsync();
    options.ReconnectDelay = TimeSpan.FromSeconds(10); // default
});
```

### Hot-reload options (`AuthRuleHotReloadOptions`)

| Property | Type | Description |
|---|---|---|
| `ControlPlaneUrl` | `string?` | Base Control Plane URL or full auth-rule hub URL |
| `TenantId` | `string?` | Tenant group to subscribe to after connecting |
| `AccessTokenFactory` | `Func<Task<string?>>?` | Bearer token factory when the hub requires authentication |
| `ReconnectDelay` | `TimeSpan` | Delay before retrying a failed connection (default: 10 s) |

### Reacting to rule changes

Register a custom `IAuthRuleChangeHandler` before calling `AddMAuthorizationRuleEngine()` to invalidate caches when the Control Plane publishes a new rule set:

```csharp
builder.Services.AddSingleton<IAuthRuleChangeHandler, MyRuleCacheInvalidator>();
builder.Services.AddMAuthorizationRuleEngine();
```

```csharp
public sealed class MyRuleCacheInvalidator : IAuthRuleChangeHandler
{
    public Task OnAuthRuleChangedAsync(Guid ruleSetId, CancellationToken ct = default)
    {
        // Invalidate caches, signal rule re-evaluation, etc.
        return Task.CompletedTask;
    }
}
```

## API Reference

| Type | Purpose |
|---|---|
| `AuthZServiceExtensions.AddMAuthorizationRuleEngine` | Registers `IAuthorizationPolicyEvaluator`, `IAuthorizationHandler`, `IMRuleOrchestrator<AuthorizationRuleContext>`, and `IRuleRowFilter<>` |
| `AuthZServiceExtensions.AddMAuthorizationHotReload` | Adds a hosted `AuthRuleHotReloadClient` that reconnects to the Control Plane SignalR hub |
| `IEndpointConventionBuilder.RequireRuleEngineAuthorization(resource, action)` | Attaches a `MuonroiAuthorizationRequirement` to a minimal-API endpoint |
| `IAuthorizationPolicyEvaluator` | Evaluates all `IRule<AuthorizationRuleContext>` instances; returns `AuthorizationResult.Allow()` only when all rules pass |
| `AuthorizationRuleContext` | Fact carrier: `UserId`, `TenantId`, `Resource`, `Action`, `Roles`, `Claims` |
| `AuthorizationResult` | Discriminated result: `Allow()` / `Deny(reason)` |
| `MuonroiAuthorizationRequirement` | ASP.NET Core `IAuthorizationRequirement` carrying `Resource` + `Action` |
| `IRuleRowFilter<T>` | Applies `IRule<RowFilterContext<T>>` rules to narrow an `IQueryable<T>` |
| `RowFilterContext<T>` | Rule context for row filtering: `UserId`, `TenantId`, `Roles`, `Query` |
| `IAuthRuleChangeHandler` | Override to react when the Control Plane publishes a new rule set |
| `AuthRuleHotReloadOptions` | Configuration for the SignalR hot-reload client |

## Samples

- [Quickstart.AuthZ](../../samples/Quickstart.AuthZ/) — minimal ASP.NET Core API demonstrating `AddMAuthorizationRuleEngine`, a custom `IRule<AuthorizationRuleContext>` rule, and endpoint-level `RequireRuleEngineAuthorization`

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires license activation

## Related Packages

- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — rule orchestration engine that evaluates `IRule<T>` chains
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — shared guards and ecosystem primitives
- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — governance and compliance contracts used by the evaluator

## License

This package is **commercially licensed**. A valid Muonroi license is required to use it in production. Contact [muonroi.com](https://muonroi.com) for activation details.
