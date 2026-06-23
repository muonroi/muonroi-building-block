# Muonroi.Mapper

> Convention-based object mapper for Muonroi services — assembly-scanned registration, compiled expression-tree mapping, and zero-configuration DI wiring.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Mapper.svg)](https://www.nuget.org/packages/Muonroi.Mapper/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Mapper` replaces hand-written mapping boilerplate with a lightweight, reflection-based engine. Mark a DTO with `IMapFrom<TSource>` and call `ConfigureMapper()` once at startup — the library scans the assembly, builds compiled `Expression`-tree mapping actions for every matched property pair, caches them in `MappingConfiguration`, and registers `IMapper → SimpleMapper` in the DI container.

## Installation

```bash
dotnet add package Muonroi.Mapper --prerelease
```

## Quick Start

**1. Mark the DTO**

```csharp
using Muonroi.Core.Abstractions.Interfaces;

public sealed class ProductDto : IMapFrom<Product>
{
    public Guid    Id            { get; set; }
    public string  Name          { get; set; } = string.Empty;
    public decimal Price         { get; set; }
    public int     StockQuantity { get; set; }
}
```

**2. Register at startup**

```csharp
using System.Reflection;
using Muonroi.Mapper.Mapper;

builder.Services.ConfigureMapper(Assembly.GetExecutingAssembly());
```

Omit the `Assembly` parameter to scan every assembly loaded in the current `AppDomain`.

**3. Inject and map**

```csharp
using Muonroi.Mapper.Mapper;

public sealed class MappingController(IMapper mapper) : ControllerBase
{
    [HttpPost("to-dto")]
    public IActionResult ToDto([FromBody] Product product)
    {
        ProductDto dto = mapper.Map<ProductDto>(product);   // new destination instance
        return Ok(dto);
    }

    [HttpPost("onto-existing")]
    public IActionResult OntoExisting([FromBody] Product product)
    {
        ProductDto existing = new();
        ProductDto result = mapper.Map<Product, ProductDto>(product, existing);  // merge onto existing
        return Ok(result);
    }
}
```

## Features

- **Assembly-scan registration** — `ConfigureMapper(params Assembly[])` discovers every type implementing `IMapFrom<T>` and auto-registers both directions (`Source → Destination` and `Destination → Source`).
- **Compiled expression trees** — mapping actions are built once via `System.Linq.Expressions` and cached in a `ConcurrentDictionary`; subsequent calls pay only a delegate invoke.
- **Property-name convention** — writable destination properties are matched by name to readable source properties; type-assignability is checked; unmatched properties are silently skipped.
- **Three mapping overloads** — create a new destination (`Map<TDestination>(source)`), merge onto an existing instance (`Map<TSource, TDestination>(source, destination)`), or use the non-generic `Map(object, object)` overload.
- **Single DI call** — `ConfigureMapper()` registers both `MappingConfiguration` (singleton) and `IMapper → SimpleMapper` (singleton) in one extension method.

## Configuration

There are no appsettings keys or options classes. All configuration is done through the `ConfigureMapper` call:

```csharp
// Scan a specific set of assemblies (recommended for startup performance)
builder.Services.ConfigureMapper(
    Assembly.GetExecutingAssembly(),
    typeof(SomeOtherMarker).Assembly);

// Scan every loaded assembly (convenient; slightly slower at startup)
builder.Services.ConfigureMapper();
```

## API Reference

| Type | Purpose |
|------|---------|
| `IMapper` (`Muonroi.Mapper.Mapper`) | Primary mapping contract — three `Map` overloads |
| `SimpleMapper` | Singleton `IMapper` implementation backed by `MappingConfiguration` |
| `MappingConfiguration` | Thread-safe cache of compiled `Action<object, object>` mapping delegates, keyed by `(Source, Destination)` type pair |
| `MapperServiceCollectionExtensions.ConfigureMapper` | DI registration extension — scans assemblies for `IMapFrom<T>`, populates `MappingConfiguration`, registers `IMapper` |
| `IMapFrom<T>` (`Muonroi.Core.Abstractions`) | Marker interface applied to destination types to declare the source type for auto-discovery |

## Samples

- [Quickstart.Mapper](../../samples/Quickstart.Mapper/) — end-to-end ASP.NET Core API demonstrating `IMapFrom<T>` declaration, `ConfigureMapper` registration, and both `Map` overloads via two controller endpoints

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — defines `IMapFrom<T>` consumed by this package's assembly scanner

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
