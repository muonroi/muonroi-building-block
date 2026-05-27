---
phase: 05-pdf-writer-determinism-security
plan: 02
subsystem: pdf-writer
tags: [pdfsharpcore, pdf, fonts, determinism, security]

requires:
  - phase: 05-01
    provides: PdfSecurityException, ThrowingResourceResolver, PdfSharpCore 1.3.65 PackageReference
  - phase: 04-font-image-pipeline
    provides: EmbeddedFontInfo, DecodedImage, FontPipeline/ImagePipeline validation
  - phase: 03-box-tree-layout-engine
    provides: PositionedPageList, PositionedPage, PositionedElement, InlineBox, ReplacedBox
provides:
  - "PdfSharpFontResolverAdapter: maps EmbeddedFontInfo bytes into PdfSharpCore font subsystem"
  - "PdfSharpCoreWriter: full IPdfWriter that emits deterministic, hardened PDF 1.7 streams"
affects: [05-03-determinism-tests, 06-di-telemetry-integration]

tech-stack:
  added: []
  patterns:
    - "Internal cast IPositionedPageList -> PositionedPageList at writer boundary"
    - "GlobalFontSettings.FontResolver swapped under static lock, restored in finally"
    - "Determinism via empty doc.Info + fixed sentinel timestamps + %PDF-1.7 header patch"

key-files:
  created:
    - src/Muonroi.Pdf/Internal/Writer/PdfSharpFontResolverAdapter.cs
    - src/Muonroi.Pdf/Internal/Writer/PdfSharpCoreWriter.cs
  modified: []

key-decisions:
  - "Reused existing PdfPageSizeDimensions.Get helper (handles A3 + sentinel default) instead of duplicating the page-size table from the plan"
  - "Added IFontResolver.DefaultFontName (= Arial) — present in PdfSharpCore 1.3.65 but absent from the plan's interface snapshot"
  - "Fully qualified PdfSharpCore.Fonts.IFontResolver to disambiguate from Abstractions.IFontResolver (global using collision)"

patterns-established:
  - "Font resolver bridge: face key {family}#{weight}#{style}, exact -> weight-only -> family-prefix -> PlatformFontResolver fallback"
  - "Security-by-omission: writer never calls /JavaScript /Launch /OpenAction /EmbeddedFile APIs (SEC-02)"

requirements-completed: [PIPE-07, SEC-01, SEC-02, SEC-03, SEC-04, DET-01, DET-02, DET-03]

duration: 18min
completed: 2026-05-27
---

# Phase 5 Plan 02: PdfSharpCore Writer Summary

**PdfSharpCoreWriter converts a PositionedPageList into a deterministic, hardened PDF 1.7 stream, with PdfSharpFontResolverAdapter wiring embedded font bytes into PdfSharpCore.**

## Performance

- **Duration:** ~18 min
- **Tasks:** 2
- **Files modified:** 2 created

## Accomplishments
- `PdfSharpFontResolverAdapter` implements `PdfSharpCore.Fonts.IFontResolver`, mapping `EmbeddedFontInfo` subset bytes to PdfSharpCore face names with a graceful fallback chain to OS fonts.
- `PdfSharpCoreWriter` implements `IPdfWriter.WriteAsync`: casts to the internal `PositionedPageList`, draws positioned `InlineBox` text and `ReplacedBox` images, and produces deterministic, security-hardened output.
- Determinism + metadata suppression: all `doc.Info` strings emptied, `CreationDate`/`ModificationDate` set to a fixed sentinel, and the header normalized to `%PDF-1.7`.
- All 47 existing tests still pass; solution builds with 0 errors.

## Task Commits

1. **Task 1: PdfSharpFontResolverAdapter** - `652bedb` (feat)
2. **Task 2: PdfSharpCoreWriter — full IPdfWriter implementation** - `bf3d974` (feat)

## Files Created/Modified
- `src/Muonroi.Pdf/Internal/Writer/PdfSharpFontResolverAdapter.cs` - IFontResolver bridge for embedded fonts
- `src/Muonroi.Pdf/Internal/Writer/PdfSharpCoreWriter.cs` - Full IPdfWriter implementation

## Decisions Made
- Reused the existing `PdfPageSizeDimensions.Get` helper (already maps A4/A5/A3/Letter/Legal with a sentinel default) rather than duplicating the plan's inline table; orientation swap applied at the writer.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] PdfSharpCore namespace is `PdfSharpCore.*`, not `PdfSharp.*`**
- **Found during:** Task 1 (build failure CS0246)
- **Issue:** The plan's API reference used `PdfSharp.Fonts` / `PdfSharp.Drawing` / `PdfSharp.Pdf`; the actual 1.3.65 assembly exposes types under `PdfSharpCore.*`.
- **Fix:** Used `PdfSharpCore.Fonts`, `PdfSharpCore.Drawing`, `PdfSharpCore.Pdf`. Verified via assembly reflection probe.
- **Files modified:** both new files
- **Committed in:** `652bedb`, `bf3d974`

**2. [Rule 3 - Blocking] `IFontResolver.DefaultFontName` member required**
- **Found during:** Task 1 (build failure CS0535)
- **Issue:** PdfSharpCore 1.3.65's `IFontResolver` declares a `DefaultFontName` property not present in the plan's interface snapshot.
- **Fix:** Implemented `DefaultFontName => "Arial"`.
- **Files modified:** PdfSharpFontResolverAdapter.cs
- **Committed in:** `652bedb`

**3. [Rule 3 - Blocking] `IFontResolver` ambiguity with Abstractions global using**
- **Found during:** Task 2 (build failure CS0104)
- **Issue:** `Muonroi.Pdf.Abstractions.IFontResolver` (global using) collides with `PdfSharpCore.Fonts.IFontResolver`.
- **Fix:** Fully qualified the PdfSharpCore type where the resolver is saved/restored; aliased in the adapter.
- **Files modified:** both new files
- **Committed in:** `652bedb`, `bf3d974`

---

**Total deviations:** 3 auto-fixed (all Rule 3 - blocking, caused by plan API reference using the wrong root namespace and an incomplete interface snapshot).
**Impact on plan:** No scope creep. No contract changes — the public `IPdfWriter` signature and all Abstractions targets are untouched.

## Issues Encountered
- The host's `--no-incremental` build deletes `obj` files and races (GenerateTargetFrameworkMonikerAttribute/FileListAbsolute errors), as noted in the environment caveat. Resolved by building/testing with `dotnet build|test -m:1 -nodereuse:false`. No csproj/target-framework changes made.

## Next Phase Readiness
- Writer is ready for 05-03 determinism tests (render-twice byte equality) and the SEC never-write assertions.
- If sentinel timestamps prove insufficient for byte-equality (trailer `/ID` hash), 05-03 should add the binary `/ID` zero-out post-process described in the plan context.

## Self-Check: PASSED

---
*Phase: 05-pdf-writer-determinism-security*
*Completed: 2026-05-27*
