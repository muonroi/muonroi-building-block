---
phase: 06-di-telemetry-integration
plan: 03
subsystem: infra
tags: [dependency-injection, options, validation, telemetry, csharp]

requires:
  - phase: 06-01
    provides: PdfTelemetryDescriptor, PdfMetrics
  - phase: 06-02
    provides: MPdfService (IMPdfService implementation)
provides:
  - AddPdf() DI extension registering the full HTML/CSS→PDF pipeline
  - PdfConfigs binding + ValidateOnStart() startup validation (all seven limits > 0)
  - PdfTelemetryDescriptor registered as ITelemetryDescriptor for OtelSetup discovery
affects: [consuming hosts, otel-setup, integration-tests]

tech-stack:
  added: []
  patterns:
    - "Idempotent DI registration via TryAddSingleton/TryAddEnumerable (caller-overridable)"
    - "Fail-fast options validation with BindConfiguration().Validate().ValidateOnStart()"

key-files:
  created:
    - src/Muonroi.Pdf/Extensions/PdfServiceCollectionExtensions.cs
  modified:
    - src/Muonroi.Pdf/Muonroi.Pdf.csproj

key-decisions:
  - "No default IFontResolver registration (Decision 7) — MPdfService optional ctor param resolves to null"
  - "csproj + extension committed together as one atomic unit (Governance ref required to compile)"

patterns-established:
  - "Pattern: package DI entry point lives in Muonroi.<Pkg>.Extensions, not Microsoft.Extensions.DependencyInjection"
  - "Pattern: every default adapter uses TryAdd so AddPdf is idempotent and caller-overridable"

requirements-completed: [PKG-02, DI-01, DI-02, DI-03, DI-04, TEL-01]

duration: 8min
completed: 2026-05-27
---

# Phase 6 Plan 03: AddPdf DI Registration Summary

**`AddPdf(IServiceCollection, IConfiguration)` in `Muonroi.Pdf.Extensions` — idempotent TryAdd registration of the full pipeline + `PdfConfigs` startup validation via `ValidateOnStart()`.**

## Performance

- **Duration:** ~8 min
- **Tasks:** 1
- **Files modified:** 2 (1 created, 1 modified)

## Accomplishments
- `AddPdf` registers parser, cascade engine, CSS policy, image decoder, resource resolver, IPdfWriter→PdfSharpCoreWriter, IMPdfService→MPdfService — all via `TryAddSingleton` (idempotent, SC1).
- `PdfConfigs` bound from the `"PdfConfigs"` section via `BindConfiguration`; `.Validate(...).ValidateOnStart()` enforces all seven limits > 0 — `MaxPages: 0` now throws at host build, before any render (SC5).
- `PdfTelemetryDescriptor` registered via `TryAddEnumerable(ServiceDescriptor.Singleton<ITelemetryDescriptor, …>)` so OtelSetup discovers the activity source and meter (TEL-01).
- No default `IFontResolver`; `MPdfService`'s optional ctor param resolves to null.

## Task Commits

1. **Task 1: PdfServiceCollectionExtensions.AddPdf** - `5995b62` (feat)

## Files Created/Modified
- `src/Muonroi.Pdf/Extensions/PdfServiceCollectionExtensions.cs` - AddPdf extension: options validation + default service registrations
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` - added Muonroi.Pdf.Governance ProjectReference

## Decisions Made
- Committed the Governance `ProjectReference` (csproj) together with the extension in the single Task 1 commit — the reference is required for the file to compile, so splitting would produce a non-building intermediate commit.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Added Muonroi.Pdf.Governance ProjectReference**
- **Found during:** Task 1
- **Issue:** `Muonroi.Pdf.csproj` did not reference `Muonroi.Pdf.Governance`, so `AngleSharpHtmlParser`/`AngleSharpCascadeEngine`/`DefaultStrictPolicy` were not visible. The plan anticipated this (context §"What exists") and authorized adding the reference; layering direction (Pdf → Governance) is correct.
- **Fix:** Added `<ProjectReference Include="..\Muonroi.Pdf.Governance\Muonroi.Pdf.Governance.csproj" />` (no Version, CPM-compliant).
- **Files modified:** src/Muonroi.Pdf/Muonroi.Pdf.csproj
- **Verification:** Build 0 errors; all 63 tests green.
- **Committed in:** 5995b62

**2. [Note] Const-only PdfLimits escape hatch was moot**
- The plan's caveat about const-only `PdfLimits` did not apply: `PdfConfigs.Limits` is a settable instance property and `MaxPages` (etc.) are settable `int` properties (backstop `Defaults` static). Binding `MaxPages: 0` reaches the validation lambda as expected. SC5 fully satisfied; no Abstractions change needed.

---

**Total deviations:** 1 auto-fixed (1 blocking) + 1 informational note
**Impact on plan:** Blocking fix was plan-authorized. No scope creep.

## Issues Encountered
None. The internal default types (`PureImageDecoder`, `ThrowingResourceResolver`, `PdfSharpCoreWriter`, `MPdfService`, `PdfTelemetryDescriptor`) live under `Muonroi.Pdf.Internal.*` and are visible because the extension is in the same assembly. All default ctors are parameterless / DI-resolvable.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- A consuming host can now call `services.AddPdf(configuration)` to wire the full pipeline.
- Idempotency confirmed by `TryAdd*` usage; `ValidateOnStart()` confirmed wired in the validation chain.
- Ready for integration/end-to-end host tests.

---
*Phase: 06-di-telemetry-integration*
*Completed: 2026-05-27*
