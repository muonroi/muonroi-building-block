# Phase 16: PDF Enterprise Governance/ControlPlane Integration — Pattern Map

**Mapped:** 2026-06-20
**Files analyzed:** 10 new/modified files across 3 repos
**Analogs found:** 10 / 10

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj` | config | n/a | self (existing csproj) | exact |
| `src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs` | gate impl | request-response | `src/Muonroi.Pdf.Enterprise/AlwaysAllowFeatureGate.cs` + `ILicenseGuard` delegation | exact (same interface, same project) |
| `src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs` | DI registration | n/a | `src/Muonroi.Governance.Enterprise/EnterpriseGovernanceServiceExtensions.cs` | role-match |
| `src/Muonroi.Pdf.Enterprise/Metering/EnterprisePdfServiceWrapper.cs` | service decorator | request-response | `src/Muonroi.Governance.Enterprise/License/EnterpriseLicenseGuardEnhancer.cs` (tenant context pattern) | role-match |
| `src/Muonroi.Governance.Abstractions/License/LicenseCapabilityResolver.cs` | enum+switch / constants | n/a | self (MODIFY — append to `Capabilities`, `CapabilityKeys`, `FeatureToCapability`) | exact |
| `src/Muonroi.Governance.Enterprise/License/MEnterpriseFailClosedMatrix.cs` | enum+switch | request-response | self (MODIFY — append to `BlocksAllEnterpriseCapabilities`) | exact |
| `src/Muonroi.Quota.Abstractions/QuotaType.cs` | enum | n/a | self (MODIFY — append `PdfRendersPerDay`) | exact |
| `src/Muonroi.Quota.Abstractions/TenantQuota.cs` + `InMemoryTenantQuotaTracker.cs` | model + switch | CRUD | self (MODIFY — add property + `GetLimit` arm) | exact |
| `muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Services/Compliance/PdfAuditControlPlaneStore.cs` | store adapter | CRUD | `src/Muonroi.Governance.Enterprise/ControlPlane/FileControlPlaneStore.cs` | role-match |
| `muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Endpoints/CanaryEndpoints.cs` | endpoint handler | request-response | self (MODIFY `ScorePdfSsimAsync` — add rolloutId + rollback) | exact |

---

## Pattern Assignments

### `src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj` (config)

**Change:** Add `<ProjectReference>` to `Muonroi.Governance.Enterprise`.

**Analog pattern** (`src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj` lines 19-23):
```xml
<ItemGroup>
  <!-- One-way reference: Enterprise -> OSS. The OSS engine must never reference Enterprise (SC5). -->
  <ProjectReference Include="..\Muonroi.Pdf\Muonroi.Pdf.csproj" />
</ItemGroup>
```

**What to add** (append inside the same `<ItemGroup>`):
```xml
<ProjectReference Include="..\Muonroi.Governance.Enterprise\Muonroi.Governance.Enterprise.csproj" />
```

**Strong-naming note:** `<SignAssembly>true</SignAssembly>` and `<AssemblyOriginatorKeyFile>../../Muonroi.snk</AssemblyOriginatorKeyFile>` already present at lines 16-17. No change needed — both `Muonroi.Pdf.Enterprise` and `Muonroi.Governance.Enterprise` use the same `Muonroi.snk` at repo root.

---

### `src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs` (gate impl, request-response)

**Analog 1 — interface to implement:** `src/Muonroi.Pdf.Enterprise/IFeatureGate.cs` (lines 7-20):
```csharp
public interface IFeatureGate
{
    bool IsEnabled(string capabilityKey);
    void EnsureFeatureOrThrow(string capabilityKey);
}
```

**Analog 2 — no-op stub to replace in DI:** `src/Muonroi.Pdf.Enterprise/AlwaysAllowFeatureGate.cs` (lines 10-22):
```csharp
public sealed class AlwaysAllowFeatureGate : IFeatureGate
{
    public static readonly IFeatureGate Instance = new AlwaysAllowFeatureGate();
    private AlwaysAllowFeatureGate() { }
    public bool IsEnabled(string capabilityKey) => true;
    public void EnsureFeatureOrThrow(string capabilityKey) { /* no-op */ }
}
```

**Analog 3 — ILicenseGuard.HasFeature to delegate to:** `src/Muonroi.Governance.Abstractions/License/ILicenseGuard.cs` (lines 33-38):
```csharp
/// <summary>Checks if a specific feature is available under the current license.</summary>
bool HasFeature(string featureName);

/// <summary>Ensures a feature is licensed. Throws if not available.</summary>
void EnsureFeature(string featureName);
```

**Concrete pattern to copy:**
```csharp
// File: src/Muonroi.Pdf.Enterprise/License/LicenseFeatureGate.cs
// Namespace: Muonroi.Pdf.Enterprise.License
// Using: Muonroi.Governance.License  (ILicenseGuard lives in this namespace)

using Muonroi.Governance.License;

namespace Muonroi.Pdf.Enterprise.License;

/// <summary>
/// Real <see cref="IFeatureGate"/> bound to the shared Muonroi license guard.
/// Delegates to <see cref="ILicenseGuard.HasFeature"/> which reads
/// <c>ActivationProof.Features[]</c> via <c>LicenseCapabilityResolver.HasAccess</c>.
/// </summary>
public sealed class LicenseFeatureGate(ILicenseGuard licenseGuard) : IFeatureGate
{
    public bool IsEnabled(string capabilityKey)
        => licenseGuard.HasFeature(capabilityKey);

    public void EnsureFeatureOrThrow(string capabilityKey)
    {
        if (!licenseGuard.HasFeature(capabilityKey))
            throw new FeatureNotLicensedException(capabilityKey);
    }
}
```

**Exception type** (`src/Muonroi.Pdf.Enterprise/FeatureNotLicensedException.cs` lines 8-19) — already in same namespace, no extra using needed:
```csharp
public sealed class FeatureNotLicensedException : InvalidOperationException
{
    public string CapabilityKey { get; }
    public FeatureNotLicensedException(string capabilityKey)
        : base($"[PDF] Feature '{capabilityKey}' is not included in the current license.")
    { CapabilityKey = capabilityKey; }
}
```

---

### `src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs` (DI registration)

**Analog:** `src/Muonroi.Governance.Enterprise/EnterpriseGovernanceServiceExtensions.cs`

**Imports pattern** (lines 1-13):
```csharp
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Governance.Abstractions.License;
// ... domain-specific using block
```

**DI registration pattern** (lines 24-129) — the key idiom is:
- `services.TryAddSingleton<IInterface, Implementation>()` for singletons that callers may override
- `services.Replace(ServiceDescriptor.Singleton<IInterface>(sp => { ... }))` when replacing an OSS default
- `AddMEnterpriseGovernance` must be called **before** `AddPdfEnterprise` (ILicenseGuard is registered by the former)

**Core pattern to copy** (modelled on lines 85-96 of `EnterpriseGovernanceServiceExtensions.cs`):
```csharp
// File: src/Muonroi.Pdf.Enterprise/Extensions/PdfEnterpriseServiceExtensions.cs
using Muonroi.Pdf.Enterprise.License;
using Muonroi.Pdf.Enterprise.Metering;

namespace Muonroi.Pdf.Enterprise.Extensions;

public static class PdfEnterpriseServiceExtensions
{
    /// <summary>
    /// Registers real PDF Enterprise services: LicenseFeatureGate (replaces AlwaysAllowFeatureGate)
    /// and EnterprisePdfServiceWrapper (metering decorator).
    /// Requires AddMEnterpriseGovernance to be called first (registers ILicenseGuard).
    /// </summary>
    public static IServiceCollection AddPdfEnterprise(this IServiceCollection services)
    {
        // Replaces AlwaysAllowFeatureGate with the governance-backed gate.
        services.TryAddSingleton<IFeatureGate, LicenseFeatureGate>();
        // Registers the metering decorator (callers resolve EnterprisePdfServiceWrapper directly
        // or use it as the IMPdfService binding — see Metering section below).
        services.TryAddSingleton<EnterprisePdfServiceWrapper>();
        return services;
    }
}
```

---

### `src/Muonroi.Pdf.Enterprise/Metering/EnterprisePdfServiceWrapper.cs` (service decorator, request-response)

**Analog 1 — tenant context resolution pattern:** `src/Muonroi.Governance.Enterprise/License/EnterpriseLicenseGuardEnhancer.cs` (lines 16-26):
```csharp
public sealed class EnterpriseLicenseGuardEnhancer(
    LicenseConfigs configs,
    CodeIntegrityVerifier codeIntegrityVerifier,
    AntiTamperDetector tamperDetector,
    PolicyEnforcer? policyEnforcer = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null) : ILicenseGuardEnhancer
{
    private readonly ISystemExecutionContextAccessor? _executionContextAccessor = executionContextAccessor;
    // tenant resolved via _executionContextAccessor first, falls back to TenantContext.CurrentTenantId
```

**Analog 2 — ITenantQuotaTracker.IncrementUsageAsync call site:** `src/Muonroi.Quota.Abstractions/InMemoryTenantQuotaTracker.cs` (lines 26-29):
```csharp
public Task IncrementUsageAsync(string tenantId, QuotaType type, int amount = 1, CancellationToken ct = default)
{
    return quotaStore.RecordUsageAsync(tenantId, type, amount, ct);
}
```

**Error handling pattern** — from CLAUDE.md No Silent Catch Rule; mirror the logger pattern already used throughout governance:
```csharp
catch (Exception ex)
{
    _logger?.Error(ex, "[PDF] Metering record failed (non-blocking): {Message}", ex.Message);
}
```

**Concrete pattern to copy:**
```csharp
// File: src/Muonroi.Pdf.Enterprise/Metering/EnterprisePdfServiceWrapper.cs
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.Quota.Abstractions;
using Muonroi.Tenancy.Core;   // TenantContext.CurrentTenantId (AsyncLocal)

namespace Muonroi.Pdf.Enterprise.Metering;

/// <summary>
/// Decorator that wraps the OSS IMPdfService and records per-render quota usage.
/// Never blocks or throws on metering failure (D-02: record-only).
/// </summary>
public sealed class EnterprisePdfServiceWrapper(
    IMPdfService inner,
    ITenantQuotaTracker quotaTracker,
    ISystemExecutionContextAccessor? executionContextAccessor = null,
    IMLog<EnterprisePdfServiceWrapper>? logger = null) : IMPdfService
{
    // Delegate all IMPdfService members to inner; after render, fire-and-log metering.

    private async Task RecordMeteringAsync(int pageCount, CancellationToken ct)
    {
        // Mirror EnterpriseLicenseGuardEnhancer: accessor first, AsyncLocal fallback.
        string? tenantId = executionContextAccessor?.CurrentContext?.TenantId
                           ?? TenantContext.CurrentTenantId;
        if (string.IsNullOrWhiteSpace(tenantId)) return;
        try
        {
            await quotaTracker.IncrementUsageAsync(
                tenantId, QuotaType.PdfRendersPerDay, pageCount, ct);
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "[PDF] Metering record failed (non-blocking): {Message}", ex.Message);
        }
    }
}
```

---

### `src/Muonroi.Governance.Abstractions/License/LicenseCapabilityResolver.cs` (MODIFY — enum+switch, constants)

**Analog:** Self. Read lines 12-95 for the exact extension points.

**Capabilities inner class** (lines 12-63) — append three constants following the same `"<domain>.<feature>"` pattern used by `Connectors = "connectors"` (line 58):
```csharp
// In: public static class Capabilities  (lines 12-63)
// After Connectors / JavaScriptExpressions, append:
/// <summary>The PDF Template Designer feature.</summary>
public const string PdfDesigner = "pdf.designer";
/// <summary>The PDF Template Registry feature.</summary>
public const string PdfRegistry = "pdf.registry";
/// <summary>The PDF Canary quality-regression scorer feature.</summary>
public const string PdfCanary = "pdf.canary";
```

**FeatureToCapability dictionary** (lines 65-79) — append identity mappings (pdf.* key IS the capability key, same pattern as `["server-validation"] = Capabilities.AuditRemote` at line 76):
```csharp
// Append to FeatureToCapability:
[Capabilities.PdfDesigner] = Capabilities.PdfDesigner,
[Capabilities.PdfRegistry] = Capabilities.PdfRegistry,
[Capabilities.PdfCanary]   = Capabilities.PdfCanary,
```

**CapabilityKeys HashSet** (lines 81-95) — append:
```csharp
// Append to CapabilityKeys collection initializer:
Capabilities.PdfDesigner,
Capabilities.PdfRegistry,
Capabilities.PdfCanary,
```

**Critical:** `ResolveCapability` at line 255 returns `null` for keys not in `CapabilityKeys` — this is exactly why both additions are mandatory for `HasAccess` to work for `LicenseTier.Licensed` tenants (Enterprise tier short-circuits at line 121, so already works without these additions for Enterprise tenants only).

---

### `src/Muonroi.Governance.Enterprise/License/MEnterpriseFailClosedMatrix.cs` (MODIFY — switch, internal)

**Analog:** Self. Read lines 39-51 for the extension point.

**BlocksAllEnterpriseCapabilities method** (lines 39-51) — current pattern uses chained `.Equals` calls:
```csharp
private static bool BlocksAllEnterpriseCapabilities(string capability)
{
    return capability.Equals(LicenseCapabilityResolver.Capabilities.CoreRuntime, StringComparison.OrdinalIgnoreCase) ||
           capability.Equals(LicenseCapabilityResolver.Capabilities.AuthRbacPlus, StringComparison.OrdinalIgnoreCase) ||
           // ... 8 more entries ...
           capability.Equals(LicenseCapabilityResolver.Capabilities.AuditRemote, StringComparison.OrdinalIgnoreCase);
}
```

**What to append** (continuing the exact same pattern after line 50):
```csharp
           capability.Equals(LicenseCapabilityResolver.Capabilities.PdfDesigner, StringComparison.OrdinalIgnoreCase) ||
           capability.Equals(LicenseCapabilityResolver.Capabilities.PdfRegistry, StringComparison.OrdinalIgnoreCase) ||
           capability.Equals(LicenseCapabilityResolver.Capabilities.PdfCanary, StringComparison.OrdinalIgnoreCase);
```

**Note:** `MEnterpriseFailClosedMatrix` is `internal static` (line 16), so this change is entirely within `Muonroi.Governance.Enterprise`. The `pdf.*` constants come from `LicenseCapabilityResolver.Capabilities` (which is in `Muonroi.Governance.Abstractions`) — already referenced at line 1 (`using Muonroi.Governance.Abstractions.License`).

---

### `src/Muonroi.Quota.Abstractions/QuotaType.cs` (MODIFY — enum)

**Analog:** Self. Read lines 7-34.

**Existing last member** (line 33): `ConnectorExecutionsPerDay`

**What to append** (after line 33, before closing brace):
```csharp
/// <summary>Maximum number of PDF page-render events recorded per day (record-only; no hard cap in Phase 16).</summary>
PdfRendersPerDay,
```

---

### `src/Muonroi.Quota.Abstractions/TenantQuota.cs` + `InMemoryTenantQuotaTracker.cs` (MODIFY — model + switch)

**TenantQuota.cs analog:** Self. Read lines 86-91 for the pattern of the last connector property:
```csharp
/// <summary>Gets or sets the maximum number of connector executions per day.</summary>
public int MaxConnectorExecutionsPerDay { get; set; } = 100;
```

**What to add to TenantQuota.cs** (after line 91):
```csharp
/// <summary>Gets or sets the maximum PDF render events recorded per day. Default: int.MaxValue (unlimited — Phase 16 is record-only).</summary>
public int MaxPdfRendersPerDay { get; set; } = int.MaxValue;
```

**TenantQuotaPresets update** — add `MaxPdfRendersPerDay = int.MaxValue` to all four presets (Free at line 137, Starter at line 170, Professional at line 200, Enterprise at line 229). All presets should be unlimited for Phase 16 (record-only, no hard cap per D-02).

**InMemoryTenantQuotaTracker.cs analog:** Self. Read lines 43-59 (`GetLimit` switch).

**Current last arm** (line 57): `QuotaType.ConnectorExecutionsPerDay => quota.MaxConnectorExecutionsPerDay,`

**What to append** (between line 57 and the `_ => int.MaxValue` fallback at line 58):
```csharp
QuotaType.PdfRendersPerDay => quota.MaxPdfRendersPerDay,
```

**Note:** The existing `_ => int.MaxValue` fallback at line 58 means a missing arm would NOT throw — it would silently return unlimited. Still add the explicit arm to keep the switch exhaustive and for future quota enforcement.

---

### `muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Services/Compliance/PdfAuditControlPlaneStore.cs` (NEW — store adapter, CRUD)

**Analog:** `src/Muonroi.Governance.Enterprise/ControlPlane/FileControlPlaneStore.cs` — the only existing `IMControlPlaneStore` implementation in the repo.

**Interface to implement** (`src/Muonroi.Governance.Enterprise/ControlPlane/IControlPlaneStore.cs` lines 7-16):
```csharp
public interface IMControlPlaneStore
{
    MControlPlaneRegistry Load();
    void Save(MControlPlaneRegistry registry);
}
```

**Analog Load() pattern** (`FileControlPlaneStore.cs` lines 14-32):
```csharp
public MControlPlaneRegistry Load()
{
    if (!File.Exists(_path))
        return new MControlPlaneRegistry();
    try
    {
        string json = File.ReadAllText(_path);
        MControlPlaneRegistry? registry = jsonSerializeService.Deserialize<MControlPlaneRegistry>(json);
        return registry ?? new MControlPlaneRegistry();
    }
    catch
    {
        return new MControlPlaneRegistry();
    }
}
```

**Target struct:** `MControlPlaneRegistry` holds `AuditTrail: List<MControlPlaneAuditRecord>` (verified `ControlPlaneContracts.cs` lines 75-76). `MControlPlaneAuditRecord` fields (lines 212-253): `AuditId`, `EventType`, `EntityType`, `EntityId`, `Actor`, `OccurredAt`, `DataHash`, `SignatureAlgorithm`, `SignatureKeyId`, `Signature`.

**Source data:** `IRuleSetAuditStore.QueryAsync` (`src/Muonroi.RuleEngine.Runtime/Rules/IRuleSetAuditStore.cs` lines 18-24) returns `Task<RuleSetAuditPage>`. `RuleSetAuditEntry` fields (`RuleSetAuditEntry.cs` lines 8-46): `Id`, `TimestampUtc`, `TenantId`, `WorkflowName`, `Action`, `Version`, `Actor`, `Detail`, `ContentHash`, `SignatureAlgorithm`, `SignatureKeyId`, `Signature`.

**Filter predicate for D-03 events:** `Action.StartsWith("pdf.template.", StringComparison.OrdinalIgnoreCase)` — covers the 6 constants from `PdfTemplateAuditActions` (`pdf.template.created/updated/submitted/approved/rejected/activated`).

**Open question on sync/async** (from RESEARCH.md §Open Questions #2): `IMControlPlaneStore.Load()` is synchronous; `IRuleSetAuditStore.QueryAsync` is async. Resolve with `.GetAwaiter().GetResult()` inside `Load()` or cache via a background `IHostedService`. Recommend the background cache pattern (stores results in-memory, refreshed on a timer) to avoid blocking the sync `Load()` on an EF async query.

**Concrete pattern to copy:**
```csharp
// File: muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Services/Compliance/PdfAuditControlPlaneStore.cs
// Namespace: Muonroi.ControlPlane.Host.Services.Compliance
using Muonroi.Governance.ControlPlane;
using Muonroi.RuleEngine.Runtime.Rules;

namespace Muonroi.ControlPlane.Host.Services.Compliance;

/// <summary>
/// IMControlPlaneStore adapter that surfaces pdf.template.* audit events from
/// IRuleSetAuditStore into the MComplianceExportService evidence chain.
/// </summary>
public sealed class PdfAuditControlPlaneStore(
    IRuleSetAuditStore auditStore,
    ILogger<PdfAuditControlPlaneStore>? logger = null) : IMControlPlaneStore
{
    public MControlPlaneRegistry Load()
    {
        // IRuleSetAuditStore is async; use GetAwaiter().GetResult() for the sync Load() contract.
        // Page size is large (int.MaxValue / 1000) to fetch all pdf.template.* entries.
        // If volume grows, replace with background IHostedService cache.
        RuleSetAuditPage page;
        try
        {
            page = auditStore.QueryAsync(
                workflowName: null,   // null = all workflows; filter on Action prefix below
                page: 1,
                pageSize: 1000)
                .GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[PdfAuditControlPlaneStore] Failed to query audit store.");
            return new MControlPlaneRegistry();
        }

        MControlPlaneRegistry registry = new();
        foreach (RuleSetAuditEntry entry in page.Items
            .Where(e => e.Action.StartsWith("pdf.template.", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.TimestampUtc))
        {
            registry.AuditTrail.Add(new MControlPlaneAuditRecord
            {
                AuditId           = entry.Id,
                EventType         = entry.Action,
                EntityType        = "pdf-template",
                EntityId          = entry.WorkflowName,
                Actor             = entry.Actor ?? "control-plane",
                OccurredAt        = entry.TimestampUtc,
                DataHash          = entry.ContentHash ?? string.Empty,
                SignatureAlgorithm = entry.SignatureAlgorithm ?? string.Empty,
                SignatureKeyId    = entry.SignatureKeyId ?? string.Empty,
                Signature         = entry.Signature ?? string.Empty
            });
        }

        return registry;
    }

    /// <summary>Save is a no-op for this read-only audit adapter.</summary>
    public void Save(MControlPlaneRegistry registry) { /* read-only adapter */ }
}
```

**DI registration** (in control-plane `Program.cs`, after existing PdfTemplateRegistryService registration at line 335):
```csharp
builder.Services.AddSingleton<IMControlPlaneStore, PdfAuditControlPlaneStore>();
```

---

### `muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Endpoints/CanaryEndpoints.cs` (MODIFY — endpoint handler, request-response)

**Analog:** Self. Read lines 46-112 (`ScorePdfSsimAsync`).

**Current signature** (lines 46-49):
```csharp
private static async Task<IResult> ScorePdfSsimAsync(
    IFormFile baseline,
    IFormFile candidate,
    CancellationToken cancellationToken)
```

**Current return** (lines 106-112):
```csharp
return Results.Ok(new
{
    Ssim   = ssim,
    Width  = decodedBaseline.widthA,
    Height = decodedBaseline.heightA
});
```

**Existing `RollbackCanaryAsync` handler pattern** (lines 192-219) — the pattern for calling `ICanaryRolloutService.RollbackCanaryAsync`:
```csharp
private static async Task<IResult> RollbackCanaryAsync(
    Guid rolloutId,
    RollbackCanaryDto request,
    ICanaryRolloutService canaryService,
    RuleEngineDbContext dbContext,
    CancellationToken cancellationToken)
{
    // ...
    await canaryService.RollbackCanaryAsync(
        rolloutId,
        request.Actor ?? "control-plane",
        request.Reason,
        cancellationToken);
    // ...
}
```

**ICanaryRolloutService.RollbackCanaryAsync signature** (`ICanaryRolloutService.cs` lines 30-34):
```csharp
Task RollbackCanaryAsync(
    Guid rolloutId,
    string rolledBackBy,
    string reason,
    CancellationToken cancellationToken = default);
```

**PdfCanaryOptions** (`Options/PdfCanaryOptions.cs` lines 2-19): `SsimThreshold = 0.95`, bound from `"PdfCanary"` section.

**Modified ScorePdfSsimAsync pattern** — extend the existing method (do NOT create a new endpoint):
```csharp
// MODIFY: src/Host/Muonroi.ControlPlane.Host/Endpoints/CanaryEndpoints.cs
// Change ScorePdfSsimAsync signature — inject 2 new params + 1 optional query param:
private static async Task<IResult> ScorePdfSsimAsync(
    IFormFile baseline,
    IFormFile candidate,
    ICanaryRolloutService canaryService,         // NEW — injected by minimal API DI
    IOptions<PdfCanaryOptions> canaryOptions,    // NEW — injected by minimal API DI
    Guid? rolloutId,                             // NEW — optional query string param
    CancellationToken cancellationToken)
{
    // ... existing validation and decode logic unchanged (lines 51-98) ...

    double ssim = SsimScorer.Compare(
        decodedBaseline.rgbA,
        decodedCandidate.rgbB,
        decodedBaseline.widthA,
        decodedBaseline.heightA);

    // NEW: auto-rollback when rolloutId is provided and SSIM is below threshold
    bool autoRolledBack = false;
    if (rolloutId.HasValue && rolloutId.Value != Guid.Empty
        && ssim < canaryOptions.Value.SsimThreshold)
    {
        await canaryService.RollbackCanaryAsync(
            rolloutId.Value,
            rolledBackBy: "system",
            reason: $"SSIM {ssim:F4} below threshold {canaryOptions.Value.SsimThreshold}",
            cancellationToken);
        autoRolledBack = true;
    }

    return Results.Ok(new
    {
        Ssim           = ssim,
        Width          = decodedBaseline.widthA,
        Height         = decodedBaseline.heightA,
        AutoRolledBack = autoRolledBack,             // NEW
        Threshold      = canaryOptions.Value.SsimThreshold // NEW
    });
}
```

**Route registration** (lines 32-36) — no change needed; minimal API DI binds `ICanaryRolloutService` and `IOptions<PdfCanaryOptions>` automatically from the DI container. `Guid? rolloutId` binds from query string by default in minimal APIs.

---

## Shared Patterns

### ILicenseGuard Injection
**Source:** `src/Muonroi.Governance.Enterprise/EnterpriseGovernanceServiceExtensions.cs` lines 50-59
**Apply to:** `LicenseFeatureGate`, `PdfEnterpriseServiceExtensions`
**Pattern:** `ILicenseGuard` is a singleton registered by `AddMEnterpriseGovernance` → call order matters: `AddMEnterpriseGovernance` before `AddPdfEnterprise`.

### Error Handling (No Silent Catch)
**Source:** CLAUDE.md `# No Silent Catch Rule` + exemplar in `EnterpriseLicenseGuardEnhancer` and `MComplianceExportService.cs` (lines 128-133):
```csharp
catch (Exception ex)
{
    _logger?.Error(ex, "Failed to load control-plane registry for compliance export.");
    registry = new MControlPlaneRegistry();
}
```
**Apply to:** `EnterprisePdfServiceWrapper.RecordMeteringAsync`, `PdfAuditControlPlaneStore.Load`
**Rule:** Every catch must log module name + operation + `ex.Message`. Empty catches forbidden.

### Tenant Context Resolution
**Source:** `src/Muonroi.Governance.Enterprise/License/EnterpriseLicenseGuardEnhancer.cs` lines 21-25
**Apply to:** `EnterprisePdfServiceWrapper`
**Pattern:** Inject `ISystemExecutionContextAccessor?` (nullable optional). Resolve tenant as:
```csharp
string? tenantId = executionContextAccessor?.CurrentContext?.TenantId
                   ?? TenantContext.CurrentTenantId;
```
`TenantContext.CurrentTenantId` is the `AsyncLocal` fallback — same as used by `PdfTemplateRegistryService` (line 45 in `PdfTemplateRegistryService.cs`).

### DI Registration Order
**Source:** `EnterpriseGovernanceServiceExtensions.cs` lines 24-129
**Pattern:** Use `services.TryAddSingleton<>()` for new registrations (not `AddSingleton`) so the host can override in tests. Use `services.AddSingleton<IMControlPlaneStore, PdfAuditControlPlaneStore>()` (not `TryAdd`) for the compliance store adapter since `IEnumerable<IMControlPlaneStore>` aggregates all registrations.

### IMLog vs ILogger in Control-Plane
**Evidence:** `PdfTemplateRegistryService.cs` (line 22) uses `ILogger<T>` (not `IMLog<T>`).
**Rule:** Building-block assemblies use `IMLog<T>` (Muonroi.Logging.Abstractions). Control-plane uses `ILogger<T>` (Microsoft.Extensions.Logging). Apply accordingly per repo.

---

## No Analog Found

All Phase 16 files have a close analog in the codebase. No file requires falling back to RESEARCH.md patterns only.

| File | Note |
|------|------|
| `PdfEnterpriseServiceExtensions.cs` | No exact analog (no existing `AddPdf*` extension), but `EnterpriseGovernanceServiceExtensions.cs` is a direct structural model. Role-match quality. |
| `PdfAuditControlPlaneStore.cs` | No existing `IMControlPlaneStore` in control-plane repo (only `MFileControlPlaneStore` is in building-block). Pattern from `FileControlPlaneStore.cs` is the closest structural analog. The async→sync bridge is novel; see Open Questions in RESEARCH.md §2. |

---

## Metadata

**Analog search scope:** `D:/sources/Core/muonroi-building-block/src/`, `D:/sources/Core/muonroi-control-plane/src/`
**Files read:** 22
**Key verified gaps (negative evidence from direct reads):**
- `LicenseCapabilityResolver.cs` lines 81-95: `pdf.*` ABSENT from `CapabilityKeys` and `FeatureToCapability`
- `MEnterpriseFailClosedMatrix.cs` lines 39-51: `pdf.*` ABSENT from `BlocksAllEnterpriseCapabilities`
- `QuotaType.cs` lines 7-34: 13 existing values, NO `PdfRendersPerDay`
- `Muonroi.Pdf.Enterprise.csproj` lines 19-23: only `Muonroi.Pdf` `ProjectReference`, no `Muonroi.Governance.Enterprise`
- `CanaryEndpoints.cs` lines 46-112: `ScorePdfSsimAsync` has no `rolloutId` param and no rollback logic

**Pattern extraction date:** 2026-06-20
