# Muonroi.RuleEngine.EntityFrameworkCore

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.EntityFrameworkCore.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.EntityFrameworkCore/)

> Entity Framework Core integration for the Muonroi Rule Engine, providing persistence, multi-tenant row-level security, and audit trails.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.EntityFrameworkCore
```

## Overview
This package provides a robust EF Core data layer for the runtime environment. It implements `RuleEngineDbContext` to persist rule sets, templates, and audits. It includes `PostgresRuleSetStore` and `PostgresRuleSetAuditStore` for PostgreSQL implementations, ensuring rule changes are transactional and audited via `RuleSetAuditRecord`.

## Features
- **Database Context**: Use `RuleEngineDbContext` for tracking rule states, assignments, and approvals.
- **Data Stores**: Includes `PostgresRuleSetStore` and `PostgresRuleSetAuditStore` as concrete implementations of `IRuleSetStore`.
- **Row-Level Security**: Secures tenant data automatically using `TenantRlsConnectionInterceptor`.
- **Canary & Approvals**: Manage rule life-cycles through `CanaryRolloutService` and `RuleSetApprovalService`.
- **Tenant Control**: Manage `TenantQuotaOverrideRecord` and `TenantRuleAssignmentRecord` safely.

## Quick Start
```csharp
// Configure EF Core for Rule Engine persistence
builder.Services.AddRuleEngineRuntime(options =>
{
    options.UseEntityFrameworkCore(dbOptions => 
    {
        dbOptions.UseNpgsql(connectionString)
                 .AddInterceptors(new TenantRlsConnectionInterceptor());
    });
});
```

## Ecosystem Combinations

### + Tenancy.Core → Multi-Tenant Databases
Works seamlessly with `TenantContext` to apply Row-Level Security interceptors to PostgreSQL, guaranteeing tenant rule sets are isolated at the database level.

### + Experience Engine → Lineage Tracking
Maintains strict requirement tracking using `RequirementRecord` and `TestLinkRecord` to feed provenance data back to the AI experience brain.

## Samples
- [`Quickstart.RuleEngine`](../../samples/Quickstart.RuleEngine)

## License
Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).



