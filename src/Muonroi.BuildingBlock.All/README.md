# Muonroi.BuildingBlock.All
> Core primitives for Muonroi.BuildingBlock.All in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BuildingBlock.All.svg)](https://www.nuget.org/packages/Muonroi.BuildingBlock.All/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.BuildingBlock.All is a metapackage that bundles the complete infrastructure setup for a Muonroi application. It references all sub-packages, instantly granting access to caching, billing, messaging, and authorization abstractions.

## Features

- **Unified Dependency**: Single package reference to pull in the entire Muonroi ecosystem baseline.
- **Version Alignment**: Guarantees compatible versions across all Muonroi building block packages.

## Quick Start

```xml
<PackageReference Include="Muonroi.BuildingBlock.All" Version="1.0.0" />
```

## Installation

```bash
dotnet add package Muonroi.BuildingBlock.All
```

## Ecosystem Combinations

Use this metapackage to bootstrap new microservices instantly, ensuring they possess the identical suite of capabilities (like `Muonroi.Auth`, `Muonroi.Bff`, `Muonroi.Billing.Abstractions`) as your existing ecosystem.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.BuildingBlock.All components.
