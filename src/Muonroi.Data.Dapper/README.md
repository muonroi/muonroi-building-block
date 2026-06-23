# Muonroi.Data.Dapper

> Dapper integration for Muonroi: lightweight read-side repository, multi-tenant query filtering, and connection factory.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Data.Dapper.svg)](https://www.nuget.org/packages/Muonroi.Data.Dapper/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Data.Dapper` plugs Dapper into the Muonroi multi-tenant stack. It adds a connection-string provider that reads from `IConfiguration`, a command builder (`MDapperCommand`) that bridges Muonroi's query model to Dapper's `CommandDefinition`, custom `SqlMapper` type handlers for Protobuf timestamps and trimmed strings, and an optional Row-Level Security (RLS) override that transparently applies the correct session context before every query or execute call — with no changes to your existing Dapper usage.

## Installation

```bash
dotnet add package Muonroi.Data.Dapper --prerelease
```

## Quick Start

Register the type handlers early in startup, then optionally enable RLS after your provider-specific Dapper registration:

```csharp
using Muonroi.Data.Dapper.Dapper.Handlers;
using Muonroi.Data.Dapper.Rls;

// 1. Register Dapper type handlers globally (process-wide, no DB connection required).
MSqlMapperTypeExtensions.RegisterDapperHandlers();

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 2. Add your provider-specific IDapper (e.g. AddDapperForPostgreSQL from Dapper.Extensions.NetCore).
// ...

// 3. Optionally layer on RLS. This is a no-op when MultiTenantConfigs.EnableRowLevelSecurity
//    is false (the default), so it is safe to call unconditionally.
builder.Services.AddMuonroiDapperRls();

WebApplication app = builder.Build();
app.MapControllers();
app.Run();
```

When RLS is enabled, every `IDapper` call automatically runs `SET app.current_tenant_id = @tid` (PostgreSQL) or `EXEC sp_set_session_context ...` (MSSQL) before executing — no changes to your query code.

For cross-tenant admin operations, wrap with a bypass scope:

```csharp
using (DapperRlsBypass.Enter())
{
    // Dapper queries here run cross-tenant (SET ROLE app_rls_bypass on PostgreSQL).
}
```

## Features

- `MConnectionStringProvider` — reads `<name>:ConnectionString` from `IConfiguration` and implements `IConnectionStringProvider` for Dapper.Extensions.NetCore.
- `MDapperCommand` — command builder (`CommandText`, `Parameters`, `Transaction`, `CommandType`, `CommandFlags`) with a `Build(CancellationToken)` method that produces a `CommandDefinition`.
- `MSqlMapperTypeExtensions.RegisterDapperHandlers()` — registers `MProtobufTimestampHandler` (Google Protobuf `Timestamp`) and `MTrimStringHandler` (auto-trim strings) as global `SqlMapper` type handlers.
- `AddMuonroiDapperRls()` — replaces the live `IDapper` with `TenantRlsDapper<TConn>` when `MultiTenantConfigs.EnableRowLevelSecurity` is `true`; zero-impact early return otherwise (CFG-01).
- `TenantRlsDapper<TConn>` — full override of all 110+ Dapper.Extensions.NetCore overloads; re-applies session context on every call (set-per-open, safe for pooled connections).
- Provider support: PostgreSQL (end-to-end), MSSQL (end-to-end); MySQL deferred.
- `DapperRlsBypass.Enter()` — `AsyncLocal`-backed cross-tenant bypass scope for admin operations; every bypassed connection open is audit-logged.
- `IRlsGuaranteeProvider` — singleton introspection of the active RLS enforcement strength (`Native` for PostgreSQL/MSSQL).
- Startup verifier (`RlsStartupVerifier`) — hosted service that asserts required RLS DDL objects exist on startup; configurable via `VerifyRlsObjectsOnStartup`.
- Strict mode (`StrictMode = true`) — throws `MissingTenantContextException` at query time when no tenant context and no bypass scope are active.

## Configuration

### appsettings.json

```json
{
  "MultiTenantConfigs": {
    "EnableRowLevelSecurity": true,
    "DapperRls": {
      "Provider": "PostgreSql",
      "BypassRoleName": "app_rls_bypass",
      "StrictMode": false,
      "VerifyRlsObjectsOnStartup": true
    }
  }
}
```

### DI registration

```csharp
// Optional delegate overrides config-file values.
builder.Services.AddMuonroiDapperRls(opts =>
{
    opts.Provider = DapperRlsProvider.MsSql;
    opts.StrictMode = true;
});
```

`AddMuonroiDapperRls` must be called **after** your provider-specific `AddDapperForXxx` registration so that the `services.Replace` wins as the last descriptor (last-wins semantics).

### DapperRlsOptions reference

| Property | Default | Description |
|----------|---------|-------------|
| `Provider` | `PostgreSql` | `PostgreSql`, `MsSql`, or `MySql` (MySql deferred, throws at registration) |
| `BypassRoleName` | `"app_rls_bypass"` | PostgreSQL role granted `BYPASSRLS`; used by `DapperRlsBypass.Enter()` |
| `StrictMode` | `false` | Throw `MissingTenantContextException` when tenant id is absent and no bypass is active |
| `VerifyRlsObjectsOnStartup` | `true` | Fail fast at startup if required RLS DDL objects are missing |

## API Reference

| Type | Purpose |
|------|---------|
| `MConnectionStringProvider` | Implements `IConnectionStringProvider`; reads `<name>:ConnectionString` from `IConfiguration` |
| `MDapperCommand` | Command builder; produces a `CommandDefinition` via `Build(CancellationToken)` |
| `MSqlMapperTypeExtensions` | `RegisterDapperHandlers()` — installs Protobuf + trim-string `SqlMapper` type handlers |
| `MProtobufTimestampHandler` | `SqlMapper.TypeHandler<Timestamp>` for Google Protobuf `Timestamp` columns |
| `MTrimStringHandler` | `SqlMapper.TypeHandler<string>` that auto-trims string values |
| `DapperRlsServiceCollectionExtensions` | `AddMuonroiDapperRls(Action<DapperRlsOptions>?)` — the primary DI registration extension |
| `DapperRlsOptions` | Options bound from `MultiTenantConfigs:DapperRls`; see table above |
| `DapperRlsProvider` | Enum: `PostgreSql`, `MsSql`, `MySql` |
| `TenantRlsDapper<TConn>` | `BaseDapper<TConn>` subclass; enforces tenant context before every query/execute |
| `ITenantSessionContextSetter` | Contract for provider-specific session-context setters (`Apply` / `ApplyAsync`) |
| `PostgreSqlTenantSessionContextSetter` | Issues `SET app.current_tenant_id = @tid` (or `SET ROLE <bypass>`) |
| `MsSqlTenantSessionContextSetter` | Issues `EXEC sp_set_session_context @key=N'TenantId', @value=@tid` |
| `DapperRlsBypass` | Static `Enter()` + `IsActive`; `AsyncLocal`-backed cross-tenant bypass scope |
| `IBypassScope` | Disposable scope returned by `DapperRlsBypass.Enter()` |
| `IRlsGuaranteeProvider` | Singleton; exposes `GuaranteeLevel` (resolved from `DapperRlsProvider` at registration) |
| `MissingTenantContextException` | Thrown in strict mode when tenant id is absent and no bypass is active |
| `RlsObjectsMissingException` | Thrown by startup verifier when required RLS DDL objects are not found |

## Samples

- [Quickstart.Data.Dapper](../../samples/Quickstart.Data.Dapper/) — demonstrates `MDapperCommand`, `RegisterDapperHandlers`, `DapperRlsBypass`, and the RLS provider/guarantee model without a live database

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Data.Abstractions`](../Muonroi.Data.Abstractions/) — `IConnectionStringProvider` and core data contracts consumed by this package
- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — `ITenantContext` and `MultiTenantOptions` (including `EnableRowLevelSecurity`) consumed by the RLS layer
- [`Muonroi.Core`](../Muonroi.Core/) — `MGuard`, logging, and exception base types used internally

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
