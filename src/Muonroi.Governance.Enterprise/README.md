# Muonroi.Governance.Enterprise

> Enterprise-grade license enforcement, anti-tamper detection, audit-chain signing, compliance export, and control-plane management for the Muonroi platform.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Governance.Enterprise.svg)](https://www.nuget.org/packages/Muonroi.Governance.Enterprise/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](../../LICENSE-COMMERCIAL)

This package upgrades the OSS governance pipeline (`Muonroi.Governance`) to a production-hardened implementation. It replaces the default no-op services with:

- Hardware-bound machine fingerprinting (`FingerprintProvider`) and HMAC chain signing (`HmacFingerprintSigner`)
- Anti-tamper detection including hardware breakpoint scanning (`AntiTamperDetector`) and code-integrity verification (`CodeIntegrityVerifier`)
- Fail-closed policy enforcement with RSA-verified policy bundles (`PolicyEnforcer`, `EnterpriseLicenseGuardEnhancer`)
- Server-side audit-chain submission with durable retry (`ChainSubmitter`, `ChainSubmissionHostedService`, `FileFailedChainSubmissionStore`)
- Compliance export and tamper-evident evidence packs (`IMComplianceExportService`, `IMComplianceEvidencePackService`)
- Enterprise control-plane operations: license issuance/revocation, policy draft/approve/activate/rollback with RSA signatures (`IMEnterpriseControlPlaneService`)
- Upgrade compatibility analysis and SLO preset management (`IMUpgradeCompatibilityService`, `IMEnterpriseSloPresetService`)
- OpenTelemetry tracing and metrics for anti-tamper and audit trail activity sources

## Installation

```bash
dotnet add package Muonroi.Governance.Enterprise --prerelease
```

## Quick Start

```csharp
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Helpers;
using Muonroi.Governance.Enterprise;
using Muonroi.Governance.Operations;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Prerequisites required by the governance pipeline
builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
builder.Services.AddSingleton<IMDateTimeService, MDateTimeService>();
builder.Services.AddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>();
builder.Services.AddHttpClient(); // required by LicenseActivator

// Register enterprise governance — calls AddLicenseProtection() internally,
// then upgrades OSS services and registers operations + compliance services.
builder.Services.AddMEnterpriseGovernance(builder.Configuration);

builder.Services.AddControllers();

WebApplication app = builder.Build();

// Exposes enterprise operations endpoints:
//   POST /api/v1/enterprise-ops/upgrade/compatibility/check
//   GET  /api/v1/enterprise-ops/slo/presets
//   GET  /api/v1/enterprise-ops/slo/presets/{presetName}
app.MapMEnterpriseOperationsEndpoints();

app.Run();
```

### appsettings.json

```json
{
  "LicenseConfigs": {
    "Mode": "Offline",
    "LicenseFilePath": "licenses/license.lic",
    "PublicKeyPath": "licenses/public.pem",
    "EnableAntiTampering": true,
    "EnableChain": true,
    "ChainStorage": "File",
    "ChainFilePath": "licenses/chain.json",
    "FailMode": "Hard",
    "EnableServerValidation": false,
    "Compliance": {
      "Enabled": true,
      "EnableBackgroundExport": true,
      "ExportRootPath": "logs/compliance",
      "ExportIntervalMinutes": 15,
      "EvidencePackRetentionDays": 365
    }
  }
}
```

## Features

- **Enterprise license guard**: replaces the OSS `ILicenseGuardEnhancer` with `EnterpriseLicenseGuardEnhancer`, which combines anti-tamper checks, policy enforcement, and tenant-context resolution
- **Hardware fingerprinting**: `FingerprintProvider` derives a machine-and-project bound fingerprint; `HmacFingerprintSigner` signs the audit chain with the license payload key
- **Anti-tamper detection**: `AntiTamperDetector` scans for hardware breakpoints (DR0–DR3 on x64) and debugger instrumentation; `CodeIntegrityVerifier` validates assembly checksums
- **TPM anchoring**: `TpmAnchor` uses Windows DPAPI or TPM to bind license files to the local machine (opt-in via `EnableTpmAnchoring`)
- **Fail-closed policy enforcement**: `PolicyEnforcer` loads and RSA-verifies a signed policy bundle at startup; unverifiable or missing policies disable enforcement rather than silently pass
- **Audit-chain submission**: `ChainSubmitter` batches and submits action-chain entries to the license server; `FileFailedChainSubmissionStore` provides durable retry for submissions that fail
- **Nonce rotation**: `NonceRotator` rotates chain-signing nonces on a schedule to limit replay exposure
- **License heartbeat**: `LicenseHeartbeatService` (hosted service) keeps the online license current when `Mode=Online` and `EnableHeartbeat=true`
- **Compliance export**: `IMComplianceExportService` appends audit records to an NDJSON append-only log and verifies log integrity; `MComplianceExportHostedService` runs export on a configurable interval
- **Evidence packs**: `IMComplianceEvidencePackService` generates RSA-signed, tamper-evident evidence pack files from compliance records and verifies them on load
- **Control plane**: `IMEnterpriseControlPlaneService` issues, revokes, and tenant-assigns licenses; manages policy-bundle lifecycle (draft → approve → activate → rollback) with RSA chain-of-custody signatures
- **Upgrade compatibility**: `IMUpgradeCompatibilityService` evaluates whether a target version upgrade is compatible given the current license configuration
- **SLO presets**: `IMEnterpriseSloPresetService` provides named SLO threshold presets for module-level alerting
- **OpenTelemetry integration**: automatically registers `AntiTamperingRuntimeTelemetry` and `AuditTrailRuntimeTelemetry` activity sources and meters

## Configuration

`AddMEnterpriseGovernance(IConfiguration)` reads from the `LicenseConfigs` section (bound to `LicenseConfigs`).

Key options relevant to enterprise behavior:

| Option | Type | Default | Purpose |
|--------|------|---------|---------|
| `LicenseConfigs:Mode` | `LicenseMode` | `Offline` | `Offline` or `Online` |
| `LicenseConfigs:EnableAntiTampering` | `bool` | `false` | Activate anti-tamper runtime checks |
| `LicenseConfigs:EnableHardwareBreakpointDetection` | `bool` | `false` | DR0–DR3 hardware breakpoint scan |
| `LicenseConfigs:EnableChain` | `bool` | `false` | Enable action-chain audit trail |
| `LicenseConfigs:EnableServerValidation` | `bool` | `false` | Submit chains to the license server |
| `LicenseConfigs:EnableTpmAnchoring` | `bool` | `false` | DPAPI/TPM machine-binding |
| `LicenseConfigs:FailMode` | `LicenseFailMode` | `Soft` | `Soft` (log) or `Hard` (throw) on failure |
| `LicenseConfigs:Online:EnableHeartbeat` | `bool` | `false` | License heartbeat (requires `Mode=Online`) |
| `LicenseConfigs:Compliance:Enabled` | `bool` | `false` | Activate compliance export pipeline |
| `LicenseConfigs:Compliance:EnableBackgroundExport` | `bool` | `false` | Start background export hosted service |
| `LicenseConfigs:Compliance:ExportIntervalMinutes` | `int` | `15` | Background export cadence |
| `LicenseConfigs:Compliance:EvidencePackRetentionDays` | `int` | `365` | Evidence-pack pruning window |

### Control Plane (separate registration)

```csharp
using Muonroi.Governance.ControlPlane;
using System.Security.Cryptography;

RSA rsa = RSA.Create();
// load rsa from your enterprise RSA private key...

services.AddMEnterpriseControlPlane(
    registryPath: "licenses/control-plane-registry.json",
    signer: new MRsaControlPlaneSigner(rsa, keyId: "my-cp-key"));
```

## API Reference

| Type | Purpose |
|------|---------|
| `EnterpriseGovernanceServiceExtensions.AddMEnterpriseGovernance` | DI registration; upgrades OSS services and wires enterprise features |
| `MControlPlaneServiceCollectionExtensions.AddMEnterpriseControlPlane` | Registers control-plane store, signer, and service |
| `EnterpriseLicenseGuardEnhancer` | Enterprise `ILicenseGuardEnhancer`: anti-tamper + policy enforcement |
| `AntiTamperDetector` | Hardware breakpoint and process-level tamper detection |
| `CodeIntegrityVerifier` | Assembly checksum validation |
| `PolicyEnforcer` | Verifies and enforces signed `LicensePolicy` bundles |
| `FingerprintProvider` | Machine-and-project hardware fingerprint |
| `HmacFingerprintSigner` | HMAC signing of fingerprint chain entries |
| `FileFingerprintChainStore` | File-backed audit chain persistence |
| `TpmAnchor` | Windows DPAPI / TPM machine-binding |
| `ChainSubmitter` | Batched license-server audit-chain submission with retry |
| `FileFailedChainSubmissionStore` (`IFailedChainSubmissionStore`) | Durable store for failed chain submissions |
| `NonceRotator` | Rotating signing nonce for the chain |
| `LicenseActivator` | Online license activation, JWT + proof file persistence |
| `LicenseHeartbeatService` | Hosted service: periodic online license heartbeat |
| `ChainSubmissionHostedService` | Hosted service: periodic chain submission |
| `IMComplianceExportService` | Export audit records to NDJSON; verify log integrity |
| `MComplianceExportService` | Implementation of `IMComplianceExportService` |
| `MComplianceExportHostedService` | Hosted service: background compliance export |
| `IMComplianceEvidencePackService` | Generate and verify RSA-signed evidence packs |
| `MComplianceEvidencePackService` | Implementation of `IMComplianceEvidencePackService` |
| `IMEnterpriseControlPlaneService` | License issuance/revocation, policy lifecycle, audit trail |
| `MEnterpriseControlPlaneService` | Implementation of `IMEnterpriseControlPlaneService` |
| `MRsaControlPlaneSigner` (`IMControlPlaneSigner`) | RSA signing/verification for control-plane records |
| `MFileControlPlaneStore` (`IMControlPlaneStore`) | File-backed control-plane registry persistence |
| `IMUpgradeCompatibilityService` | Evaluates version-upgrade compatibility |
| `IMEnterpriseSloPresetService` | Named SLO threshold preset lookup |
| `GovernanceTelemetryDescriptor` | `ITelemetryDescriptor` for governance OTel registration |

## Samples

- [Quickstart.Governance.Enterprise](../../samples/Quickstart.Governance.Enterprise/) — ASP.NET Core API demonstrating `AddMEnterpriseGovernance`, `ILicenseGuard` tier/feature enforcement, and `MapMEnterpriseOperationsEndpoints`

## Compatibility

- Target framework: `net8.0`
- Requires: `Microsoft.AspNetCore.App` framework reference
- License: Commercial — requires activation. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).

## Related Packages

- [`Muonroi.Governance`](../Muonroi.Governance/) — OSS base: license loading, offline validation, policy store, basic fingerprinting
- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — Shared contracts: `LicenseConfigs`, `ILicenseGuard`, `IFingerprintChainStore`, `IPolicyStore`
- [`Muonroi.Core`](../Muonroi.Core/) — Core services: `IMJsonSerializeService`, `IMDateTimeService`
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — Tenant context resolution used by the enterprise guard enhancer

## License

This package requires a commercial license. Contact [Muonroi](https://muonroi.com) for licensing terms. See [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL).
