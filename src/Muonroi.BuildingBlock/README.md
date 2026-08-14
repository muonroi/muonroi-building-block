# Muonroi.BuildingBlock
> Core primitives for Muonroi.BuildingBlock in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BuildingBlock.svg)](https://www.nuget.org/packages/Muonroi.BuildingBlock/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.BuildingBlock is a foundational package that ties together various ecosystem primitives. It includes utilities such as `CodeIntegrityVerifier` to ensure consistent security across dependent assemblies.

## Features

- **Code Validation**: Verify assembly signatures and metadata using `CodeIntegrityVerifier`.
- **Core Primitives**: Baseline components relied on by other layers of the Muonroi ecosystem.

## Quick Start

```csharp
using Muonroi.BuildingBlock;

var isValid = CodeIntegrityVerifier.VerifyAssembly();
```

## Installation

```bash
dotnet add package Muonroi.BuildingBlock
```

## Ecosystem Combinations

Combine with `Muonroi.BuildingBlock.All` to automatically include all essential foundational configurations and start utilizing standard validation tooling.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.BuildingBlock components.
