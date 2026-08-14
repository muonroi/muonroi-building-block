# Muonroi.Quota.Abstractions

> Contracts and base implementations for multi-tenant quota and rate limit tracking.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Quota.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Quota.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Quota.Abstractions` package provides the foundational contracts for enforcing usage limits (quotas) in a multi-tenant application. In SaaS environments, it's critical to control the resources consumed by individual tenantsâ€”whether that's the number of active users, storage size, or the rate of API calls/messages processed.

This package defines the interfaces required to read tenant limits (`ITenantQuotaStore`) and record/check usage (`ITenantQuotaTracker`). It also provides lightweight in-memory implementations suitable for development, testing, or single-node deployments. For distributed production environments, these contracts are typically implemented using distributed caches (like Redis) by higher-level packages.

## Features

- **Standardized Quota Contracts**: `ITenantQuotaTracker` and `ITenantQuotaStore` define a clear boundary for quota management, allowing the underlying storage mechanism to be swapped without affecting business logic.
- **Quota Types**: The `QuotaType` categorization limits (e.g., API calls, Storage, Entities created), providing a unified vocabulary across the system.
- **Exception Models**: Provides the standard `QuotaExceededException`, which can be caught by centralized exception handlers to return appropriate HTTP status codes (e.g., `429 Too Many Requests` or `402 Payment Required`).
- **In-Memory Fallbacks**: Includes thread-safe, `ConcurrentDictionary`-backed implementations (`InMemoryTenantQuotaStore`, `InMemoryTenantQuotaTracker`) for rapid local development.
- **DI Registration**: Easy wiring into the dependency injection container via `TenantQuotaServiceCollectionExtensions`.

## Installation

```bash
dotnet add package Muonroi.Quota.Abstractions
```

## Quick Start

### Basic Configuration

Register the in-memory implementations for local development or testing.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Quota.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Registers InMemoryTenantQuotaStore and InMemoryTenantQuotaTracker
builder.Services.AddInMemoryTenantQuotas();
```

### Checking Quotas in Business Logic

Inject the `ITenantQuotaTracker` into your services to enforce limits before performing actions.

```csharp
using Muonroi.Quota.Abstractions;

public class DocumentService
{
    private readonly ITenantQuotaTracker _quotaTracker;

    public DocumentService(ITenantQuotaTracker quotaTracker)
    {
        _quotaTracker = quotaTracker;
    }

    public async Task CreateDocumentAsync(string tenantId)
    {
        // Attempt to consume the quota. This will throw a QuotaExceededException 
        // if the tenant lacks sufficient allowance.
        await _quotaTracker.ConsumeAsync(tenantId, QuotaType.DocumentsCreated, 1);
        
        // ... proceed
    }
}
```

## API Reference

### Core Contracts
- `ITenantQuotaStore`: Persists and retrieves the maximum *allowable limits* configured for a given tenant.
- `ITenantQuotaTracker`: Tracks *current usage* and increments counts. Defines `ConsumeAsync` and `GetUsageAsync`.

### Models & Exceptions
- `TenantQuota`: A configuration model representing a limit.
- `QuotaUsage`: A state model representing current consumption against the limit.
- `QuotaType`: Identifiers for the resource being consumed.
- `QuotaExceededException`: Thrown by `ITenantQuotaTracker.ConsumeAsync` when a tenant attempts to exceed their limit.

## Ecosystem Combinations

### + Muonroi.Tenancy.Core â†’ Automated Tenant Throttling
By integrating `ITenantQuotaTracker` with `ISystemExecutionContextAccessor`, middleware can automatically intercept incoming HTTP requests, extracting the `TenantId` and aggressively returning `429 Too Many Requests` if the tenant's daily quota limit is reached.

### + Muonroi.Governance.Enterprise â†’ Tier-Based Allowances
Enterprise licenses integrate directly with `ITenantQuotaStore` to sync SaaS plans (e.g., Free vs Premium tiers). As a tenant's license upgrades, the store automatically raises the upper bounds on `QuotaType.DocumentsCreated`.

### Full Quota Stack
```csharp
builder.Services
    .AddInMemoryTenantQuotas();
```

## Samples
- [`Quickstart.Quota.Abstractions`](../../samples/Quickstart.Quota.Abstractions)

## License

Apache 2.0 â€” see [LICENSE-APACHE](../../LICENSE-APACHE).
