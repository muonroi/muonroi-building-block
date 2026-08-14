# Muonroi.AspNetCore.RuleEngine
> Core primitives for Muonroi.AspNetCore.RuleEngine in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AspNetCore.RuleEngine.svg)](https://www.nuget.org/packages/Muonroi.AspNetCore.RuleEngine/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.AspNetCore.RuleEngine integrates rule-based routing and CRUD operations into the ASP.NET Core pipeline. It offers dynamic endpoints via `MGenericController` and rule evaluation utilities like `CrudRuleExtensions`.

## Features

- **Generic Controllers**: Dynamically generate routes using `MGenericController` and `GenericControllerRouteConvention`.
- **Rule Management**: Handle rule state changes in-memory via `InMemoryRuleChangeStore`.
- **UI Engine Integration**: Expose UI configuration changes with `UiEngineChangesController`.

## Quick Start

```csharp
using Muonroi.AspNetCore.RuleEngine;

builder.Services.AddRuleEngineInfrastructure();
```

## Installation

```bash
dotnet add package Muonroi.AspNetCore.RuleEngine
```

## Ecosystem Combinations

Combine with `Muonroi.AuthZ` to secure dynamically generated rule-based endpoints, ensuring that auto-generated CRUD routes enforce row-level access policies via `RuleRowFilter`.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.AspNetCore.RuleEngine components.
