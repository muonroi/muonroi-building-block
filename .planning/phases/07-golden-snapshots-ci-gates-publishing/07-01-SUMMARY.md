---
phase: 07-golden-snapshots-ci-gates-publishing
plan: 01
subsystem: testing
tags: [golden-snapshots, determinism, testing, ci]
requires: [AddPdf DI container, IMPdfService.RenderToBytesAsync, EmbeddedTestFontResolver, PdfRenderCollection]
provides: [GoldenPdf byte comparer, GoldenCorpus registry, DeterminismCanary SC1, block-layout golden baselines, binary .gitattributes]
affects: [later corpus plans 07-02/07-03 extend GoldenCorpus.AllCases]
tech-stack:
  added: []
  patterns: [hand-rolled byte-equality golden comparer, opt-in regen via env var, embedded-resource baselines]
key-files:
  created:
    - .gitattributes
    - tests/Muonroi.Pdf.Tests/Golden/GoldenPdf.cs
    - tests/Muonroi.Pdf.Tests/Golden/GoldenCorpus.cs
    - tests/Muonroi.Pdf.Tests/Golden/BlockLayoutGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/DeterminismCanaryTests.cs
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/.gitkeep
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/*.pdf (8 baselines)
  modified: []
decisions:
  - Hand-rolled byte comparer, no Verify.Xunit dependency (locked decision 1)
  - Embedded test font declared under family "serif" so synthesized inline text resolves on headless host
metrics:
  duration: ~15m
  completed: 2026-05-27
---

# Phase 7 Plan 01: Golden-Snapshot Foundation Summary

Established the golden-snapshot infrastructure for byte-stable PDF output: a hand-rolled `GoldenPdf` byte-equality comparer with `MUONROI_UPDATE_SNAPSHOTS` opt-in regeneration, an extensible `GoldenCorpus` registry, the SC1 determinism canary, 8 committed block-layout baselines, and `.gitattributes` marking PDF/TTF baselines binary — all with zero new dependencies.

## What Was Built

- **GoldenPdf** — `internal static` comparer. `UpdateMode` reads `MUONROI_UPDATE_SNAPSHOTS` (`1`/`true`). `VerifyAsync` renders via `PdfServiceTestHarness.BuildProvider` → `RenderToBytesAsync`; in update mode writes the baseline to the SOURCE tree via `[CallerFilePath]` (not bin/), otherwise loads the embedded baseline and asserts `SequenceEqual`, throwing a clear message when absent. Shared `internal RenderAsync` reused by the canary.
- **GoldenCorpus** — registry with per-group `internal static readonly` fields (`BlockLayout`) concatenated into `AllCases`; `AllCasesData`/`BlockCasesData` MemberData sources + `ByName`. Structured so 07-02/07-03 append groups.
- **BlockLayoutGoldenTests** — 8 cases (`block-single`, `block-nested`, `margin-collapse-adjacent`, `margin-collapse-parent-child`, `bfc-root-overflow-hidden`, `box-sizing-padding-border`, `block-multi-paragraph`, `block-background-color`).
- **DeterminismCanaryTests** — SC1: renders every corpus case twice, asserts byte-identity.
- **.gitattributes** — `*.pdf binary`, `*.ttf binary`, `tests/**/TestResources/** -text`.

All golden/canary classes carry `[Collection(PdfRenderCollection.Name)]` (PdfSharpCore FontFactory race).

## Baselines

8 block-layout baselines bootstrapped with `MUONROI_UPDATE_SNAPSHOTS=1`, committed as embedded resources, then verified byte-exact with the flag UNSET.

## Verification

- Build: 0 errors.
- `BlockLayoutGoldenTests`: 8/8 pass (flag unset).
- `DeterminismCanaryTests`: 8/8 pass.
- Full suite: **89/89 pass** (73 prior + 16 new), `MUONROI_UPDATE_SNAPSHOTS` unset.

## Deviations from Plan

**1. [Rule 1 - Correctness] Font family declared as `serif`, not the embedded family name**
- **Found during:** Task 2 corpus authoring.
- **Issue:** The plan suggested a `font-family` referencing the embedded face. The box tree assigns synthesized inline text the default family `serif` and does not inherit a block-level family down to inline text nodes; on the headless host an unmatched family throws "No appropriate font found".
- **Fix:** Declared `@font-face{font-family:serif;...}` in every case (mirrors the existing `MPdfServiceIntegrationTests` pattern). No determinism weakened.
- **Files:** GoldenCorpus.cs. **Commit:** 441c490.

## Self-Check: PASSED

- All 5 created source files + 8 baselines present on disk.
- Commits 97ff700, 441c490, 2062373 exist in git log.
