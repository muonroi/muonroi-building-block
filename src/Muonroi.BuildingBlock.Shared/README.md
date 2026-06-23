# Muonroi.BuildingBlock.Shared

> Reserved OSS package for shared cross-cutting types (result types, pagination models, and common extensions) used across the Muonroi Building Block ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BuildingBlock.Shared.svg)](https://www.nuget.org/packages/Muonroi.BuildingBlock.Shared/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.BuildingBlock.Shared` is the OSS baseline package reserved for shared primitives — result types, pagination models, and common utilities — that are consumed by the other Muonroi Building Block packages. In the current `1.0.0-alpha.16` release the assembly exports no public types; the package is a versioned placeholder that establishes the package identity and dependency slot while the shared-types surface is being settled.

If you need the full Muonroi infrastructure stack today, reference [`Muonroi.BuildingBlock.All`](../Muonroi.BuildingBlock.All/) (Commercial) or the individual granular packages.

## Installation

```bash
dotnet add package Muonroi.BuildingBlock.Shared --prerelease
```

## Quick Start

Because the assembly currently exports no public types, there is no DI registration or runtime call to make. Add the package reference to reserve the dependency slot:

```xml
<PackageReference Include="Muonroi.BuildingBlock.Shared" Version="1.0.0-alpha.16" />
```

When shared types are promoted into this package in a later alpha, the public API will appear here.

## Features

- Versioned package identity for the OSS shared-types layer
- Zero dependencies — no transitive pull
- Apache-2.0 licensed; safe to reference from OSS libraries

## API Reference

No public types are exported in the current release. The table below will be populated as types are promoted from internal packages into this shared surface.

| Type | Purpose |
|------|---------|
| *(none in 1.0.0-alpha.16)* | — |

## Samples

No dedicated sample exists for this package in the current release.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.BuildingBlock.All`](../Muonroi.BuildingBlock.All/) — Commercial metapackage bundling all OSS and enterprise packages; use when you want the full stack in a single reference
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — Core service contracts (date/time, JSON, execution context)
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — Rule engine contracts and base types

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
