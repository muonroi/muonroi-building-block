---
phase: 05-pdf-writer-determinism-security
plan: 03
subsystem: testing
tags: [xunit, fluentassertions, pdfsharpcore, determinism, security, governance]

requires:
  - phase: 05-01
    provides: PdfSecurityException, ThrowingResourceResolver, DefaultStrictPolicy script rejection
  - phase: 05-02
    provides: PdfSharpCoreWriter, PdfSharpFontResolverAdapter
provides:
  - Writer integration test suite (PdfWriterTests, DeterminismTests, SecurityTests)
  - Automated proof of DET-01 byte-for-byte determinism
  - Automated proof of SEC-01/02/05/06 security boundaries
  - Determinism hardening in PdfSharpCoreWriter (subset-prefix + /ID normalization)
affects: [phase-06-integration, telemetry, di]

tech-stack:
  added: []
  patterns:
    - "Embedded test font (WriterTestFonts) for host-font-independent, deterministic writer tests"
    - "Single shared PdfSharpFontResolverAdapter installed once; backing font map swapped per render"
    - "Latin1 round-trip post-processing to normalize PdfSharpCore per-render random tokens"

key-files:
  created:
    - tests/Muonroi.Pdf.Tests/Writer/PdfWriterTests.cs
    - tests/Muonroi.Pdf.Tests/Writer/DeterminismTests.cs
    - tests/Muonroi.Pdf.Tests/Writer/SecurityTests.cs
    - tests/Muonroi.Pdf.Tests/Writer/WriterTestFonts.cs
  modified:
    - src/Muonroi.Pdf/Internal/Writer/PdfSharpCoreWriter.cs
    - src/Muonroi.Pdf/Internal/Writer/PdfSharpFontResolverAdapter.cs
    - tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj

key-decisions:
  - "Embed TestFont.ttf as an EmbeddedFontInfo rather than rely on OS fonts (build host has no Arial; OS fallback is non-deterministic)"
  - "Install GlobalFontSettings.FontResolver once and swap its font map per render (PdfSharpCore forbids reassigning it after first use)"
  - "Normalize the random font-subset prefix and trailer /ID via fixed-length Latin1 token replacement to achieve byte-for-byte determinism"
  - "Empty page list throws InvalidOperationException (PdfSharpCore cannot save a 0-page PDF); test asserts the throw rather than a fabricated blank page"

patterns-established:
  - "WriterTestFonts: deterministic embedded-font helper shared across writer tests"
  - "Determinism post-processing isolated in PdfSharpCoreWriter.NormalizeForDeterminism"

requirements-completed: [PIPE-07, SEC-01, SEC-02, SEC-04, SEC-05, SEC-06, DET-01, DET-02]

duration: 35min
completed: 2026-05-27
---

# Phase 5 Plan 3: Writer Determinism & Security Test Suite Summary

**16 writer tests proving byte-for-byte determinism (DET-01), %PDF-1.7 header, no forbidden PDF constructs, ThrowingResourceResolver security, and <script> policy rejection — plus the writer hardening required to make determinism real.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 2
- **Files modified:** 7 (4 created, 3 modified)
- **Test count:** 47 → 63 (16 added), 0 failures

## Accomplishments
- PdfWriterTests (6): non-empty output, %PDF-1.7 header (SEC-01), no /JavaScript /Launch /OpenAction /EmbeddedFile (SEC-02), no current-year timestamps (SEC-04), missing-image tolerance, empty-page-list throws
- DeterminismTests (3): byte-identical output across two in-process renders (DET-01), options sensitivity (DET-02), multi-page determinism
- SecurityTests (7): file:// and javascript: throw PdfSecurityException SEC-06; http/https return null; <script> yields forbidden.script-element (SEC-05); clean HTML accepted; %PDF-1.7 integration check (SEC-01)
- Hardened PdfSharpCoreWriter so it is callable more than once per process and produces deterministic bytes

## Task Commits

1. **Task 1: PdfWriterTests + DeterminismTests (+ writer hardening)** - `060f786` (test)
2. **Task 2: SecurityTests** - `d916729` (test)

## Files Created/Modified
- `tests/Muonroi.Pdf.Tests/Writer/PdfWriterTests.cs` - writer output structure/security assertions
- `tests/Muonroi.Pdf.Tests/Writer/DeterminismTests.cs` - DET-01/02 byte-for-byte checks
- `tests/Muonroi.Pdf.Tests/Writer/SecurityTests.cs` - resolver + policy boundary tests
- `tests/Muonroi.Pdf.Tests/Writer/WriterTestFonts.cs` - embedded-font helper (TestFont.ttf)
- `src/Muonroi.Pdf/Internal/Writer/PdfSharpFontResolverAdapter.cs` - swappable font map; parameterless ctor + SetEmbeddedFonts
- `src/Muonroi.Pdf/Internal/Writer/PdfSharpCoreWriter.cs` - single-install resolver + NormalizeForDeterminism
- `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` - reference Muonroi.Pdf.Governance

## Decisions Made
See key-decisions in frontmatter.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] GlobalFontSettings.FontResolver cannot be reassigned after first use**
- **Found during:** Task 1 (DeterminismTests — second WriteAsync call)
- **Issue:** Writer set `GlobalFontSettings.FontResolver` on every call; PdfSharpCore throws "Must not change font resolver after it was once used", so the writer was unusable more than once per process — making DET-01 (two renders) impossible.
- **Fix:** Install a single static `PdfSharpFontResolverAdapter` once; swap its backing font map per render via `SetEmbeddedFonts` under the existing lock.
- **Files modified:** PdfSharpCoreWriter.cs, PdfSharpFontResolverAdapter.cs
- **Verification:** All DeterminismTests + full suite pass.
- **Committed in:** `060f786`

**2. [Rule 1 - Bug] Non-deterministic font-subset prefix and trailer /ID broke DET-01**
- **Found during:** Task 1 (determinism tests failing; the plan flagged this as the expected signal)
- **Issue:** PdfSharpCore injects a random 6-letter font-subset prefix (`ABCDEF+Font`) and a random trailer `/ID [<hex32><hex32>]` per render.
- **Fix:** Added `NormalizeForDeterminism` — a Latin1 round-trip that replaces both tokens with fixed, same-length sentinels (preserving byte offsets and the xref table).
- **Files modified:** PdfSharpCoreWriter.cs
- **Verification:** Two renders now produce byte-identical output (DET-01).
- **Committed in:** `060f786`

**3. [Rule 3 - Blocking] Test project did not reference Muonroi.Pdf.Governance**
- **Found during:** Task 2 (SEC-05 test needs parser/cascade/policy)
- **Issue:** `AngleSharpHtmlParser`, `AngleSharpCascadeEngine`, `DefaultStrictPolicy` live in an assembly the test project did not reference.
- **Fix:** Added the project reference.
- **Files modified:** Muonroi.Pdf.Tests.csproj
- **Committed in:** `d916729`

**4. [Rule 1 - Bug] Plan API mismatches corrected in tests**
- **Found during:** Tasks 1-2
- **Issue:** Plan snippets used `ApplyAsync` (actual: `CascadeAsync(doc, userStyleSheet, ct)`), a 4-arg `ParseAsync` (actual: 2-arg `ParseAsync(html, ct)`), `result.IsValid` (actual: `Accepted`), and assumed a valid 0-page PDF (PdfSharpCore throws).
- **Fix:** Tests use the real signatures/properties; the empty-page test asserts `InvalidOperationException`.
- **Files modified:** PdfWriterTests.cs, SecurityTests.cs
- **Committed in:** `060f786`, `d916729`

---

**Total deviations:** 4 auto-fixed (2 bug, 1 blocking, 1 bug/spec-correction)
**Impact on plan:** Deviations 1 and 2 were essential for DET-01 to be achievable and were explicitly anticipated by the plan ("the test failing is the signal to add the post-processing to the writer"). No scope creep — writer changes are confined to determinism/usability prerequisites.

## Issues Encountered
- Build-host obj-deletion race avoided by using `-m:1 -nodereuse:false` per the plan's caveat.

## User Setup Required
None.

## Next Phase Readiness
- All Phase 5 SEC/DET requirements now have automated coverage; the writer is deterministic and re-entrant within a process.
- Phase 6 (DI/telemetry/integration) can rely on `PdfSharpCoreWriter` producing stable, reproducible bytes.

## Self-Check: PASSED

---
*Phase: 05-pdf-writer-determinism-security*
*Completed: 2026-05-27*
