# Muonroi.Integration.Persistence

> EF Core persistence layer for Muonroi Connector configs and encrypted credentials — drop in one call and your connector store is backed by PostgreSQL with per-tenant RLS and ASP.NET Data Protection encryption.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Integration.Persistence.svg)](https://www.nuget.org/packages/Muonroi.Integration.Persistence/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package provides the concrete EF Core implementations of `IConnectorConfigStore` and `IConnectorCredentialStore` defined in `Muonroi.Integration.Abstractions`. It maps connector metadata to two PostgreSQL tables (`connector_configs`, `connector_credentials`), enforces tenant query filters automatically via `TenantContext`, and encrypts credential values using `IDataProtectionProvider` with a per-tenant key derivation path (`connector-creds:{tenantId}`). The `ConnectorDbContext` is wired to pick up any registered `IInterceptor` singletons (e.g. `TenantRlsConnectionInterceptor`) so RLS `set_config` calls fire before every query.

## Installation

```bash
dotnet add package Muonroi.Integration.Persistence --prerelease
```

## Quick Start

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register EF-backed connector stores targeting PostgreSQL.
// Any IInterceptor registered in DI (e.g. for RLS) is attached automatically.
builder.Services.AddMConnectorPersistence(
    builder.Configuration.GetConnectionString("ConnectorDb")!);

// Data Protection is required for credential encryption.
builder.Services.AddDataProtection();

var app = builder.Build();
 await app.RunAsync();
```

Inject `IConnectorConfigStore` or `IConnectorCredentialStore` wherever you need to read or persist connector data:

```csharp
public class ConnectorService(IConnectorConfigStore configs, IConnectorCredentialStore creds)
{
    public async Task<ConnectorConfigDto?> GetConfigAsync(string id, string tenantId, CancellationToken ct)
        => await configs.GetByIdAsync(id, tenantId, ct);

    public async Task StoreApiKeyAsync(string credId, string tenantId, string apiKey, CancellationToken ct)
        => await creds.SaveAsync(credId, tenantId, new Dictionary<string, string> { ["apiKey"] = apiKey }, ct);
}
```

## Features

- Single-call DI registration via `AddMConnectorPersistence(connectionString)`
- PostgreSQL persistence with `Npgsql.EntityFrameworkCore.PostgreSQL`
- Tenant query filters on both tables — queries are automatically scoped to `TenantContext.CurrentTenantId`
- Credential values encrypted at rest using `IDataProtectionProvider.CreateProtector($"connector-creds:{tenantId}")`
- `IInterceptor` auto-attachment — RLS interceptors registered in DI are wired to `ConnectorDbContext` without extra config
- Composite indexes on `(TenantId, ConnectorType)` and `(TenantId, OwnerId)` for efficient tenant-scoped list queries
- Upsert semantics on both `SaveAsync` methods — creates or updates transparently

## Configuration

`AddMConnectorPersistence` takes a PostgreSQL connection string. There is no separate options class — all behavior is derived from the connection string and DI-registered interceptors.

```csharp
builder.Services.AddMConnectorPersistence(
    builder.Configuration.GetConnectionString("ConnectorDb")!);
```

Ensure `AddDataProtection()` is called before `AddMConnectorPersistence`; the credential store resolves `IDataProtectionProvider` from DI at runtime.

Apply EF migrations from the package's `ConnectorDbContext` to create the two tables:

```bash
dotnet ef migrations add InitConnector --context ConnectorDbContext
dotnet ef database update --context ConnectorDbContext
```

## API Reference

| Type | Purpose |
|------|---------|
| `ConnectorPersistenceRegistration.AddMConnectorPersistence` | Extension method — registers `ConnectorDbContext`, `EfConnectorConfigStore`, and `EfConnectorCredentialStore` |
| `ConnectorDbContext` | EF `DbContext` exposing `ConnectorConfigs` and `ConnectorCredentials` `DbSet`s; applies tenant query filters |
| `EfConnectorConfigStore` | `IConnectorConfigStore` implementation — CRUD for connector configurations stored in `connector_configs` |
| `EfConnectorCredentialStore` | `IConnectorCredentialStore` implementation — encrypted CRUD for credentials stored in `connector_credentials` |
| `ConnectorConfigEntity` | EF entity mapped to `connector_configs`; fields include `ConnectorType`, `ConfigJson` (jsonb), `CredentialId`, `Status`, `OwnerId` |
| `ConnectorCredentialEntity` | EF entity mapped to `connector_credentials`; `EncryptedValues` holds Data-Protection-encrypted JSON |

## Samples

No dedicated sample exists for this package. See the Quick Start above for a minimal registration pattern. For integration patterns with the Muonroi connector pipeline, refer to [`Muonroi.Integration.Abstractions`](../Muonroi.Integration.Abstractions/).

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Integration.Abstractions`](../Muonroi.Integration.Abstractions/) — defines `IConnectorConfigStore`, `IConnectorCredentialStore`, and `ConnectorConfigDto` consumed by this package
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — provides `TenantContext.CurrentTenantId` used for query filters and key derivation
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — shared core contracts referenced by the persistence layer
- [`Muonroi.Logging.Abstractions`](../Muonroi.Logging.Abstractions/) — `IMLog<T>` used for structured logging in `EfConnectorCredentialStore`


## Ecosystem Combinations

### + Integration.Abstractions → Credential Store Implementation
`IConnectorCredentialStore` defined in Abstractions is implemented here with encrypted EF Core storage.

### + Tenancy → Per-Tenant Credential Isolation
Each tenant's connector credentials are stored and retrieved in isolation — tenant A cannot access tenant B's API keys.

### + Governance → License-Gated Connector Count
The number of active connectors a tenant can configure is enforced by the license tier via `ILicenseGuard`.

## Samples
- [`Quickstart.Integration.Persistence`](../../samples/Quickstart.Integration.Persistence)


## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
