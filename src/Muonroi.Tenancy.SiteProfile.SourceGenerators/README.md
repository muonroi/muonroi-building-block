# Muonroi.Tenancy.SiteProfile.SourceGenerators

> Roslyn source generators and analyzers that make multi-site `ISiteProfile` registration AOT-safe and type-checked at compile time.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Tenancy.SiteProfile.SourceGenerators.svg)](https://www.nuget.org/packages/Muonroi.Tenancy.SiteProfile.SourceGenerators/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is an analyzer/source-generator-only assembly (targets `netstandard2.0`, placed under `analyzers/dotnet/cs`). It ships four incremental source generators and eight diagnostic analyzers for the `Muonroi.Tenancy.SiteProfile` ecosystem. There is no runtime API — all behavior is compile-time code generation and static analysis.

## Installation

```bash
dotnet add package Muonroi.Tenancy.SiteProfile.SourceGenerators --prerelease
```

The package is resolved as a Roslyn analyzer/generator reference. No `<PackageReference>` with `OutputItemType="Analyzer"` is required — the NuGet metadata handles placement automatically.

## Quick Start

### 1. Declare a site profile with `[GenerateSiteProfile]`

```csharp
using Muonroi.Tenancy.SiteProfile;

[GenerateSiteProfile("TCI", typeof(TciDbContext))]
[SiteProfileBehavior(typeof(TciCacheBehavior))]
public partial class TciSiteProfile : ISiteProfile
{
    public string SiteId => "TCI";

    // Generated partial void RegisterAdditionalServices(...) is your extension hook.
    // The generator emits RegisterServices(...) — do not write it manually.
    partial void RegisterAdditionalServices(
        IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedScoped<IOrderService, TciOrderService>("TCI");
    }
}
```

The generator emits `TciSiteProfile.RegisterServices.g.cs` containing a `RegisterServices(IServiceCollection, IConfiguration)` implementation that calls `SiteProfileBootstrap.RegisterSiteServices(...)` and then invokes your `RegisterAdditionalServices` hook.

### 2. Register all discovered profiles (AOT-safe, no reflection)

```csharp
// Program.cs — replaces reflection-based AddMultiSiteProfiles(Assembly[])
builder.Services.AddGeneratedSiteProfiles(
    builder.Configuration,
    siteCodeAccessor: sp => sp.GetRequiredService<ISiteProfileResolver>().CurrentSiteId);
```

`AddGeneratedSiteProfiles` is emitted by `SiteProfileRegistrationGenerator` into `SiteProfileRegistrationExtensions.g.cs`. It instantiates each discovered `ISiteProfile` via `new TProfile()` — no `Activator.CreateInstance`.

### 3. Use the generated `SiteIds` constants

```csharp
// Instead of: services.AddKeyedScoped<IOrderService, TciOrderService>("TCI");
// Use:
services.AddKeyedScoped<IOrderService, TciOrderService>(SiteIds.TCI);
```

Analyzer `MSP001` warns whenever a raw string literal matches a known `SiteId` value but `SiteIds.<NAME>` is not used.

## Features

- **`SiteProfileRegistrationGenerator`** — scans all `ISiteProfile` implementations and emits:
  - `SiteProfileRegistrationExtensions.g.cs` with `AddGeneratedSiteProfiles(IServiceCollection, IConfiguration, Func<IServiceProvider, string?>)` — AOT-safe alternative to reflection-based registration
  - `SiteIds.g.cs` with `internal static class SiteIds` containing one `const string` per discovered profile
  - `SiteDbContextTypeRegistry.g.cs` with `GetAllSiteDbContextTypes()` — AOT-safe migration runner support
  - `SiteGrpcServiceRegistry.g.cs` with `GetAllSiteGrpcServices()` — discovers `[SiteGrpcService]` from both local source and referenced assemblies
  - `SiteEntityTypeRegistry.g.cs` with `GetAllSiteEntityTypes()` — entity hierarchy mappings from `[SiteEntityMap]`
- **`SiteProfileScaffoldingGenerator`** — for each class decorated with `[GenerateSiteProfile("siteId", typeof(DbContext))]`, emits a per-class partial `RegisterServices(...)` implementation plus an optional `ApplyEntityHierarchy(ModelBuilder)` method when `[SiteEntityMap]` attributes are present
- **`SiteProfileAliasGenerator`** — for classes decorated with `[SiteProfileAlias("TARGET")]`, emits a `RegisterAliasServices(IServiceCollection)` partial method that forwards keyed service registrations from the target site; enforces `MSP030`/`MSP031`
- **`SiteGrpcFacadeGenerator`** — for partial interfaces decorated with `[GenerateSiteGrpcFacade(SharedClient = typeof(X))]`, emits a partial interface completion with async RPC signatures plus a concrete `{Name}Facade` class that delegates each call to the correct inner gRPC client

## Diagnostics

| ID | Severity | Title |
|----|----------|-------|
| `MSP001` | Warning | Use `SiteIds` constant instead of a string literal for a known `SiteId` value |
| `MSP002` | Warning | Site `ISiteProfile` class is missing a keyed DI registration for a type registered via `AddSiteResolvedService<T>()` |
| `MSP010` | Info | `[GenerateSiteProfile]`-decorated class is not `partial` — add `partial` to enable scaffolding |
| `MSP020` | Warning | Duplicate proto message name clashes with a shared assembly message |
| `MSP021` | Warning | gRPC service class derives from a gRPC base but is missing `[SiteGrpcService("SITE_ID")]` |
| `MSP022` | Warning | `[SiteGrpcService]`-decorated type is absent from the generated `SiteGrpcServiceRegistry` — rebuild required |
| `MSP023` | Info | Site gRPC service class could inherit from a shared service to reuse RPC implementations |
| `MSP030` | Warning | `[SiteProfileAlias]` used together with `[SiteProfileBehavior]` — behaviors may not apply as expected |
| `MSP031` | Error | `[SiteProfileAlias]` requires `[GenerateSiteProfile]` on the same class |
| `MSP040` | Warning | `ISiteColumnMap` is missing a `Column()` mapping for an entity property |
| `MSP041` | Info | `ISiteProfile` in a referenced assembly may not be discovered — add its assembly to `SiteAssemblies` |

## API Reference

This package has no runtime API. All public surface is Roslyn generator/analyzer infrastructure.

| Type | Purpose |
|------|---------|
| `SiteProfileRegistrationGenerator` | `IIncrementalGenerator` — scans `ISiteProfile` impls; emits `AddGeneratedSiteProfiles`, `SiteIds`, `SiteDbContextTypeRegistry`, `SiteGrpcServiceRegistry`, `SiteEntityTypeRegistry` |
| `SiteProfileScaffoldingGenerator` | `IIncrementalGenerator` — emits per-class `RegisterServices` partial and `ApplyEntityHierarchy` from `[GenerateSiteProfile]` + `[SiteEntityMap]` |
| `SiteProfileAliasGenerator` | `IIncrementalGenerator` — emits `RegisterAliasServices` partial from `[SiteProfileAlias]`; enforces MSP030/MSP031 |
| `SiteGrpcFacadeGenerator` | `IIncrementalGenerator` — emits gRPC facade interface completion and concrete facade class from `[GenerateSiteGrpcFacade]` |
| `SiteIdLiteralAnalyzer` | `DiagnosticAnalyzer` — MSP001 |
| `ContractComplianceAnalyzer` | `DiagnosticAnalyzer` — MSP002 |
| `SiteProfileScaffoldingGenerator` (diagnostic path) | MSP010 |
| `DuplicateProtoMessageAnalyzer` | `DiagnosticAnalyzer` — MSP020 |
| `MissingSiteGrpcServiceAttributeAnalyzer` | `DiagnosticAnalyzer` — MSP021 |
| `SiteGrpcServiceRegistryAnalyzer` | `DiagnosticAnalyzer` — MSP022 |
| `InheritanceHintAnalyzer` | `DiagnosticAnalyzer` — MSP023 |
| `SiteProfileAliasGenerator` (diagnostic path) | MSP030, MSP031 |
| `ColumnMapDriftAnalyzer` | `DiagnosticAnalyzer` — MSP040 |
| `AssemblyIsolationHintAnalyzer` | `DiagnosticAnalyzer` — MSP041 |

## Samples

No dedicated sample project is provided for this package. The generated extension methods (`AddGeneratedSiteProfiles`, `SiteIds`, `RegisterAdditionalServices`) are exercised in the host project of any service that references `Muonroi.Tenancy.SiteProfile`.

## Compatibility

- Target framework: `netstandard2.0`
- Roslyn component (`<IsRoslynComponent>true</IsRoslynComponent>`)
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Tenancy.SiteProfile`](../Muonroi.Tenancy.SiteProfile/) — defines `ISiteProfile`, `ISiteProfileResolver`, `AddSiteResolvedService<T>()`, and `AddMultiSiteProfiles()` — required runtime counterpart
- [`Muonroi.Tenancy.SiteProfile.Generated.Runtime`](../Muonroi.Tenancy.SiteProfile.Generated.Runtime/) — provides `SiteProfileBootstrap.RegisterSiteServices(...)` and `SiteProfileManifestRunner.Register(...)` called by the generated code
- [`Muonroi.Tenancy.SiteProfile.Web`](../Muonroi.Tenancy.SiteProfile.Web/) — adds `AddSiteProfileWeb()`, per-request middleware, EF Core / Dapper per-site DI helpers


## Ecosystem Combinations

### + SiteProfile → Generated Registration Code
Source generators scan for `ISiteProfile` implementations and emit partial classes with `RegisterSiteServices()` calls — replacing the entire manual `AddSiteProfile<T>()` boilerplate.

### + SiteProfile.Web → Generated Middleware Wiring
Generated code includes the middleware registration order for `SiteProfileStateMiddleware` — no manual `app.UseSiteProfile()` calls needed.

### + Data.EntityFrameworkCore → Generated DbContext Registration
For each `ISiteProfile`, the generator emits code to register `AddSiteDbContext<T>()` with the correct connection string from the site profile.

## Samples
- [`Quickstart.Tenancy.SiteProfile`](../../samples/Quickstart.Tenancy.SiteProfile)


## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
