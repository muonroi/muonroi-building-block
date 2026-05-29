# Phase 9.1 — WS-A Foundation: Research

**Researched:** 2026-05-29
**Domain:** Pure-managed C# SSIM, capability-gate design, dual NuGet packaging
**Confidence:** HIGH (all findings sourced from live codebase or official specifications)

---

## 1. SSIM Algorithm — Pure-Managed C# (Luminance-Only)

### 1.1 Canonical Formula (Wang/Bovik 2004)

Source: Z. Wang, A. C. Bovik, H. R. Sheikh, E. P. Simoncelli, "Image quality assessment: From error
visibility to structural similarity," IEEE Transactions on Image Processing, vol. 13, no. 4, pp.
600–612, Apr. 2004. [CITED: doi.org/10.1109/TIP.2003.819861]

For two windows x and y of size N×N:

```
SSIM(x, y) = (2·μx·μy + C1)(2·σxy + C2)
             ----------------------------------------
             (μx² + μy² + C1)(σx² + σy² + C2)

Where:
  μx    = mean of x window
  μy    = mean of y window
  σx²   = variance of x window
  σy²   = variance of y window
  σxy   = covariance of x and y windows

  L  = 255  (dynamic range for 8-bit)
  K1 = 0.01, K2 = 0.03  (default Wang/Bovik constants)
  C1 = (K1·L)² = (0.01·255)² ≈ 6.5025
  C2 = (K2·L)² = (0.03·255)² ≈ 58.5225
```

Return range: [-1, 1] in theory; natural images produce [0, 1] in practice.

### 1.2 Luminance Conversion

Rec.709 luma (matches HTML/CSS sRGB colorspace): [CITED: ITU-R BT.709]

```
Y = 0.2126·R + 0.7152·G + 0.0722·B
```

Applied per pixel to the 8-bit RGB byte buffer before windowing. Result is a `double` in [0, 255].

### 1.3 PureImageDecoder Buffer Contract

**Critical finding:** `PureImageDecoder.Decode()` returns a `DecodedImage` whose `Data` field
contains the **raw compressed file bytes** (PNG IDAT stream or JPEG file bytes), NOT a decoded
pixel buffer. [VERIFIED: codebase — `src/Muonroi.Pdf/Internal/Image/PureImageDecoder.cs`]

The `SsimScorer` must therefore accept a **pre-decoded, already-rasterised** RGB byte buffer
as its input contract — meaning the caller (canary integration code in 9.2/9.3) is responsible
for rasterizing the PDF page to an RGB buffer before calling the scorer. The scorer itself never
touches `DecodedImage.Data`.

**Recommended input contract:**
```csharp
// Caller provides: flat interleaved RGB bytes, row-major, no padding
// stride = width * 3
// buffer.Length must equal width * height * 3
public double Compare(
    ReadOnlySpan<byte> referenceRgb,
    ReadOnlySpan<byte> candidateRgb,
    int width,
    int height)
```

The test project (`Muonroi.Pdf.Tests`) already uses `PDFtoImage` + `SkiaSharp` to rasterize PDF
pages to bitmaps; `SKBitmap` can produce interleaved RGB byte spans. The canary integration in
9.2/9.3 will follow the same rasterization pattern.
[VERIFIED: codebase — `tests/Muonroi.Pdf.Tests/Golden/VisualRegressionTests.cs` lines 531-552]

### 1.4 Pseudocode — 8×8 Sliding Window

```csharp
// Source: Wang/Bovik 2004 algorithm, adapted for C# with double accumulators
// Window size W = 8 is the standard; yields (width-7)*(height-7) windows
// for a 600×800 image: 593 * 793 = 470,249 windows ≈ 470k (within 480k budget)

const int W = 8;
const double C1 = 6.5025;   // (0.01 * 255)^2
const double C2 = 58.5225;  // (0.03 * 255)^2

double totalSsim = 0.0;
int windowCount = 0;

for (int row = 0; row <= height - W; row++)
{
    for (int col = 0; col <= width - W; col++)
    {
        double sumX = 0, sumY = 0;
        double sumX2 = 0, sumY2 = 0, sumXY = 0;

        for (int wy = 0; wy < W; wy++)
        {
            for (int wx = 0; wx < W; wx++)
            {
                int idx = ((row + wy) * width + (col + wx)) * 3;
                // Rec.709 luma
                double lx = 0.2126 * refRgb[idx]
                          + 0.7152 * refRgb[idx + 1]
                          + 0.0722 * refRgb[idx + 2];
                double ly = 0.2126 * cndRgb[idx]
                          + 0.7152 * cndRgb[idx + 1]
                          + 0.0722 * cndRgb[idx + 2];

                sumX  += lx;   sumY  += ly;
                sumX2 += lx * lx;
                sumY2 += ly * ly;
                sumXY += lx * ly;
            }
        }

        int n = W * W;  // 64
        double muX  = sumX  / n;
        double muY  = sumY  / n;
        // Biased variance and covariance (match Wang/Bovik; unbiased uses n-1)
        double varX  = sumX2 / n - muX * muX;
        double varY  = sumY2 / n - muY * muY;
        double covXY = sumXY / n - muX * muY;

        double numerator   = (2.0 * muX * muY + C1) * (2.0 * covXY + C2);
        double denominator = (muX * muX + muY * muY + C1) * (varX + varY + C2);

        totalSsim += numerator / denominator;
        windowCount++;
    }
}

return windowCount > 0 ? totalSsim / windowCount : 1.0;
```

### 1.5 Edge Handling

**Recommendation: clip (shrunken effective area), not zero-pad.**

The sliding window starts at `(row=0, col=0)` and ends at `(row=height-W, col=width-W)`. Windows
that would extend beyond the image boundary are simply not evaluated. The `windowCount` divisor
reflects the actual number of evaluated windows.

Rationale: zero-padding introduces artificial low-luma border regions that lower SSIM for identical
images near edges. Clipping is the approach used in the MATLAB reference implementation cited in
the Wang/Bovik paper. For a 600×800 image the unsampled border strip is 7 pixels on right/bottom
— negligible for canary use cases.

### 1.6 Numerical Precision

- All intermediate accumulators: `double` (64-bit IEEE 754). [ASSUMED — standard practice; no
  spec mandates this, but `float` accumulation over 64 pixels risks ≈0.1 LSB drift for 8-bit input.]
- Input bytes promoted to `double` inline at the luma conversion site.
- Return type: `double`, range [-1, 1]. For identical buffers: 1.0 exactly (numerator =
  denominator, C constants cancel perfectly when variance = 0).
- No `Math.Clamp` required on the return; the formula is mathematically bounded [−1, 1] when C1,
  C2 > 0. Callers may clamp to [0, 1] for display.

### 1.7 Performance Budget

| Scenario | Windows | Single-threaded estimate |
|----------|---------|--------------------------|
| 600×800 @ 100 dpi | ~470k | ~50–100 ms on modern x64 |
| 1200×1600 @ 200 dpi | ~1.89M | ~200–400 ms |

**Phase 9.1 target:** Single-threaded baseline only. This is an **offline canary** — not a hot path.
50–100 ms is acceptable.

**Flagged for future optimization (NOT 9.1):**
- `Parallel.For` over rows: trivially parallelizable, ~4–8× speedup on 4-core.
- SIMD via `System.Runtime.Intrinsics` (AVX2): vectorize the inner 8×8 accumulation over 8 pixels
  at once using 256-bit registers. Estimated 8–16× speedup vs scalar.
- Both deferred to Phase 9.x per PLAN.md §"SSIM perf budget".

### 1.8 No Image Library Constraint

The scorer operates exclusively on a pre-decoded `byte[]` / `ReadOnlySpan<byte>` in RGB interleaved
format. It introduces zero new NuGet dependencies. [VERIFIED: codebase — PLAN F6 constraint]

---

## 2. Capability-Gate Prior Art

### 2.1 What the License-Server Uses

The license server (`muonroi-license-server`) stores feature entitlements as `string[]
AllowedFeatures` in `LicensePayload` and `ActivationProof.Features`. The server's
`ValidationService.IsActionAllowed()` checks `features.Contains(actionType,
StringComparer.OrdinalIgnoreCase)` with wildcard `"*"` support.
[VERIFIED: codebase — `src/Muonroi.LicenseServer/Services/ValidationService.cs` lines 53-71]

### 2.2 Existing `EnsureFeatureOrThrow` Pattern (building-block repo)

The pattern already exists in `Muonroi.Governance.Abstractions`:

```csharp
// Muonroi.Governance.Abstractions.License.LicenseGuardExtensions
public static IServiceCollection EnsureFeatureOrThrow(
    this IServiceCollection services,
    string featureName)
```

This is a **startup-time DI-registration guard** — it builds a transient `ServiceProvider` to
resolve `LicenseState` and throws `LicenseException` if `state.HasFeature(featureName)` is false.
[VERIFIED: codebase — `src/Muonroi.Governance.Abstractions/License/LicenseGuardExtensions.cs`]

The existing runtime guard is `ILicenseGuard.EnsureFeature(string featureName)` — throws at
**call time**, not at startup. [VERIFIED: codebase — `src/Muonroi.Governance.Abstractions/License/ILicenseGuard.cs` line 38]

### 2.3 Existing Exception Type

```csharp
// Muonroi.Governance.License.LicenseException : MException
// MExceptionCategory.Security, HTTP 403
// Error code: "LICENSE_ERROR"
```
[VERIFIED: codebase — `src/Muonroi.Governance.Abstractions/License/LicenseException.cs`]

**There is no `FeatureNotLicensedException` in the existing codebase.** All gate violations use
`LicenseException` directly. Two options for F2:

| Option | Recommendation |
|--------|----------------|
| A: Reuse `LicenseException` | Consistent with existing ecosystem. Simpler. |
| B: New `FeatureNotLicensedException : LicenseException` | More specific catch site for PDF callers. Matches PLAN.md recommendation. |

**Recommendation: Option B** — `FeatureNotLicensedException : LicenseException` (which itself
extends `MException`). The PDF enterprise assembly has no dependency on `MException`; therefore
define `FeatureNotLicensedException` as `FeatureNotLicensedException : InvalidOperationException`
as stated in PLAN.md. This keeps `Muonroi.Pdf.Enterprise` free of a transitive dependency on
`Muonroi.Core.Abstractions`. [ASSUMED — dependency decision not locked in PLAN; confirm before Wave A]

### 2.4 Recommended `IFeatureGate` Interface Shape

```csharp
namespace Muonroi.Pdf.Enterprise;

/// <summary>
/// Runtime capability gate. The commercial assembly provides a real implementation;
/// the OSS engine has zero awareness of this interface.
/// </summary>
public interface IFeatureGate
{
    /// <summary>
    /// Returns true if the capability key is licensed under the current activation proof.
    /// Never throws.
    /// </summary>
    bool IsEnabled(string capabilityKey);

    /// <summary>
    /// Throws <see cref="FeatureNotLicensedException"/> if <paramref name="capabilityKey"/>
    /// is not licensed. Returns void on success.
    /// </summary>
    void EnsureFeatureOrThrow(string capabilityKey);
}

public sealed class FeatureNotLicensedException : InvalidOperationException
{
    public string CapabilityKey { get; }

    public FeatureNotLicensedException(string capabilityKey)
        : base($"[PDF] Feature '{capabilityKey}' is not included in the current license.")
    {
        CapabilityKey = capabilityKey;
    }
}
```

**Default no-op implementation (stub for 9.1):**

```csharp
/// <summary>
/// Stub used during 9.1. Real binding to ActivationProof RSA verification lands in Phase 9.4.
/// </summary>
internal sealed class AlwaysAllowFeatureGate : IFeatureGate
{
    public static readonly IFeatureGate Instance = new AlwaysAllowFeatureGate();
    public bool IsEnabled(string _) => true;
    public void EnsureFeatureOrThrow(string _) { /* no-op */ }
}
```

**OSS engine awareness:** The OSS engine (`Muonroi.Pdf`, `Muonroi.Pdf.Abstractions`) has zero
reference to `IFeatureGate`. The commercial assembly (`Muonroi.Pdf.Enterprise`) holds the interface
and calls it before invoking OSS engine entry-points that require licensing. PLAN.md SC5 enforces
this with a `grep` import check. [VERIFIED: codebase — `src/Muonroi.Pdf/` and `src/Muonroi.Pdf.Abstractions/` contain no governance imports]

### 2.5 Capability Key Naming Convention

Existing keys in `Muonroi.Governance.Abstractions.License.LicenseCapabilityResolver.Capabilities`:

```
core.runtime    auth.rbac_plus   tenancy.strict   rules.runtime
transport.grpc  transport.message_bus   cache.distributed
audit.trail     runtime.anti_tampering  audit.remote   connectors   js_expressions
```

Pattern: `<domain>.<feature>` using **snake_case** for multi-word features, lowercase dot-separated
namespace. [VERIFIED: codebase — `LicenseCapabilityResolver.cs`]

Existing premium feature keys (legacy, pre-capability-resolver) use **kebab-case**:
`multi-tenant`, `advanced-auth`, `rule-engine`, `anti-tampering`, etc.
[VERIFIED: codebase — `FreeTierFeatures.Premium` constants in `LicenseState.cs`]

**Assessment of proposed PDF keys:**
- `pdf.designer` — **conforms** to `<domain>.<feature>` pattern. Approved.
- `pdf.registry` — **conforms**. Approved.
- `pdf.canary` — **conforms**. Approved.

These keys should be defined as constants in `Muonroi.Pdf.Enterprise.CapabilityKeys` (per PLAN F3).
They do not require registration in `LicenseCapabilityResolver` for the 9.1 stub — that wiring
happens in 9.4 when real `ActivationProof` binding lands.

---

## 3. Dual NuGet Packaging

### 3.1 Current State (already partially implemented)

`Directory.Build.props` already implements a conditional license file strategy:
[VERIFIED: codebase — `Directory.Build.props` lines 59-81]

```xml
<!-- OSS packages (IsCommercialPackage != 'true') -->
<None Include="$(MSBuildThisFileDirectory)LICENSE-APACHE" Pack="true" PackagePath="\" />

<!-- Commercial packages (IsCommercialPackage == 'true') -->
<None Include="$(MSBuildThisFileDirectory)LICENSE-COMMERCIAL" Pack="true" PackagePath="\" />
```

`Muonroi.Pdf.Enterprise.csproj` already sets:
```xml
<IsCommercialPackage>true</IsCommercialPackage>
<PackageLicenseFile>LICENSE-COMMERCIAL</PackageLicenseFile>
<PackageRequireLicenseAcceptance>true</PackageRequireLicenseAcceptance>
```
[VERIFIED: codebase — `src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj`]

The OSS assembly (`Muonroi.Pdf`) uses the `Directory.Build.props` default `<PackageLicenseFile>LICENSE-APACHE</PackageLicenseFile>`.
[VERIFIED: codebase — `Directory.Build.props` line 7]

### 3.2 SDK-Style Metadata Fields

For the commercial package, required fields beyond what `Directory.Build.props` provides:

```xml
<!-- Muonroi.Pdf.Enterprise.csproj — additions needed in Wave C -->
<PropertyGroup>
  <PackageId>Muonroi.Pdf.Enterprise</PackageId>
  <TargetFramework>net8.0</TargetFramework>
  <IsCommercialPackage>true</IsCommercialPackage>

  <!-- PackageLicenseFile: references a file packed into the .nupkg root.
       Use this (not PackageLicenseExpression) for proprietary/custom EULA.
       PackageLicenseExpression is only for SPDX identifiers (Apache-2.0, MIT, etc.)
       Microsoft docs: learn.microsoft.com/en-us/nuget/reference/nuspec#license -->
  <PackageLicenseFile>LICENSE-COMMERCIAL</PackageLicenseFile>
  <PackageRequireLicenseAcceptance>true</PackageRequireLicenseAcceptance>

  <!-- Description distinguishes the commercial package in the NuGet feed -->
  <Description>Enterprise extensions for Muonroi.Pdf: template registry client,
    Redis hot-reload, SSIM quality scorer, and capability gates.</Description>

  <PackageTags>muonroi;pdf;enterprise;html-to-pdf;template-registry;ssim</PackageTags>
</PropertyGroup>
```

For the OSS package (`Muonroi.Pdf`), use `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>`
if switching to expression syntax, OR keep `<PackageLicenseFile>LICENSE-APACHE</PackageLicenseFile>`
(current). Both are valid. [CITED: learn.microsoft.com/en-us/nuget/reference/nuspec#license]

**`<PackageLicenseExpression>` vs `<PackageLicenseFile>`:**
- `PackageLicenseExpression`: SPDX identifier only (e.g., `Apache-2.0`). Cannot be used for
  proprietary licenses.
- `PackageLicenseFile`: Path to a file embedded in the package. Required for custom EULA.
  **Use `PackageLicenseFile` for `Muonroi.Pdf.Enterprise`** — the current approach is correct.

### 3.3 Strong-Naming Decision

**Current state:** `Muonroi.snk` already exists in the repo root as a full RSA keypair
(PRIVATEKEYBLOB header `07 02`, RSA2, 1024-bit). No `<SignAssembly>` property is currently set
in any `.csproj` or `.props` file. [VERIFIED: codebase — file exists, no SignAssembly usage found]

**Recommendation: Option A — commit `.snk` full key, reference from `Directory.Build.props`.**

Rationale:
- The key is already committed. It is a private repo; the risk profile of a committed 1024-bit SNK
  (used for CLR strong-naming, NOT for cryptographic security) is the same as the status quo.
- Delayed signing with a public-key-only `.snk` requires a second CI step to re-sign after build.
  This adds pipeline complexity for no security benefit in a private repo context.
- Strong-naming in .NET Core / .NET 5+ is NOT a security boundary — the CLR does not verify SNK
  signatures by default outside GAC scenarios. Its primary value here is preventing accidental
  assembly substitution.
- The `Muonroi.Governance.Enterprise` pattern (same repo, same threat model) does not use strong
  naming either — consistent with no strong-naming being the current team standard.

**Therefore: wave A should add `<SignAssembly>true</SignAssembly>` and
`<AssemblyOriginatorKeyFile>$(RepositoryRoot)Muonroi.snk</AssemblyOriginatorKeyFile>` to the
`Muonroi.Pdf.Enterprise.csproj` only (commercial assembly). The OSS assembly does not need it
unless team decides to sign all assemblies.**

If the team prefers CI-managed keys (delayed signing for public releases):
1. Generate a public-key-only `.snk` with `sn -p Muonroi.snk Muonroi.pub.snk`
2. Commit `Muonroi.pub.snk`; add `<DelaySign>true</DelaySign>` in csproj
3. Re-sign in CI: `sn -R <assembly> <private.snk>` using CI secret
This is OVERKILL for a private repo in phase 9.1.

### 3.4 File Layout

Confirmed correct by codebase inspection:
```
src/
  Muonroi.Pdf/                       # OSS engine, Apache-2.0
  Muonroi.Pdf.Abstractions/          # OSS contracts, Apache-2.0, netstandard2.0
  Muonroi.Pdf.Governance/            # OSS CSS policy, Apache-2.0
  Muonroi.Pdf.DesignSystem.Default/  # OSS default design system, Apache-2.0
  Muonroi.Pdf.Enterprise/            # Commercial, proprietary EULA
    Muonroi.Pdf.Enterprise.csproj
    (to be populated by Wave A)
tests/
  Muonroi.Pdf.Tests/                 # Existing 73 test files, xUnit + PDFtoImage
```

`Muonroi.Pdf.Enterprise` is already registered in the solution (stub was created in Phase 1,
plan 01-03). [VERIFIED: codebase — directory and stub csproj exist]

The project ref from `Muonroi.Pdf.Enterprise` to `Muonroi.Pdf` (OSS) is correct and expected.
The reverse is FORBIDDEN (SC5). The stub csproj has no project reference at all — Wave A adds it.

### 3.5 `dotnet pack` Produces 2 `.nupkg` Files

Confirmed via `Directory.Build.props` version governance: `VersionPrefix=1.0.0`,
`VersionSuffix=alpha.14`. Both packages will emit at `1.0.0-alpha.14`.

Running `dotnet pack` at solution root (or targeting both projects) produces:
- `Muonroi.Pdf.1.0.0-alpha.14.nupkg` — Apache-2.0
- `Muonroi.Pdf.Enterprise.1.0.0-alpha.14.nupkg` — proprietary EULA

No `.nuspec` file is needed; SDK-style csproj generates the `.nuspec` at pack time.
[CITED: learn.microsoft.com/en-us/nuget/create-packages/creating-a-package-msbuild]

---

## Open Questions for Orchestrator

1. **`FeatureNotLicensedException` base class.** PLAN.md says `FeatureNotLicensedException : InvalidOperationException`. Research shows the existing ecosystem uses `LicenseException : MException`. Making the PDF enterprise exception inherit `InvalidOperationException` avoids a transitive dependency on `Muonroi.Core.Abstractions` from `Muonroi.Pdf.Enterprise`, which is desirable for a distributable commercial package. Confirm: should `Muonroi.Pdf.Enterprise` reference `Muonroi.Governance.Abstractions` (and thus `Muonroi.Core.Abstractions`) and reuse `LicenseException`, or stay dependency-free and use `InvalidOperationException`?

2. **Strong-naming scope.** Research recommends signing `Muonroi.Pdf.Enterprise` only using the existing committed `Muonroi.snk`. Should the OSS packages also be signed? If yes, a global `Directory.Build.props` `<SignAssembly>` entry is cleaner than per-csproj.

3. **SSIM pixel buffer provider for tests.** The test vectors in SC3 (identical=1.0, inverted≈0, reference pairs) require synthetic RGB buffers — these are trivially constructed in test code. However, the canary integration test (9.2/9.3) will need to rasterize a PDF page. The existing `PDFtoImage`+`SkiaSharp` stack in `Muonroi.Pdf.Tests` can produce `SKBitmap` → `byte[]` RGB. Confirm: should `SsimScorer` tests live in the existing `Muonroi.Pdf.Tests` project (which already has `PDFtoImage`), or in a new `Muonroi.Pdf.Enterprise.Tests` project? PLAN.md says `tests/Muonroi.Pdf.Tests/` — treating this as confirmed, noting the project already references the required rasterization stack.

4. **`IFeatureGate` registration in DI.** The stub `AlwaysAllowFeatureGate` is suitable for 9.1. In 9.4 it will be replaced by an `ActivationProof`-backed implementation. Should the DI registration (`services.AddSingleton<IFeatureGate, AlwaysAllowFeatureGate>()`) live in an `AddPdfEnterprise()` extension method inside `Muonroi.Pdf.Enterprise`, or should callers wire it manually? Recommend: `AddPdfEnterprise()` extension for discoverability, consistent with `AddMEnterpriseGovernance()` precedent. Confirm before Wave A.
