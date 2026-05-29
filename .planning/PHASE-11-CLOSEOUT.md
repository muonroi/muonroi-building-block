# Phase 11 — Consumer Friction Hotfix — CLOSED

**Closed**: 2026-05-29
**Version**: 1.0.1
**Final test count**: 491/491 (16 new across sub-phases)

## Closing-goal verification

**Goal**: "TCIS fileexporter `POST /preview-registration` returns valid PDF using only `services.AddPdf(configuration)` — zero consumer-side override classes."

**Status**: ✅ Engineering scope complete — engine-side changes shipped. TCIS-side revert + curl verification pending (next handoff).

## Commits on develop

| SHA | Sub-phase | Scope |
|---|---|---|
| `5e802b1` | 11.4 | `LegacyPrintPolicy` soft-degrade option for flex/grid |
| `7e5a1ce` | 11.2 | `PngDecoder` palette (color_type=3) + RGBA (color_type=6) |
| `2798378` | 11.1 | `AddPdf` self-registers Muonroi.Logging + `ISystemExecutionContextAccessor` |
| `9d69c64` | 11.3 | `DefaultFontResolver` + generic-family fallback |
| `52ea5f2` | 11.2 fixup | Update rejection tests for palette/RGBA now-supported + grayscale still-unsupported |
| `68148f8` | 11.5 | Bump `Muonroi.Pdf` + `Muonroi.Pdf.Enterprise` to v1.0.1 |

## What changed

### 11.1 — DI self-wiring
- `services.AddPdf(configuration)` now wires `AddMuonroiLogging()` + `TryAddSingleton<ISystemExecutionContextAccessor, SystemExecutionContextAccessor>()` internally.
- Consumer override still works (TryAdd contract preserved): pre-register before AddPdf to substitute custom impl.
- 2 new tests verify self-registration + override contract.

### 11.2 — PNG palette + RGBA
- `PngDecoder` parses PLTE + optional tRNS chunks for color_type=3, expands index bytes to RGB(A).
- color_type=6 (RGBA) composites alpha onto white background → emits RGB pixels.
- Grayscale (color_type=0/4) still fail-loud, out of scope for v1.0.1.
- 5 new tests with hand-crafted PNG fixtures (pure BCL zlib + CRC, no SkiaSharp in production).

### 11.3 — DefaultFontResolver
- New `Muonroi.Pdf.Internal.Font.DefaultFontResolver` reading `PdfConfigs:FontResolver` config:
  ```json
  "PdfConfigs": {
    "FontResolver": {
      "Fonts": [{ "Family": "Arial", "Path": "wwwroot/Fonts/arial.ttf", "Weight": 400, "Style": "Normal" }],
      "GenericFamilyMap": { "serif": "Times New Roman", "sans-serif": "Arial", "monospace": "Arial" },
      "FallbackToFirstRegistered": true
    }
  }
  ```
- Resolution algorithm: exact match → family-only → generic-family mapping → first-registered fallback.
- Wired via `TryAdd` in `AddPdf`; consumer override preserved.
- 5 new tests (exact, generic-fallback, empty-registry, first-registered-fallback, TryAdd-override).

### 11.4 — LegacyPrintPolicy soft-degrade
- New `PdfConfigs.Policy.SoftDegradeUnknownDisplay` (default `false` = strict charter behavior).
- When `true`: `display:flex|grid` emits `Warning` instead of `Error`, layout treats element as `display:block`.
- `flex-*`/`grid-*` properties dropped silently with one aggregate warning per page.
- Telemetry counter `muonroi_pdf_policy_soft_degrade_total` increments per page.
- `DefaultStrictPolicy` untouched — charter "fail-loud" still default behavior.
- 4 new tests.

## Out of scope / followups

| Item | Why deferred |
|---|---|
| Real flex/grid layout engine | Soft-degrade is block-stack only — proper flex layout = Phase 12+ |
| PNG SMask for true alpha | White composite sufficient for v1.0.1 logos/preview |
| Font style synthesis (italic from regular, bold via stroke) | Defer until consumer demand |
| Multi-tenant font scoping in `DefaultFontResolver` | Defer to enterprise tier |
| Grayscale PNG (color_type=0/4) support | Out of scope — clear fail-loud message instructs converting to RGB |

## TCIS-side revert checklist (next handoff)

In `D:\sources\TEP\tcis.eport.fileexporter.services` (and 3 other services):

- [ ] Delete `BypassFontResolver` + `BypassPolicy` classes (`Infrastructures/BypassPreviewServices.cs`)
- [ ] Delete `SanitizeFlexCss`, `InjectFontFallback`, `NormalizePngToRgb` helpers + callsites in `FileExporterService.cs`
- [ ] Delete `services.AddSingleton<ISystemExecutionContextAccessor, ...>()` line in `Program.cs` (engine self-registers)
- [ ] Delete `services.AddLogging(b => b.AddMuonroiLogging())` line in `Program.cs` (engine self-registers)
- [ ] Add `PdfConfigs:FontResolver:Fonts[]` to `appsettings.json` pointing to `wwwroot/Fonts/arial.ttf` + `times.ttf`
- [ ] Add `PdfConfigs:Policy:SoftDegradeUnknownDisplay: true` to `appsettings.json`
- [ ] Rebuild + rerun curl `POST /api/v1/full-container/delivery/preview-registration`
- [ ] Verify HTTP 200 + viewable PDF artifact

Apply same revert to:
- `tcis.eport.download.aggregate.services`
- `tcis.eport.eeir.aggregate.services`
- `tcis.eport.fullcontainerdelivery.aggregate.services`

## Lessons learned

1. **Worktree isolation fails when parent cwd is wrong repo** — spawned with `isolation: worktree` from TCIS-service cwd; engine repo `git worktree list` showed only base develop. All 4 agents wrote to same working tree. Mitigated by their disciplined `git add` per-file (no `git add -A`). Risk could've caused commit bundling.
2. **Pre-staged WIP detected by 11.1 agent** — work was already partially in tree, suggesting prior session WIP. Agent staged accurately based on commit message scope.
3. **Cross-cutting test fixups** — 11.2's PngDecoder change broke `ImageRejectionTests` (still asserted PdfFormatException). Caught by orchestrator post-merge, committed as `52ea5f2`. Lesson: agents should grep for tests whose assertions encode the contract being changed.
