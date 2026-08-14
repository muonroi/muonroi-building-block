# Muonroi.AuthZ
> Core primitives for Muonroi.AuthZ in the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AuthZ.svg)](https://www.nuget.org/packages/Muonroi.AuthZ/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

Muonroi.AuthZ delivers robust authorization policy enforcement. It supports dynamic rule evaluation via `RuleEngineAuthorizationPolicyEvaluator`, Open Policy Agent (OPA) integration through `OpaAuthorizationService`, and row-level data filtering via `RuleRowFilter`.

## Features

- **Policy Evaluation**: Extend standard authorization with `MuonroiAuthorizationHandler` and `RuleEngineAuthorizationPolicyEvaluator`.
- **External AuthZ**: Integrate seamlessly with OPA using `OpaAuthorizationService`.
- **Dynamic Rules**: Enable hot-reloading of auth rules via `AuthRuleHotReloadClient`.

## Quick Start

```csharp
using Muonroi.AuthZ;

builder.Services.AddAuthZServices();
```

## Installation

```bash
dotnet add package Muonroi.AuthZ
```

## Ecosystem Combinations

Combine with `Muonroi.AspNetCore` to enforce complex policies via `AuthorizePermissionAttribute` directly on controller actions, leveraging dynamic rule updates from `AuthRuleHotReloadClient`.

## Samples

Check out the `../../samples/` directory for full working examples of the Muonroi.AuthZ components.
