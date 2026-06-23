# Muonroi.Governance.Abstractions

> OSS contracts for license governance: the interfaces, value types, and configuration models that all Muonroi license-aware components program against.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Governance.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Governance.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package is **contracts only** — it ships no runtime behavior. It defines `ILicenseGuard`, `LicenseState`, `LicenseTier`, the policy interfaces, fingerprint abstractions, and the full `LicenseConfigs` options graph. Application code, middleware, and plugin authors reference this package to stay decoupled from the concrete implementation.

The runtime implementation lives in [`Muonroi.Governance`](../Muonroi.Governance/) (OSS) and enterprise extensions in [`Muonroi.Governance.Enterprise`](../Muonroi.Governance.Enterprise/).

## Installation

```bash
dotnet add package Muonroi.Governance.Abstractions --prerelease
```

> In most scenarios you depend on `Muonroi.Governance` or `Muonroi.Governance.Enterprise` instead; those packages pull this one in transitively. Depend on this package directly only when writing a plugin, adapter, or middleware that must not couple to the implementation.

## Quick Start

### Consuming `ILicenseGuard` in a service

Inject `ILicenseGuard` (registered by `Muonroi.Governance`) and call its contract methods:

```csharp
using Muonroi.Governance.License;

public class ReportService(ILicenseGuard licenseGuard)
{
    public byte[] GeneratePdf(ReportRequest request)
    {
        // Throws LicenseException when the feature is not available for the current tier.
        licenseGuard.EnsureFeature("pdf.export");

        // Gate arbitrary actions — logs or throws depending on FailMode config.
        licenseGuard.EnsureValid("report.generate", actionName: "GeneratePdf");

        return BuildPdf(request);
    }

    public bool CanUseMultiTenant() =>
        licenseGuard.HasFeature(FreeTierFeatures.Premium.MultiTenant);
}
```

### Implementing `ILicenseStore` in a custom adapter

```csharp
using Muonroi.Governance.License;

public sealed class DatabaseLicenseStore : ILicenseStore
{
    public LicensePayload? Load() { /* read from DB */ }
    public void Save(LicensePayload payload) { /* persist to DB */ }
    public ActivationProof? LoadActivationProof() { /* ... */ }
    public void SaveActivationProof(ActivationProof proof) { /* ... */ }
    public string? LoadActivationJwt() { /* ... */ }
    public void SaveActivationJwt(string jwt) { /* ... */ }
}

// Register the custom store in the DI container before AddMuonroiGovernance().
builder.Services.AddSingleton<ILicenseStore, DatabaseLicenseStore>();
```

### Implementing `ILicenseGuardEnhancer` for custom enforcement

```csharp
using Muonroi.Governance.License;
using Muonroi.Governance.Abstractions.License;

public sealed class AuditLicenseEnhancer : ILicenseGuardEnhancer
{
    public void OnStartup(LicenseConfigs configs, LicenseState state)
        => Console.WriteLine($"License tier: {state.TrustedTier}");

    public void OnEnsureValid(string actionType, LicenseState state)
        => Console.WriteLine($"Checking action: {actionType}");

    public void OnRecordAction(LicenseActionContext context, LicenseState state) { }
}
```

## Features

- `ILicenseGuard` — tier query (`Tier`, `IsFreeMode`), feature gating (`HasFeature`, `EnsureFeature`), action validation (`EnsureValid`), chain recording (`RecordAction`, `GetChainToken`), and secure decryption (`DecryptSecurely`)
- `LicenseState` — immutable snapshot of validity, tier, expiry, features, and `ActivationProof`; `HasFeature` delegates to `LicenseCapabilityResolver` with backward-compatible key resolution
- `FreeTierFeatures` — well-known feature key constants for free-tier (`db.query`, `http.request`, …) and premium capabilities (`multi-tenant`, `rule-engine`, `audit-trail`, …)
- `LicenseConfigs` — complete options graph: mode (Offline/Online), fingerprint scope, fail mode, chain storage, anti-tampering, TPM anchoring, plus nested `OnlineLicenseConfigs`, `MEnterpriseSecurityConfigs`, and `MComplianceConfigs`
- `ILicenseStore` — load/save `LicensePayload`, `ActivationProof`, and activation JWT
- `ILicenseFingerprintProvider` / `IFingerprintChainStore` / `IFingerprintSigner` — device fingerprint and chain-signing contracts
- `ILicenseGuardEnhancer` — hook into startup, `EnsureValid`, and `RecordAction` without modifying the guard
- `IMPolicyDecisionService` — evaluate `MPolicyDecisionRequest` (user, tenant, permissions bitmask, claims) and receive `MPolicyDecisionResult` (allowed/denied/fallback with authoritative flag)
- `IPolicyStore` — load and save `LicensePolicy` (enforcement rules + per-feature quotas)
- `IAssemblyHashCollector` — assembly integrity fingerprint contract

## Configuration

`LicenseConfigs` is bound from `appsettings.json` under the `"LicenseConfigs"` section by the implementation packages. Key properties:

| Property | Default | Description |
|----------|---------|-------------|
| `Mode` | `Offline` | `Offline` or `Online` activation mode |
| `LicenseFilePath` | — | Path to the signed license file |
| `PublicKeyPath` | — | Path to the RSA public key PEM |
| `ActivationProofPath` | `"licenses/activation_proof.json"` | RSA-signed offline proof generated during online activation |
| `ActivationJwtPath` | `"licenses/activation_jwt.txt"` | JWT for frontend license verification |
| `FallbackToOnlineActivation` | `true` | Attempt online activation when proof is absent |
| `FingerprintScope` | `MachineAndProject` | `MachineAndProject` or `ProjectOnly` |
| `FailMode` | `Soft` | `Soft` (log only) or `Hard` (throw) |
| `EnableChain` | `false` | Enable audit action chain |
| `EnableAntiTampering` | `false` | Runtime anti-tampering checks |
| `RequireSignedPolicy` | `false` | Require a valid signed policy file |

```json
{
  "LicenseConfigs": {
    "Mode": "Offline",
    "LicenseFilePath": "licenses/license.key",
    "PublicKeyPath": "licenses/public.pem",
    "FailMode": "Soft",
    "FingerprintScope": "ProjectOnly"
  }
}
```

## API Reference

| Type | Purpose |
|------|---------|
| `ILicenseGuard` | Central guard: tier query, feature/action gating, chain recording, secure decryption |
| `LicenseState` | Immutable license snapshot — validity, tier, expiry, features, `ActivationProof` |
| `LicenseTier` | Enum: `Free = 0`, `Licensed = 1`, `Enterprise = 2` |
| `FreeTierFeatures` | Well-known feature key constants; `FreeTierFeatures.Premium.*` for paid features |
| `LicenseConfigs` | Root options model; section name `"LicenseConfigs"` |
| `OnlineLicenseConfigs` | Online-mode sub-options: endpoint, heartbeat, certificate pinning |
| `MEnterpriseSecurityConfigs` | Enterprise production security defaults (cert pinning, trusted hosts, response signatures) |
| `MComplianceConfigs` | Compliance export pipeline options (NDJSON export, evidence packs, retention) |
| `ILicenseStore` | Persistence contract for `LicensePayload`, `ActivationProof`, and activation JWT |
| `ILicenseFingerprintProvider` | Device fingerprint generation contract |
| `IFingerprintChainStore` | Chain entry persistence contract |
| `IFingerprintSigner` | HMAC/RSA chain-signing contract |
| `IFingerprintChainStore` | Chain persistence contract |
| `ILicenseGuardEnhancer` | Extension point: `OnStartup`, `OnEnsureValid`, `OnRecordAction` |
| `LicenseException` | Thrown by `EnsureValid` / `EnsureFeature` in Hard fail mode |
| `LicenseActionContext` | Payload passed to `RecordAction` |
| `LicenseExecutionContext` | Execution context snapshot attached to guarded actions |
| `ProofTierAccessor` | Resolves the trusted tier from `LicenseState` + `LicenseRuntimeStatus` |
| `ActivationProof` | RSA-signed offline activation proof |
| `ActivationRequest` / `ActivationResponse` | Online activation API contracts |
| `LicenseHeartbeatRequest` / `LicenseHeartbeatResponse` | Heartbeat keep-alive contracts |
| `FingerprintChainEntry` | Single entry in the tamper-evident action chain |
| `IMPolicyDecisionService` | Evaluates `MPolicyDecisionRequest` → `MPolicyDecisionResult` |
| `MPolicyDecisionRequest` | Decision input: user, tenant, action, resource, permissions, claims |
| `MPolicyDecisionResult` | Decision output: `IsAllowed`, `IsAuthoritative`, `UsedFallback`, `DecisionSource` |
| `MPolicyDecisionConfigs` | Configuration for the policy decision service |
| `IPolicyStore` | Load/save `LicensePolicy` |
| `LicensePolicy` | Policy document: `PolicyEnforcementRules` + per-feature `FeatureQuota` |
| `IAssemblyHashCollector` | Collects assembly checksums for integrity verification |

## Samples

No dedicated sample exists for this package. See the implementation packages for runnable examples:

- [`Muonroi.Governance`](../Muonroi.Governance/) — OSS runtime; registers `ILicenseGuard` and default implementations
- [`Muonroi.Governance.Enterprise`](../Muonroi.Governance.Enterprise/) — enterprise runtime; anti-tampering, heartbeat, compliance export, TPM anchoring

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Governance`](../Muonroi.Governance/) — OSS runtime implementing the contracts in this package
- [`Muonroi.Governance.Enterprise`](../Muonroi.Governance.Enterprise/) — enterprise extension layer (anti-tampering, TPM, compliance, control-plane integration)
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — base exception types (`MException`) and core contracts
- [`Muonroi.Tenancy.Abstractions`](../Muonroi.Tenancy.Abstractions/) — tenant context contracts consumed by governance enforcement

## License

Licensed under the [Apache License, Version 2.0](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE).
