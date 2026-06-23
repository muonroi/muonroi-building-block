# Muonroi.Core

> Foundational runtime services — datetime, JSON serialization, execution context, clock providers, sequential GUIDs, and configuration helpers — shared across all Muonroi applications.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Core.svg)](https://www.nuget.org/packages/Muonroi.Core/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Core` implements the contracts declared in `Muonroi.Core.Abstractions` and wires them into the ASP.NET Core DI container with a single call. It covers the low-level concerns that every service needs before it can do anything useful: an injectable clock, a testable JSON serializer, an ambient execution context that carries tenant/user/correlation data, database-portable sequential GUIDs, cryptography-aware configuration reading, and opinionated Redis and pagination binding. It also pulls in Muonroi structured logging via `Muonroi.Logging`.

## Installation

```bash
dotnet add package Muonroi.Core --prerelease
```

## Quick Start

Call `AddCoreServices` once during startup. The sample below is taken directly from `samples/Quickstart.Core/src/Quickstart.Core.Api/Program.cs`:

```csharp
using Muonroi.Core.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCoreServices(
    builder.Configuration,
    isSecretDefault: true,   // true = config values are read as-is (no decryption)
    secretKey: string.Empty, // decryption key — empty when isSecretDefault is true
    paginationConfigs: null, // null = bound from appsettings with safe defaults
    tokenConfig: null);      // null = bound from appsettings

builder.Services.AddControllers();
WebApplication app = builder.Build();
app.MapControllers();
app.Run();
```

Inject the registered services anywhere:

```csharp
public sealed class CoreDemoController(
    IMDateTimeService dateTime,
    IMJsonSerializeService json,
    ISystemExecutionContextAccessor contextAccessor) : ControllerBase
{
    [HttpGet("now")]
    public IActionResult Now() =>
        Ok(new { now = dateTime.Now(), utcNow = dateTime.UtcNow(), nowTs = dateTime.NowTs() });

    [HttpPost("json-roundtrip")]
    public IActionResult JsonRoundtrip([FromBody] SamplePayload payload)
    {
        string serialized = json.Serialize(payload);
        SamplePayload? result = json.Deserialize<SamplePayload>(serialized);
        return Ok(new { serialized, result });
    }
}
```

## Features

- **Single-call DI registration** — `AddCoreServices()` registers all foundational singletons in one call; `AddRedisConfiguration()` is available separately if you only need Redis binding.
- **Testable clock abstraction** — `IMDateTimeService` (implementation: `MDateTimeService`) exposes `Now()`, `UtcNow()`, `Today()`, `UtcToday()`, `NowTs()`, `UtcNowTs()` so controllers and services never call `DateTime.Now` directly.
- **Pluggable static clock** — `Clock` static class delegates to a swappable `IClockProvider`. Built-in providers: `ClockProviders.Utc`, `ClockProviders.Local`, `ClockProviders.Unspecified`.
- **JSON serialization wrapper** — `IMJsonSerializeService` wraps `System.Text.Json` with `IgnoreCycles` and `WhenWritingDefault` options. `JsonExtensions.Serialize(this object)` provides an extension-method shortcut.
- **Ambient execution context** — `ISystemExecutionContextAccessor` + `ISystemExecutionContext` carry tenant ID, user ID, username, correlation ID, auth token, API key, permissions, and source type without threading them through every method signature.
- **Sequential GUID generation** — `MSequentialGuidGenerator.Instance.Create()` produces time-ordered GUIDs suited for SQL Server (sequential at end), Oracle (sequential as binary), MySQL/PostgreSQL (sequential as string).
- **Configuration helpers** — `IConfiguration.GetOptions<T>(section)`, `ConfigureStartupConfig<TConfig>()`, `ConfigureDictionary<TOptions>()`, and `GetConfigHelper()` / `GetCryptConfigValue()` for transparent AES decryption when `EnableEncryption: true` is set.
- **Pagination config binding** — `MPaginationConfig` is bound from `appsettings.json` and enforces `DefaultPageIndex ≥ 1` and `DefaultPageSize ≥ 15` automatically.
- **String utilities** — `MStringExtension` exposes `NormalizeString()` (accent removal, to-lowercase), `DecryptConfigurationValue()`, and related helpers.
- **Structured logging** — registers Muonroi logging via `AddMuonroiLogging()` (from `Muonroi.Logging`).
- **Problem Details** — registers `AddProblemDetails()` and `AddHttpContextAccessor()` as part of the standard setup.

## Configuration

### `appsettings.json` sections

`AddCoreServices` binds three optional sections. All have safe defaults if the section is absent.

```json
{
  "RedisConfigs": {
    "Host": "localhost",
    "Port": "6379",
    "Password": "",
    "KeyPrefix": "myapp"
  },
  "PaginationConfigs": {
    "DefaultPageIndex": 1,
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  },
  "JwtConfigs": {
    "Issuer": "https://auth.example.com",
    "Audience": "myapp",
    "SecretKey": "..."
  }
}
```

### Encrypted configuration values

Set `EnableEncryption: true` and `SecretKey: <key>` at the configuration root to have `GetConfigHelper()` / `GetCryptConfigValue()` decrypt AES-encrypted values transparently.

### Registering only Redis

```csharp
services.AddRedisConfiguration(configuration, isSecretDefault: true, secretKey: "");
```

### Registering typed pagination config

```csharp
services.AddPaginationConfigs(configuration, new MyPaginationConfig());
```

### Swapping the static clock provider

```csharp
Clock.Provider = ClockProviders.Utc; // or ClockProviders.Local
```

## API Reference

| Type | Purpose |
|------|---------|
| `CoreServiceCollectionExtensions.AddCoreServices()` | Registers all foundational singletons; entry point for DI setup |
| `CoreServiceCollectionExtensions.AddRedisConfiguration()` | Standalone Redis config binding with optional decryption |
| `IMDateTimeService` / `MDateTimeService` | Injectable date/time service: `Now()`, `UtcNow()`, `Today()`, `UtcToday()`, `NowTs()`, `UtcNowTs()` |
| `Clock` | Static clock façade; provider is set via `Clock.Provider` |
| `IClockProvider` | Contract for clock providers; built-ins: `UtcClockProvider`, `LocalClockProvider`, `UnspecifiedClockProvider` |
| `ClockProviders` | Static properties `Utc`, `Local`, `Unspecified` |
| `IMJsonSerializeService` | DI-friendly JSON serializer/deserializer |
| `JsonExtensions.Serialize()` | `object.Serialize()` extension using shared `JsonSerializerOptions` |
| `ISystemExecutionContextAccessor` / `SystemExecutionContextAccessor` | Ambient context carrier: `Set(ctx)`, `Get()`, `Clear()` |
| `IContextResolver` / `NullContextResolver` | Default no-op resolver; replace to resolve context from HTTP or messaging headers |
| `ITenantContextPolicy` / `DefaultTenantContextPolicy` | Policy for extracting tenant identity from context |
| `MSequentialGuidGenerator` | Sequential GUID factory; `Instance.Create()` targets SQL Server by default |
| `MConfigurationExtension.GetOptions<T>()` | Binds a config section into a new `T` |
| `MConfigurationExtension.GetConfigHelper()` | Reads a config value, decrypting it if `EnableEncryption` is set |
| `MConfigurationExtension.GetCryptConfigValue()` | Low-level encrypted value reader |
| `MPaginationConfig` | Pagination settings model bound from `PaginationConfigs` section |
| `MPaginationExtensions.AddPaginationConfigs<TPaging>()` | Public extension for registering custom pagination config types |
| `MStringExtension.NormalizeString()` | Accent-strips and lowercases a string (useful for search normalization) |

## Samples

- [Quickstart.Core](../../samples/Quickstart.Core/) — Minimal ASP.NET Core API demonstrating `IMDateTimeService`, `IMJsonSerializeService`, and `ISystemExecutionContextAccessor` via three controller endpoints.

## Compatibility

- Target framework: `net8.0`
- Requires: `Microsoft.AspNetCore.App` framework reference
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — Contracts (`IMDateTimeService`, `IMJsonSerializeService`, `ISystemExecutionContextAccessor`, `IClockProvider`, `IGuidGenerator`, guards, and common models) that this package implements. Depend on Abstractions alone when authoring library code that must not take a runtime dependency.
- [`Muonroi.Logging`](../Muonroi.Logging/) — Muonroi structured logging provider wired in by `AddCoreServices`.
- [`Muonroi.AspNetCore`](../Muonroi.AspNetCore/) — HTTP middleware and ASP.NET Core extensions that build on the execution context registered here.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) at the repository root.
