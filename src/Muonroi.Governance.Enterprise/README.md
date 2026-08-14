# Muonroi.Governance.Enterprise

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Governance.Enterprise.svg)](https://www.nuget.org/packages/Muonroi.Governance.Enterprise/)

> Enterprise-grade governance, anti-tampering, and compliance.

## Overview
Extends standard governance with rigorous features including `AntiTamperDetector`, `MComplianceExportService`, and `EnterpriseControlPlaneService` for mission-critical deployments.

## Features
- **Anti-Tampering**: Protect execution integrity using `AntiTamperDetector` and `CodeIntegrityVerifier`.
- **Compliance**: Generate audit artifacts via `MComplianceEvidencePackService`.
- **Hardware Roots**: Anchor trust with `TpmAnchor` and `ChainSubmitter`.

## Installation

```bash
dotnet add package Muonroi.Governance.Enterprise
```

## Quick Start

```csharp
builder.Services.AddEnterpriseGovernance(options =>
{
    options.EnableAntiTampering = true;
    options.UseTpmAnchor = true;
});

public class AuditTask(IMComplianceExportService exporter)
{
    public async Task Run() => await exporter.ExportEvidenceAsync();
}
```

## Ecosystem Combinations

### Muonroi.Governance.Enterprise + Muonroi.Governance
Upgrades the standard `LicenseGuard` by registering `EnterpriseLicenseGuardEnhancer` to require hardware-backed `FingerprintChainEntry` validation.

## Samples

Find complete running examples in the [../../samples/](../../samples/) directory.

## License

This project is licensed under the terms of the applicable Muonroi license.
