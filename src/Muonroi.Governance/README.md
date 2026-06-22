# Muonroi.Governance

> OSS license protection for the Muonroi open-core stack: tier resolution, feature gating, action-chain audit, and policy enforcement — all in one call.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Governance.svg)](https://www.nuget.org/packages/Muonroi.Governance/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.Governance` implements the OSS (Free tier) license pipeline. It loads and
verifies a license payload (offline or online), resolves the effective `LicenseTier`,
and exposes `ILicenseGuard` as a scoped service. Applications that need no paid
license run transparently in Free tier with no configuration required. Premium
enforcement (anti-tampering, HMAC action chains, enterprise secure-defaults) is
layered on by `Muonroi.Governance.Enterprise` without changing any registration
call.

## Installation

```bash
dotnet add package Muonroi.Governance --prerelease
```

## Quick Start

```csharp
// Program.cs
using Muonroi.Governance.License;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Required helpers (or supply via Muonroi.Core's AddCoreServices())
builder.Services.AddSingleton<IMJsonSerializeService, MJsonSerializeService>();
builder.Services.AddSingleton<IMDateTimeService, MDateTimeService>();

// Register OSS license protection — binds "LicenseConfigs" config section.
// With no license file the app resolves to Free tier automatically.
builder.Services.AddLicenseProtection(builder.Configuration);

builder.Services.AddControllers();
WebApplication app = builder.Build();

// Exposes GET /api/v1/license/info (tier + activation JWT for frontend).
app.MapMuonroiLicenseInfoEndpoint();

app.MapControllers();
app.Run();
```

Inject `ILicenseGuard` in any controller or service:

```csharp
public class MyService(ILicenseGuard guard)
{
    public void DoWork()
    {
        // Non-throwing probe
        if (!guard.HasFeature("multi-tenant"))
        {
            // run free-tier path
            return;
        }

        // Throwing enforcement — throws MInternalException when feature absent
        guard.EnsureFeature("rule-engine");

        // Action-level validation (also records chain entry when EnableChain = true)
        guard.EnsureValid("export.pdf");
    }
}
```

## Features

- **Offline and online license verification** — loads a local license file and/or validates against a remote endpoint. Falls back to Free tier when neither is present.
- **Tier-aware feature gating** — `ILicenseGuard.HasFeature()` (non-throwing) and `EnsureFeature()` (throwing) enforce capabilities declared in the license payload.
- **Action-level enforcement** — `EnsureValid(actionType)` validates and optionally records the action to the audit chain.
- **Audit action chain** — when `EnableChain = true`, `RecordAction()` appends HMAC-signed entries per tenant partition (no-op `IFingerprintChainStore` / `IFingerprintSigner` in OSS; real implementations provided by `Muonroi.Governance.Enterprise`).
- **License info endpoint** — `MapMuonroiLicenseInfoEndpoint()` maps `GET /api/v1/license/info` returning tier, validity, and an RS256 activation JWT for frontend consumers.
- **Policy file verification** — `FilePolicyStore` + `PolicyVerifier` validate a signed JSON policy file when `RequireSignedPolicy = true`.
- **Soft/Hard fail modes** — `FailMode = Soft` logs license failures without throwing (developer-friendly default); `Hard` throws on any invalid state.
- **Automatic online refresh** — when `Mode = Online` and an endpoint is configured, a `LicenseRefreshHostedService` periodically re-validates the license in the background.
- **Centralized PDP integration** — `AddMPolicyDecision()` connects to an OPA or OpenFGA policy decision point for authorization decisions alongside the license layer.

## Configuration

### `AddLicenseProtection`

Bound from the `"LicenseConfigs"` section:

```json
{
  "LicenseConfigs": {
    "Mode": "Offline",
    "FailMode": "Soft",
    "LicenseFilePath": "licenses/license.json",
    "PublicKeyPath": "licenses/public.pem",
    "ActivationProofPath": "licenses/activation_proof.json",
    "ActivationJwtPath": "licenses/activation_jwt.txt",
    "FallbackToOnlineActivation": true,
    "FingerprintScope": "MachineAndProject",
    "ProjectSeed": "<your-seed>",
    "EnableChain": false,
    "EnableAntiTampering": false,
    "FailMode": "Soft",
    "RequireSignedPolicy": false,
    "Online": {
      "Endpoint": "https://license.muonroi.com",
      "TimeoutSeconds": 10,
      "RefreshMinutes": 1440
    }
  }
}
```

Key `LicenseConfigs` properties:

| Property | Default | Description |
|----------|---------|-------------|
| `Mode` | `Offline` | `Offline` or `Online` |
| `FailMode` | `Soft` | `Soft` = log only; `Hard` = throw |
| `LicenseFilePath` | `null` | Path to signed license JSON |
| `FingerprintScope` | `MachineAndProject` | `MachineAndProject` or `ProjectOnly` |
| `EnableChain` | `false` | Enable HMAC action-chain audit |
| `EnableAntiTampering` | `false` | Enable anti-tampering checks (Enterprise) |
| `RequireSignedPolicy` | `false` | Reject startup if policy file absent/invalid |
| `FallbackToOnlineActivation` | `true` | Auto-activate online when no proof found |

### `AddMPolicyDecision`

Optional. Binds from the `"MPolicyDecision"` section:

```json
{
  "MPolicyDecision": {
    "Enabled": true,
    "Provider": "Opa",
    "Endpoint": "http://opa-service:8181",
    "TimeoutSeconds": 5,
    "FailureMode": "FallbackToLocal",
    "EnableDecisionLogging": true
  }
}
```

## API Reference

| Type | Purpose |
|------|---------|
| `ILicenseGuard` | Primary guard: `Tier`, `IsFreeMode`, `HasFeature()`, `EnsureFeature()`, `EnsureValid()`, `RecordAction()` |
| `LicenseState` | Resolved license snapshot: `IsValid`, `IsExpired`, `TrustedTier`, `Features`, `OrganizationName` |
| `LicenseTier` | Enum: `Free = 0`, `Licensed = 1`, `Enterprise = 2` |
| `LicenseConfigs` | Strongly typed config; section name `"LicenseConfigs"` |
| `ITenantLicenseFeatureGate` | Per-tenant feature gate adapter (scoped) |
| `ILicenseStore` | Loads/saves license payload and activation proof |
| `ILicenseActivationService` | Activates a license key against the license server |
| `IMPolicyDecisionService` | Evaluates authorization decisions via OPA or OpenFGA |
| `MPolicyDecisionConfigs` | Config for PDP integration; section name `"MPolicyDecision"` |
| `LicenseInfoEndpointExtensions` | `MapMuonroiLicenseInfoEndpoint()` — serves tier + JWT to frontend |

## Samples

- [Quickstart.Governance](../../samples/Quickstart.Governance/) — minimal ASP.NET Core API demonstrating `AddLicenseProtection`, `ILicenseGuard` feature/action enforcement, and `MapMuonroiLicenseInfoEndpoint`
- [Quickstart.Governance.Enterprise](../../samples/Quickstart.Governance.Enterprise/) — same API with `Muonroi.Governance.Enterprise` layered on for anti-tampering and HMAC chain

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Governance.Abstractions`](../Muonroi.Governance.Abstractions/) — contracts (`ILicenseGuard`, `LicensePayload`, `LicenseConfigs`, `ILicenseStore`, etc.) consumed by this package and by Enterprise
- [`Muonroi.Governance.Enterprise`](../Muonroi.Governance.Enterprise/) — adds anti-tampering, HMAC chain signing, compliance export, and enterprise secure-defaults on top of this package
- [`Muonroi.Core`](../Muonroi.Core/) — provides `IMJsonSerializeService` and `IMDateTimeService` required by the license pipeline

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
