# Muonroi.Governance

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Governance.svg)](https://www.nuget.org/packages/Muonroi.Governance/)

> Core policy enforcement and software licensing mechanisms.

## Overview
Implements core governance mechanics including `LicenseGuard`, `MPolicyDecisionService`, and `LicenseVerifier` to protect features and enforce usage policies.

## Features
- **License Protection**: Use `LicenseGuard` to restrict access based on active licenses.
- **Policy Decisions**: Make runtime rules with `MPolicyDecisionService`.
- **Validation**: Background checking via `LicenseConfigurationValidationHostedService`.

## Installation

```bash
dotnet add package Muonroi.Governance
```

## Quick Start

```csharp
builder.Services.AddLicenseServices();
builder.Services.AddPolicyDecisionServices();

public class PremiumFeature(LicenseGuard guard)
{
    public void Execute()
    {
        guard.EnsureActive(LicenseEnums.Premium);
        // Premium logic
    }
}
```

## Ecosystem Combinations

### Muonroi.Governance + Muonroi.Governance.Enterprise
The base `LicenseGuard` here can be enhanced by registering an `EnterpriseLicenseGuardEnhancer` to include anti-tamper checking in enterprise deployments.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
