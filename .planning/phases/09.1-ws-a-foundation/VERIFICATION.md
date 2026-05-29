# Phase 9.1 — WS-A Foundation (VERIFICATION)

> **Closed:** 2026-05-29
> **Branch:** `phase/09.1-ws-a-foundation` → merged develop
> **Predecessor:** Phase 8.16 (`95036c9`) + C3 (`06d8544`) — Phase 8 family CLOSED
> **Scope:** First sub-phase of Phase 9 split (9.1=WS-A here, 9.2=WS-B, 9.3=WS-C, 9.4=WS-D, 9.5=TCIS cutover). C# runtime foundation only.

## Commits

| # | SHA | Wave | Subject |
|---|-----|------|---------|
| 1 | `d2b0c5c` | — | docs(09.1): open phase — WS-A Foundation |
| 2 | `3649ad2` | R | docs(09.1): Wave R research — SSIM algorithm + capability-gate prior art + dual NuGet patterns |
| 3 | `f301272` | A (+B) | feat(09.1): IFeatureGate seam + capability keys + registry/hot-reload interfaces (Wave A) |
| 4 | `5e7fade` | C | chore(09.1): activate dual NuGet packaging + Enterprise assembly signing (Wave C — F7) |

> Note: Waves A and B ran in parallel; A finished slightly ahead and committed both A's interface files AND B's SsimScorer files (Wave B had already written them to disk) in a single atomic commit. Same cosmetic race seen in 8.16. Both fixes are present, all tests pass — functional outcome identical.

## Findings & deliverables

### F1 — `Muonroi.Pdf.Enterprise` assembly skeleton
- Project: `src/Muonroi.Pdf.Enterprise/` (existed as stub pre-9.1; activated here)
- Targets same TFM (net8.0) as OSS engine; references it via `<ProjectReference>`
- Strong-named via existing `Muonroi.snk` (asymmetric — OSS stays unsigned, see signing strategy below)

### F2 — Capability gate seam
- `IFeatureGate` — `IsEnabled(string capabilityKey)` + `EnsureFeatureOrThrow(string capabilityKey)`
- `FeatureNotLicensedException : InvalidOperationException` carrying `CapabilityKey` property
- `AlwaysAllowFeatureGate` — singleton no-op default impl (used in OSS/dev scenarios; commercial deployments swap in license-bound impl in 9.4)
- Confirmed asymmetric coupling: `grep` of `src/Muonroi.Pdf/**.cs` for `FeatureGate` returns ZERO hits — OSS engine has zero awareness of the gate. Commercial code calls gates before invoking OSS APIs requiring licensing.

### F3 — Capability key constants
- `Muonroi.Pdf.Enterprise.CapabilityKeys`: `PdfDesigner = "pdf.designer"`, `PdfRegistry = "pdf.registry"`, `PdfCanary = "pdf.canary"`
- Naming follows existing license-server `<domain>.<feature>` convention (RESEARCH §2)

### F4 — Registry client interface
- `IMPdfTemplateRegistry`: `LookupAsync(templateId)`, `ResolveAsync(templateId, version)`, `SubscribeAsync(IAsyncObserver<TemplateChange>)`
- DTOs (records): `TemplateDescriptor`, `TemplateVersion`, `TemplateChange`, `IAsyncObserver<T>`
- Interface + records only; no transport. Wire format lands in 9.2 (WS-B).

### F5 — Hot-reload subscriber interface
- `IMPdfTemplateHotReload`: `StartAsync(CancellationToken)`
- Comment marks Redis impl as 9.2 deferral.

### F6 — Pure-managed SSIM scorer
- `Quality/SsimScorer.Compare(ReadOnlySpan<byte> rgbA, ReadOnlySpan<byte> rgbB, int width, int height)`
- Rec.709 luminance: `Y = 0.2126R + 0.7152G + 0.0722B` (double accumulators)
- 8×8 sliding window with **clip edge handling** (effective area shrinks at borders; no zero-pad)
- Wang/Bovik 2004 constants: `C1 = 6.5025`, `C2 = 58.5225`
- Biased variance estimator (÷N, not ÷(N−1))
- Identical buffers return **exactly 1.0** (strict `.Should().Be(1.0)` equality, verified)
- Small-image guard: <8×8 buffers fall through to a single clipped window
- NO SIMD — single-threaded baseline per PLAN; deferred to 9.x if perf budget tight

### F7 — Dual NuGet packaging
- `Directory.Build.props` already routed licenses via `<IsCommercialPackage>` (existed pre-9.1)
- OSS `Muonroi.Pdf.csproj`: explicit `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` (overrides global file-based default), PackageId, Version=0.1.0-alpha, tags
- Commercial `Muonroi.Pdf.Enterprise.csproj`: PackageId, Version=0.1.0-alpha, tags, `<SignAssembly>true</SignAssembly>` + `<AssemblyOriginatorKeyFile>../../Muonroi.snk</AssemblyOriginatorKeyFile>`
- `LICENSE-APACHE` and `LICENSE-COMMERCIAL` files present at repo root
- `dotnet pack -c Release` produces:
  - `Muonroi.Pdf.0.1.0-alpha.nupkg` (~2.17 MB; `<license type="expression">Apache-2.0</license>`)
  - `Muonroi.Pdf.Enterprise.0.1.0-alpha.nupkg` (~17.98 KB; `<license type="file">LICENSE-COMMERCIAL</license>` + bundled file)

### F8 — Tests added
- `Enterprise/FeatureGateTests.cs` — 14 tests (allow paths, deny paths, exception shape with CapabilityKey, multi-key scenarios)
- `Enterprise/CapabilityKeysTests.cs` — 6 tests (literal values, prefix convention, uniqueness)
- `Enterprise/SsimScorerTests.cs` — 6 tests (identical=1.0 exact, inverted<0.2, slightly-perturbed>0.9, monotonicity, mismatched-size throws, exact 8×8)
- `Enterprise/PackagingMetadataTests.cs` — 2 tests (Enterprise signed `GetPublicKey().Length=160`, OSS unsigned `=0`)
- **Total new: +28 tests**

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | `Muonroi.Pdf.Enterprise` builds + ships separate NuGet alongside OSS engine | PASS — `5e7fade` produces 2 `.nupkg` with distinct license metadata |
| SC2 | `EnsureFeatureOrThrow("pdf.designer")` throws `FeatureNotLicensedException` when denied, returns void when allowed | PASS — 14 `FeatureGateTests` cover both paths |
| SC3 | SSIM returns 1.0 for identical RGB buffers, <0.2 for inverted, monotonic for closer matches | PASS — 6 `SsimScorerTests` with strict-equality identical check |
| SC4 | All existing 447 tests still pass + new tests | PASS — 475/475 green (447 + 28 new) |
| SC5 | OSS engine has NO reference to `Muonroi.Pdf.Enterprise` (asymmetric coupling) | PASS — `grep` of `src/Muonroi.Pdf/` for `FeatureGate` / `Muonroi.Pdf.Enterprise` returns zero hits |
| SC6 | No native deps introduced (still pure-managed) | PASS — `dotnet list package` shows only managed deps; no native binaries shipped |

## Signing strategy

Asymmetric — **Enterprise signed, OSS unsigned**. Rationale:
- Enterprise consumers expect a strong-named commercial binary (versioning + tamper-detection)
- OSS contributors benefit from skip-friction — no need to manage signing for community PRs
- `Muonroi.snk` already committed to repo (private repo, full-key strategy per RESEARCH §3 recommendation)
- Public-key length verified at runtime: 160 bytes for Enterprise assembly, 0 for OSS — `PackagingMetadataTests` codifies this.

## Files changed

- `src/Muonroi.Pdf.Enterprise/IFeatureGate.cs` (new)
- `src/Muonroi.Pdf.Enterprise/FeatureNotLicensedException.cs` (new)
- `src/Muonroi.Pdf.Enterprise/AlwaysAllowFeatureGate.cs` (new)
- `src/Muonroi.Pdf.Enterprise/CapabilityKeys.cs` (new)
- `src/Muonroi.Pdf.Enterprise/Registry/IMPdfTemplateRegistry.cs` (new)
- `src/Muonroi.Pdf.Enterprise/Registry/IMPdfTemplateHotReload.cs` (new)
- `src/Muonroi.Pdf.Enterprise/Quality/SsimScorer.cs` (new)
- `src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj` (activated: project ref, package metadata, signing)
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` (added package metadata, license expression)
- `tests/Muonroi.Pdf.Tests/Enterprise/FeatureGateTests.cs` (new, 14 tests)
- `tests/Muonroi.Pdf.Tests/Enterprise/CapabilityKeysTests.cs` (new, 6 tests)
- `tests/Muonroi.Pdf.Tests/Enterprise/SsimScorerTests.cs` (new, 6 tests)
- `tests/Muonroi.Pdf.Tests/Enterprise/PackagingMetadataTests.cs` (new, 2 tests)
- `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` (project ref to Enterprise)

## Lessons learned

- **Pre-existing infra paid off.** RESEARCH §3 surfaced that `Directory.Build.props` already routed dual licenses via `<IsCommercialPackage>` and `Muonroi.snk` was already in the repo — Wave C only had to activate flags + add explicit license expression for OSS to override the global file-based default. Saves a workstream's worth of yak-shaving.
- **Wave A/B race repeats.** Same pattern as 8.16: when Wave B writes files to disk before Wave A's `git add .` runs, A absorbs both atomic-split goals into one commit. Functionally harmless; cosmetically suboptimal. To prevent: stagger waves OR have B write to a temp dir until A commits. Not worth the orchestration cost given track record.
- **Sonnet stuck-at-final-report repeats.** Wave C also hung at the report step (memory `[[feedback_agent_stuck_pattern]]`). User flagged it; I confirmed via git state that the commit had actually landed (`5e7fade`). The notification arrived during investigation. Going forward: when a sonnet executor "feels stuck" past ~10 min, check git log before pulling the plug — often the work is done and only the report is hung.

## What 9.1 unlocks

- **9.2 WS-B (control-plane):** registry/versioning/maker-checker/audit can now compile against `IMPdfTemplateRegistry` interface and emit `TemplateChange` events matching the DTO shape.
- **9.3 WS-C (ui-engine):** Designer component knows the exact capability-gated surface (`CapabilityKeys.PdfDesigner`) it must respect.
- **9.4 WS-D (license-server):** PDF capability keys (`pdf.designer/registry/canary`) ready to be issued in ActivationProof; commercial `IFeatureGate` impl swaps in for `AlwaysAllowFeatureGate`.
- **9.5 TCIS cutover:** `IMPdfService` + registry surface stable; cutover can begin once 9.2 wires control-plane.
- **SSIM:** Canary quality gate scorer ready for control-plane to invoke offline post-render.

## References

- `.planning/phases/09.1-ws-a-foundation/PLAN.md`
- `.planning/phases/09.1-ws-a-foundation/RESEARCH.md`
- `.planning/ROADMAP.md` §"Phase 9: v1.0 Enterprise"
- `PROFILE-V1.md` §7 (Template Format Contract — Designer seam) + §8 (Layout IR Seam)
- Memory `[[project_muonroi_ecosystem_topology]]`
- Wang & Bovik (2004), "Image Quality Assessment: From Error Visibility to Structural Similarity", IEEE TIP 13(4)
