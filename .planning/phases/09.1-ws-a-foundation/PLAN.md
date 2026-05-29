# Phase 9.1 — WS-A Foundation (PLAN)

> **Branch:** `phase/09.1-ws-a-foundation`
> **Predecessor:** Phase 8.16 (`95036c9`) + C3 (`06d8544`) — Phase 8 family CLOSED
> **Parent:** ROADMAP Phase 9, Workstream A (building-block repo)
> **Scope:** First of 5 sub-phases (9.1→9.5) splitting Phase 9 by workstream. This phase builds the **C# runtime foundation** in this repo only. WS-B/C/D + TCIS cutover land in later sub-phases.

## Goal

Land the `Muonroi.Pdf.Enterprise` library skeleton + capability-gate seam + pure-managed SSIM scorer in the same repo as the OSS engine, so WS-B (control-plane) and WS-C (ui-engine) can integrate against a stable surface in 9.2/9.3 without further churn on this repo.

## In-scope (WS-A only)

| ID | Item | Notes |
|----|------|-------|
| F1 | New project: `src/Muonroi.Pdf.Enterprise/` (commercial assembly, separate NuGet) | Strict-name signed; targets same TFM as `Muonroi.Pdf`; references OSS engine via project ref. |
| F2 | Capability gate seam: `IFeatureGate` + `EnsureFeatureOrThrow(key)` | Default no-op impl in OSS engine (returns true); commercial impl validates via license-server ActivationProof (stub in 9.1; real binding 9.4). |
| F3 | Capability keys constants: `pdf.designer`, `pdf.registry`, `pdf.canary` | Defined in `Muonroi.Pdf.Enterprise.CapabilityKeys`. Consumed by future registry/designer/canary code paths. |
| F4 | Registry client interface: `IMPdfTemplateRegistry` (LookupAsync, ResolveAsync, SubscribeAsync) | Interface + DTOs only; no transport yet (control-plane wire format lands in 9.2). |
| F5 | Hot-reload subscriber interface: `IMPdfTemplateHotReload` | Interface for Redis pub/sub consumer; impl TBD 9.2. |
| F6 | Pure-managed SSIM scorer: `Muonroi.Pdf.Enterprise.Quality.SsimScorer` | Operates on 8-bit RGB pixel buffers (matches `PureImageDecoder` output). NO native deps. Algorithm: standard luminance-only SSIM with 8×8 sliding window. |
| F7 | NuGet packaging metadata for BOTH assemblies | OSS: `Muonroi.Pdf` (Apache-2.0). Commercial: `Muonroi.Pdf.Enterprise` (proprietary EULA). Side-by-side `.nuspec`/`<PackageId>`. |
| F8 | Unit tests: capability gate (allow/deny/throw paths), SSIM scorer (identical=1.0, inverted~0, known reference vectors) | Add to `tests/Muonroi.Pdf.Tests/`. Target +30 tests minimum. |

## Out-of-scope (deferred)

- Real registry transport / Redis subscriber wire (→ **9.2 WS-B-prep** or 9.3)
- License-server ActivationProof RSA verification (→ **9.4 WS-D**)
- Designer component (→ **9.3 WS-C**, ui-engine repo)
- TCIS cutover (→ **9.5**, TCIS repo)
- CI NuGet auto-publish pipeline (covered separately by Phase 7 publishing rails; revisit if gaps)

## Waves

| Wave | Sonnet/Opus | Output | Files (predicted) |
|------|-------------|--------|-------------------|
| **R** | sonnet (gsd-phase-researcher) | `RESEARCH.md` — SSIM algorithm references, capability-gate prior art (license-server `EnsureFeatureOrThrow`), NuGet dual-pkg patterns | RESEARCH.md |
| **A** | sonnet (execute) | F1 + F2 + F3 + F4 + F5 skeleton (interfaces, no impl, compiles + tests green) | `src/Muonroi.Pdf.Enterprise/*.cs`, `Muonroi.Pdf.Enterprise.csproj`, 1 test file |
| **B** | sonnet (execute) | F6 SSIM scorer + tests | `Quality/SsimScorer.cs`, `tests/.../SsimScorerTests.cs` |
| **C** | sonnet (execute) | F7 NuGet metadata + F8 capability-gate tests | `.csproj` package nodes, `*.nuspec` if needed |
| **V** | opus (gsd-verifier) | `VERIFICATION.md`, merge develop | — |

Waves A and B are orthogonal — can run parallel. Wave C depends on A. Wave R blocks A/B.

## Success criteria

| ID | Criterion | Verify |
|----|-----------|--------|
| SC1 | `Muonroi.Pdf.Enterprise` assembly builds + ships separate NuGet alongside OSS engine | `dotnet pack` produces 2 `.nupkg` files; correct license metadata per package |
| SC2 | `EnsureFeatureOrThrow("pdf.designer")` throws `FeatureNotLicensedException` when gate denies, returns void when allows | Unit tests cover both paths |
| SC3 | SSIM scorer returns 1.0 for identical RGB buffers, <0.1 for inverted, monotonically increasing for closer matches | Unit tests with 3 reference vector pairs |
| SC4 | All existing 447 tests still pass + new ≥30 tests | `dotnet test` green |
| SC5 | OSS engine (`Muonroi.Pdf`) has NO reference to `Muonroi.Pdf.Enterprise` (asymmetric coupling) | `grep` import check in OSS sources |
| SC6 | No native deps introduced (still pure-managed) | `.csproj` review; `dotnet list package` |

## Risks / open questions

1. **Capability gate default impl in OSS engine** — should the OSS engine ship `IFeatureGate` interface, or stay totally unaware? Recommendation: interface lives in `Muonroi.Pdf.Enterprise`, OSS engine has zero awareness. Commercial code calls gates before invoking OSS APIs that need licensing.
2. **SSIM perf budget** — full-page diff at 100 dpi ≈ 600×800 pixels ≈ 480k window comparisons. Acceptable for canary (offline) but may need SIMD pass in Phase 9.x if used in hot path. Out of scope here; flag in RESEARCH.md.
3. **Strong-naming** — Need a key file (`muonroi-pdf-enterprise.snk`). Decision: generate in Wave A, commit `.snk` to repo (private signing only; public release uses CI-managed key). Confirm with user if delayed-signed instead.

## Sequencing

R → (A ∥ B) → C → V → merge develop

## References

- `.planning/ROADMAP.md` §"Phase 9: v1.0 Enterprise"
- `PROFILE-V1.md` §7 (Template Format Contract — Designer seam) + §8 (Layout IR Seam)
- Memory `[[project_muonroi_ecosystem_topology]]` (4-repo open-core SaaS)
- License-server `EnsureFeatureOrThrow` precedent (prior art for capability gates)
