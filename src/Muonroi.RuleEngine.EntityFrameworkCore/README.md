# Muonroi.RuleEngine.EntityFrameworkCore

> PostgreSQL persistence provider for the Muonroi Rule Engine — swaps the default in-memory store for a durable, tenant-isolated, audit-signed PostgreSQL backend.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.EntityFrameworkCore/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package registers `RuleEngineDbContext` (Npgsql) and replaces the abstract `IRuleSetStore` / `IRuleSetAuditStore` bindings introduced by `Muonroi.RuleEngine.Runtime` with their PostgreSQL implementations. It adds optional maker-checker approval workflow and per-tenant canary rollouts on top. Row-Level Security (RLS) isolation is opt-in through the standard `MultiTenantOptions`.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.EntityFrameworkCore --prerelease
```

## Quick Start

Call `AddMRuleEngineWithPostgres` during service configuration and then apply EF Core migrations before first run:

```csharp
// Program.cs
builder.Services.AddMRuleEngineWithPostgres(
    connectionString: builder.Configuration.GetConnectionString("RuleEngine")!,
    configureOptions: options =>
    {
        options.RequireApproval = true;           // enable maker-checker gate
        options.EnableCanary    = true;           // enable canary rollout
        options.AuditSignerKeyId     = "ruleset-v1";
        options.AuditPrivateKeyPemPath = "/run/secrets/audit.pem"; // optional RSA signing
    });

// optionally enable approval or canary independently:
// builder.Services.AddMRuleEngineApprovalWorkflow();
// builder.Services.AddMCanaryRollout();
```

Apply migrations at startup or via CLI:

```bash
dotnet ef database update --project src/Muonroi.RuleEngine.EntityFrameworkCore
```

## Features

- **PostgreSQL-backed ruleset store** — `PostgresRuleSetStore` implements `IRuleSetStore` with versioned rows, `(tenantId, workflowName, version)` unique index, and activation semantics.
- **Immutable published versions** — `RuleEngineDbContext.SaveChangesAsync` enforces forward-only status transitions (`Draft → PendingApproval → Approved → Active → Superseded`) and blocks JSON mutation once a version reaches `PendingApproval` or beyond.
- **Durable audit trail** — `PostgresRuleSetAuditStore` persists every lifecycle event. Audit records are optionally RSA-signed; `RsaRuleSetAuditSigner` is resolved from inline PEM (`AuditPrivateKeyPem`), a file path (`AuditPrivateKeyPemPath`), or an ephemeral key.
- **Maker-checker approval workflow** — `RuleSetApprovalService` (`IRuleSetApprovalService`) enforces `SubmitForApprovalAsync` / `ApproveAsync` / `RejectAsync` transitions when `RequireApproval = true`.
- **Canary rollout** — `CanaryRolloutService` (`ICanaryRolloutService`) targets a subset of tenant IDs before full promotion; 30-second lookup cache keeps the hot path cheap.
- **Row-Level Security** — `TenantRlsConnectionInterceptor` injects the current tenant ID as a PostgreSQL session variable on every connection when `MultiTenantOptions.EnableRowLevelSecurity` is `true`.
- **PDF template domain** — `RuleEngineDbContext` also owns `PdfTemplates`, `PdfTemplateVersions`, and `PdfTemplateApprovals` DbSets, with the same immutability guards and forward-only status machine.
- **Traceability domain** — `Requirements`, `RuleLinks`, `TestLinks`, `DryRunExamples`, and `CopilotDraftProvenance` DbSets support requirement-to-rule-to-test traceability.
- **Outbound sync jobs** — `OutboundSyncJobs` DbSet for tenant-scoped, retriable sync tasks.
- **Ingested source documents** — `IngestedSourceDocuments` DbSet for classified source material (doc type: `rule` | `business`).

## Configuration

### DI registration

```csharp
// Full registration (store + audit + approval + canary):
services.AddMRuleEngineWithPostgres(connectionString, options => { ... });

// Add approval gate to an existing registration:
services.AddMRuleEngineApprovalWorkflow();

// Add canary rollout to an existing registration:
services.AddMCanaryRollout();
```

### `RuleControlPlaneOptions` (`appsettings.json` section `"RuleControlPlane"`)

| Property | Type | Default | Description |
|---|---|---|---|
| `RequireApproval` | `bool` | `false` | Require explicit approval before a ruleset becomes active. |
| `NotifyOnStateChange` | `bool` | `true` | Emit `IRuleSetChangeNotifier` events on lifecycle transitions. |
| `EnableCanary` | `bool` | `true` | Allow canary rollouts targeting specific tenants. |
| `AuditSignerKeyId` | `string` | `"ruleset-control-plane"` | Key identifier embedded in each signed audit record. |
| `AuditPrivateKeyPem` | `string?` | `null` | Inline PEM private key for RSA audit signing. |
| `AuditPrivateKeyPemPath` | `string?` | `null` | File path to PEM private key for RSA audit signing. |

When neither `AuditPrivateKeyPem` nor `AuditPrivateKeyPemPath` is supplied, an ephemeral RSA key is generated per process start.

### `appsettings.json` example

```json
{
  "ConnectionStrings": {
    "RuleEngine": "Host=localhost;Database=rule_engine;Username=app;Password=secret"
  },
  "RuleControlPlane": {
    "RequireApproval": true,
    "EnableCanary": true,
    "AuditSignerKeyId": "ruleset-v1",
    "AuditPrivateKeyPemPath": "/run/secrets/audit.pem"
  },
  "MultiTenant": {
    "EnableRowLevelSecurity": true
  }
}
```

## API Reference

| Type | Purpose |
|------|---------|
| `ServiceCollectionExtensions` | `AddMRuleEngineWithPostgres`, `AddMRuleEngineApprovalWorkflow`, `AddMCanaryRollout` — the three DI entry points. |
| `RuleEngineDbContext` | EF Core `DbContext` for all rule-engine and PDF-template tables; enforces immutability on `SaveChangesAsync`. |
| `PostgresRuleSetStore` | Implements `IRuleSetStore` — versioned save, load, activate, and list over PostgreSQL. |
| `PostgresRuleSetAuditStore` | Implements `IRuleSetAuditStore` — persists lifecycle audit events with optional RSA signature. |
| `RuleSetApprovalService` | Implements `IRuleSetApprovalService` — submit, approve, and reject workflow transitions. |
| `CanaryRolloutService` | Implements `ICanaryRolloutService` — start, promote, and roll back canary deployments. |
| `TenantRlsConnectionInterceptor` | EF Core `DbConnectionInterceptor` that sets the PostgreSQL RLS session variable per tenant. |
| `RuleControlPlaneOptions` | Options record bound from `appsettings.json` section `"RuleControlPlane"`. |

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.RuleEngine.Runtime`](../Muonroi.RuleEngine.Runtime/) — defines `IRuleSetStore`, `IRuleSetAuditStore`, `IRuleSetApprovalService`, `ICanaryRolloutService`, and `RuleControlPlaneOptions` that this package implements and configures.
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — core contracts (`IRule<T>`, `FactBag`, `RuleResult`) consumed at runtime.
- [`Muonroi.RuleEngine.Core`](../Muonroi.RuleEngine.Core/) — in-process orchestrator; use alongside this package when executing rules in the same process.
- [`Muonroi.RuleEngine.Runtime.Web`](../Muonroi.RuleEngine.Runtime.Web/) — REST + SignalR surface for the runtime; registers its own controllers on top of the store bindings this package provides.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
