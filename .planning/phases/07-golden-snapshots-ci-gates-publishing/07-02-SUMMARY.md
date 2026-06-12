---
phase: 07-golden-snapshots-ci-gates-publishing
plan: 02
subsystem: testing
tags: [golden-snapshots, determinism, tables, paged-media, counters, images, fonts, security, testing]

# Dependency graph
requires:
  - phase: 07-01
    provides: [GoldenPdf byte comparer, GoldenCorpus registry, DeterminismCanary SC1, PdfRenderCollection, EmbeddedTestFontResolver]
provides:
  - 36 new golden baselines across inline/table/paged-media/counter/image/font/security groups
  - 44-case golden corpus (TEST-01 floor satisfied with comfortable padding)
  - Runtime corpus-floor guard (AllCases.Count >= 40)
  - Hardened-PDF regression lock (%PDF-1.7 header + no /JavaScript, SEC-01/02)
affects: [07-03 CI gates consume the corpus; future subset extensions append new GoldenCorpus groups]

# Tech tracking
tech-stack:
  added: []
  patterns: [per-group [Theory]+MemberData golden test classes, per-call options.ResourceResolver image stub, byte-level security assertion alongside golden baseline, runtime corpus-floor guard test]

key-files:
  created:
    - tests/Muonroi.Pdf.Tests/Golden/InlineLayoutGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/TableGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/PagedMediaGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/ImageGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/FontGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/Golden/SecurityGoldenTests.cs
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/*.pdf (36 baselines)
  modified:
    - tests/Muonroi.Pdf.Tests/Golden/GoldenCorpus.cs

key-decisions:
  - "Reused the 07-01 @font-face serif pattern in every new case so synthesized inline text resolves on the headless host"
  - "Used per-call options.ResourceResolver stub for image-via-resolver instead of mutating the shared harness"
  - "Security golden asserts on rendered bytes directly (%PDF-1.7 + no /JavaScript) in addition to a committed baseline"
  - "Added a runtime AllCases.Count >= 40 guard so the TEST-01 floor is enforced, not manually counted"

patterns-established:
  - "Each subset group is its own [Collection(PdfRenderCollection.Name)] sealed test class iterating GoldenCorpus.ByName via [Theory]/MemberData"
  - "New groups are appended to AllCases so the 07-01 determinism canary covers them for free"

requirements-completed: [TEST-01, TEST-03]

# Metrics
duration: ~12m
completed: 2026-05-27
---

# Phase 7 Plan 02: General Golden Corpus Summary

**Extended the golden corpus from 8 block-layout cases to 44 structural cases spanning inline, tables, paged media + counters, images, fonts, and a hardened-PDF security lock — all byte-equality verified with the regen flag unset.**

## Performance

- **Duration:** ~12 min
- **Completed:** 2026-05-27
- **Tasks:** 3
- **Files modified:** 7 source (6 new test classes + GoldenCorpus.cs) + 36 baselines

## Accomplishments
- 36 new committed golden baselines across 7 subset groups; corpus now 44 cases (TEST-01 floor of 40 cleared with padding).
- Inline (6) + table (7) coverage incl. wrap, baseline, vertical-align, white-space, 2x2/colspan/rowspan, border-collapse:separate + border-spacing, auto/fixed width.
- Paged-media (12): page-break before/after/inside-avoid, multi-page overflow, @page margins, A5/Letter/Legal, landscape, repeating header/footer, counter(page)/counter(pages).
- Image (5): PNG/JPEG data-URI, per-call IResourceResolver, intrinsic + explicit sizing. Font (5): bold/italic/scale/embedded subset/@font-face.
- Security golden locks SEC-01/02 (%PDF-1.7 header + no /JavaScript) and a runtime guard enforces the 40+ floor (TEST-01).

## Task Commits

1. **Task 1: Inline + Table golden groups (13 cases)** - `9d05cbc` (test)
2. **Task 2: Paged-media + counters golden group (12 cases)** - `02270d7` (test)
3. **Task 3: Image + Font + Security golden groups (11 cases), reach 44 corpus cases** - `45d2a51` (test)

**Plan metadata:** `docs(07-02): complete general golden corpus plan`

## Files Created/Modified
- `Golden/GoldenCorpus.cs` - Appended InlineLayout, Tables, PagedMedia, Images, Fonts, Security group fields to AllCases.
- `Golden/InlineLayoutGoldenTests.cs`, `TableGoldenTests.cs`, `PagedMediaGoldenTests.cs`, `ImageGoldenTests.cs`, `FontGoldenTests.cs` - Per-group [Theory] golden verifiers.
- `Golden/SecurityGoldenTests.cs` - %PDF-1.7 + no-/JavaScript byte assertions plus AllCases.Count >= 40 corpus-floor guard.
- `TestResources/Golden/*.pdf` - 36 new embedded byte-equality baselines.

## Decisions Made
- Reused the 07-01 `@font-face{font-family:serif;...}` pattern in every case so headless-host inline text resolves (no determinism weakened).
- `image-via-resolver` uses the per-call `options.ResourceResolver` override rather than touching the shared harness.
- Security regression is asserted directly on rendered bytes in addition to a committed baseline.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing Critical] Carried the @font-face serif declaration into every new case**
- **Found during:** Task 1 corpus authoring.
- **Issue:** Synthesized inline text defaults to family `serif`; an unmatched family throws "No appropriate font found" on the headless host (same condition fixed in 07-01).
- **Fix:** Declared `@font-face{font-family:serif;...}` in each new case's HTML.
- **Files modified:** GoldenCorpus.cs.
- **Verification:** Clean (flag-unset) byte-equality run green.
- **Committed in:** 9d05cbc / 02270d7 / 45d2a51 (task commits).

---

**Total deviations:** 1 auto-fixed (1 missing critical).
**Impact on plan:** Necessary for renders to succeed on the headless host. No scope creep; corpus authored strictly within the declared v0.1 CSS subset.

## Issues Encountered
None — all groups generated baselines and passed byte-equality on the first clean run.

## User Setup Required
None - no external service configuration required.

## Verification

- Full suite: **163/163 pass** (89 prior + 36 new goldens, plus canary/guard), `MUONROI_UPDATE_SNAPSHOTS` unset.
- Determinism canary (07-01 SC1) covers all 44 cases via AllCases.
- Corpus-floor guard asserts `GoldenCorpus.AllCases.Count >= 40` (44 actual).
- Security golden proves `%PDF-1.7` header and absence of `/JavaScript` token.

## Self-Check: PASSED

- All 6 created test classes + 36 baselines present on disk and in commits.
- Commits 9d05cbc, 02270d7, 45d2a51 exist in git log.

## Next Phase Readiness
- 44-case corpus ready for 07-03 CI gates (byte-equality + determinism + corpus-floor are runtime-enforced).

---
*Phase: 07-golden-snapshots-ci-gates-publishing*
*Completed: 2026-05-27*
