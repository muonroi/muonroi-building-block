---
phase: 06-di-telemetry-integration
plan: 02
subsystem: pdf
tags: [pdf, telemetry, activitysource, cancellation, di, options, pdfsharpcore]

requires:
  - phase: 06-01
    provides: PdfMetrics (ActivitySource, OperationCounter, PageCountHistogram), PdfTelemetryNames
  - phase: 05
    provides: PdfSharpCoreWriter (IPdfWriter), determinism normalization
  - phase: 03
    provides: LayoutEngine.LayoutAsync (font + image pre-passes internal)
  - phase: 06-00
    provides: bindable PdfConfigs.PdfLimits
provides:
  - MPdfService — concrete IMPdfService orchestrating parse → cascade → policy → layout → write
  - Render timeout enforcement via linked CTS + CancelAfter(MaxRenderDurationMs)
  - pdf.render span emission with pdf.template_id / tenant.id tags + operation/page-count metrics
  - RenderToBytesAsync and RenderMultiPageAsync (PdfSharpCore page merge)
affects: [06-04 DI extension AddPdf, 06-03 integration tests]

tech-stack:
  added:
    - Microsoft.Extensions.DependencyInjection.Abstractions
    - Microsoft.Extensions.Options
    - Microsoft.Extensions.Options.ConfigurationExtensions
    - Microsoft.Extensions.Logging.Abstractions
  patterns:
    - Per-call scoped-service resolution via IServiceProvider.GetService (captive-dependency avoidance)
    - Linked CancellationTokenSource + CancelAfter for per-operation timeout
    - Catch OperationCanceledException before general catch to preserve cancellation semantics

key-files:
  created:
    - src/Muonroi.Pdf/Internal/Service/MPdfService.cs
  modified:
    - src/Muonroi.Pdf/Muonroi.Pdf.csproj

key-decisions:
  - "Used PolicyValidationResult.Accepted (actual member) rather than IsValid (plan-stated, nonexistent)"
  - "Page count obtained by casting IPositionedPageList to concrete PositionedPageList (.PageCount); the interface is an opaque marker"
  - "HTML size gate reads _configs.Limits.MaxHtmlBytes (configured instance) not the static Defaults backstop"
  - "RenderMultiPageAsync byteCount measured from destination stream position delta; output.Save(closeStream: false) preserves caller stream"

patterns-established:
  - "Telemetry-wrapped orchestration: start activity, set snake_case tags, record counter+histogram with tenant.id tag on completion"
  - "Singleton service resolves scoped ITenantContext per call, never via constructor injection"

requirements-completed: [PIPE-08, TEL-02, TEL-03, TEL-04, TEL-05]

duration: 12min
completed: 2026-05-27
---

# Phase 6 Plan 02: MPdfService Orchestrator Summary

**End-to-end IMPdfService that drives parse→cascade→policy→LayoutAsync→PdfSharpCore writer, enforces a linked-CTS render timeout, and emits a pdf.render span + operation/page-count metrics.**

## Performance

- **Duration:** ~12 min
- **Tasks:** 2
- **Files modified:** 2 (1 created, 1 edited)

## Accomplishments
- `MPdfService : IMPdfService` orchestrates the full pipeline, threading a linked-CTS token through every await (SC2/SC4).
- Telemetry: `pdf.render` activity tagged with `pdf.template_id` + `tenant.id`; `OperationCounter` (status ok/error) and `PageCountHistogram` recorded (SC3).
- Tenant resolved per-call via `IServiceProvider.GetService<ITenantContext>()`, defaulting to `"unknown"`.
- `RenderToBytesAsync` (MemoryStream wrap) and `RenderMultiPageAsync` (per-fragment render + PdfSharpCore `PdfReader.Open(Import)` page merge) implemented.

## Task Commits

1. **Task 1: Add Microsoft.Extensions package references** - `6ab0dba` (feat)
2. **Task 2: MPdfService implementation** - `d140ae9` (feat)

## Files Created/Modified
- `src/Muonroi.Pdf/Internal/Service/MPdfService.cs` - Concrete IMPdfService: pipeline orchestration, timeout, telemetry, multi-page merge
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` - Microsoft.Extensions DI/Options/Logging references + tenancy/logging ProjectReferences

## Decisions Made
- See key-decisions frontmatter. Most notable: actual `PolicyValidationResult.Accepted` member differs from the plan's stated `IsValid`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Plan-stated `policy.IsValid` does not exist; used `policy.Accepted`**
- **Found during:** Task 2
- **Issue:** The plan's verified-signatures block listed `PolicyValidationResult(bool IsValid, ...)`. The actual record is `PolicyValidationResult(bool Accepted, IReadOnlyList<PolicyViolation> Violations)`.
- **Fix:** Used `if (!policy.Accepted) throw new PdfPolicyException(policy.Violations);`
- **Files modified:** MPdfService.cs
- **Verification:** Build succeeds; 63 tests pass
- **Committed in:** d140ae9

**2. [Rule 1 - Bug] `IPositionedPageList.PageCount` does not exist; cast to concrete type**
- **Found during:** Task 2
- **Issue:** Plan claimed `IPositionedPageList.PageCount`. The interface is an empty opaque marker (`public interface IPositionedPageList { }`). `PageCount` lives on the concrete internal `PositionedPageList`.
- **Fix:** `int pageCount = (pages as PositionedPageList)?.PageCount ?? 0;` — safe within the same assembly.
- **Files modified:** MPdfService.cs
- **Verification:** Build succeeds; tests pass
- **Committed in:** d140ae9

**3. [Rule 3 - Blocking] `LogError` missing without Microsoft.Extensions.Logging using**
- **Found during:** Task 2
- **Issue:** `IMLog<T>` derives from `ILogger<T>`; `LogError(...)` is a Microsoft.Extensions.Logging extension method requiring the namespace.
- **Fix:** Added `using Microsoft.Extensions.Logging;`
- **Files modified:** MPdfService.cs
- **Verification:** Build succeeds
- **Committed in:** d140ae9

**4. [Rule 2 - Correctness] HTML byte gate reads configured limit, not static Defaults**
- **Found during:** Task 2
- **Issue:** Plan action step 2 referenced `PdfConfigs.PdfLimits.MaxHtmlBytes` (the static type). Per CONTEXT (06-00 prerequisite), the injected `PdfConfigs` instance carries the configured limit.
- **Fix:** Used `_configs.Limits.MaxHtmlBytes` for both the comparison and the exception's LimitValue.
- **Files modified:** MPdfService.cs
- **Verification:** Consistent with `_configs.Limits.MaxRenderDurationMs` usage for the timeout CTS.
- **Committed in:** d140ae9

---

**Total deviations:** 4 auto-fixed (2 bug, 1 blocking, 1 correctness)
**Impact on plan:** All fixes necessary to compile against actual contracts and honor the 06-00 configured-limits decision. No scope creep.

## Notes
- `TemplateHash` returned as `string.Empty` — content hashing is SEC-07 / Phase 9 scope, as the plan permits.
- `PolicyId` uses `_cssPolicy.Id` (exists per ABST-04, e.g. `default-strict-v1`).
- `RenderMultiPageAsync` byteCount uses destination stream position delta; `output.Save(destination, closeStream: false)` avoids closing the caller-owned stream.

## Issues Encountered
- The `-q` quiet build flag emitted spurious "Building target CoreCompile completely" MSBuild errors despite a successful build. Re-running without `-q` confirmed `Build succeeded` with 0 errors. Verification used the non-quiet form.

## End-to-End RenderAsync Confirmation
The full chain compiles and wires correctly (parse → cascade → policy → LayoutEngine.LayoutAsync → PdfSharpCoreWriter), and `PdfSharpCoreWriter` already normalizes the header to `%PDF-1.7` (Phase 5). A live RenderAsync producing a non-empty `%PDF-1.7` stream is exercised by integration tests in plan 06-04 (not added here per the plan split). All 63 existing unit tests remain green.

## Next Phase Readiness
- `MPdfService` ready for DI registration in plan 06-04 (`AddPdf()`); `IFontResolver` left optional/unregistered per Decision 7.

## Self-Check: PASSED
- src/Muonroi.Pdf/Internal/Service/MPdfService.cs — FOUND
- Commit 6ab0dba — FOUND
- Commit d140ae9 — FOUND

---
*Phase: 06-di-telemetry-integration*
*Completed: 2026-05-27*
