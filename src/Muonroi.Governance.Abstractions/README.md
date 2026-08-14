# Muonroi.Governance.Abstractions

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Governance.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Governance.Abstractions/)

> Interfaces and enums for Muonroi software governance.

## Overview
Defines core contracts such as `ILicenseGuard`, `IMPolicyDecisionService`, and key types like `LicensePolicy` and `ActivationProof` for uniform policy application.

## Features
- **Core Contracts**: `ILicenseGuard` and `IMPolicyDecisionService`.
- **State Models**: Standardized models including `LicenseState` and `LicenseRuntimeStatus`.
- **Extension Points**: `ILicenseGuardEnhancer` to augment protection logic.

## Installation

```bash
dotnet add package Muonroi.Governance.Abstractions
```

## Quick Start

```csharp
public class CustomGuardEnhancer : ILicenseGuardEnhancer
{
    public void EnhanceContext(LicenseActionContext context)
    {
        context.AddMetadata("CustomCheck", true);
    }
}
```

## Ecosystem Combinations

### Muonroi.Governance.Abstractions + Muonroi.Governance
Reference these abstractions in your shared libraries, allowing the host application to inject the concrete `LicenseGuard` from `Muonroi.Governance`.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
