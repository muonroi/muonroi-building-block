---
phase: 06-di-telemetry-integration
plan: 01
subsystem: telemetry
tags: [opentelemetry, activitysource, meter, diagnostics, csharp]

# Dependency graph
requires:
  - phase: 01-abstractions-contracts
    provides: PdfTelemetryNames string constants, Muonroi.Core.Abstractions.ITelemetryDescriptor
provides:
  - PdfTelemetryDescriptor (ITelemetryDescriptor) for OtelSetup auto-discovery
  - PdfMetrics static ActivitySource + Meter + Counter<long> + Histogram<int>
affects: [06-02-otel-registration, 06-03-mpdfservice-instrumentation]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Telemetry singletons (ActivitySource/Meter/Counter/Histogram) are static readonly, never disposed"
    - "Net8.0-bound telemetry contract types live in the net8.0 engine, not the netstandard2.0 Abstractions project"

key-files:
  created:
    - src/Muonroi.Pdf/Internal/Telemetry/PdfTelemetryDescriptor.cs
    - src/Muonroi.Pdf/Internal/Telemetry/PdfMetrics.cs
  modified:
    - src/Muonroi.Pdf/Muonroi.Pdf.csproj

key-decisions:
  - "PdfTelemetryDescriptor hosted in the net8.0 engine (not netstandard2.0 Abstractions) because ITelemetryDescriptor is defined in net8.0 Muonroi.Core.Abstractions and a netstandard2.0 project cannot reference it"
  - "Descriptor is public sealed (reflection discovery via Activator.CreateInstance requires a public, non-abstract, parameterless type)"

patterns-established:
  - "Pattern 1: PDF telemetry primitives live under src/Muonroi.Pdf/Internal/Telemetry/"
  - "Pattern 2: Meter name equals activity-source name by repo convention (Muonroi.BuildingBlock.Pdf)"

requirements-completed: [TEL-01, TEL-02, TEL-03, TEL-04, DI-04]

# Metrics
duration: 8min
completed: 2026-05-27
---

# Phase 6 Plan 01: Telemetry Building Blocks Summary

**PdfTelemetryDescriptor discovery token plus the static PdfMetrics ActivitySource, pdf.operation counter, and pdf.page_count histogram — all named `Muonroi.BuildingBlock.Pdf` and ready for OtelSetup wiring in Wave 2.**

## Performance

- **Duration:** ~8 min
- **Tasks:** 2
- **Files modified:** 3 (2 created, 1 modified)

## Accomplishments
- `PdfTelemetryDescriptor` implements `ITelemetryDescriptor`; both `ActivitySourceNames` and `MeterNames` return `["Muonroi.BuildingBlock.Pdf"]`; public sealed with implicit parameterless ctor for `OtelSetup` reflection discovery (`AppDomain.CurrentDomain.GetAssemblies()` scan).
- `PdfMetrics` exposes a static `ActivitySource Source`, a private static `Meter`, `Counter<long> OperationCounter` (`pdf.operation`), and `Histogram<int> PageCountHistogram` (`pdf.page_count`) — all `static readonly`, never disposed (threat T-06-02).
- Engine builds 0 errors; full suite stays green at 63/63.

## Task Commits

1. **Task 1: PdfTelemetryDescriptor** - `44595a9` (feat)
2. **Task 2: PdfMetrics static telemetry** - `7e8dde4` (feat)

## Files Created/Modified
- `src/Muonroi.Pdf/Internal/Telemetry/PdfTelemetryDescriptor.cs` - ITelemetryDescriptor discovery token
- `src/Muonroi.Pdf/Internal/Telemetry/PdfMetrics.cs` - Static ActivitySource + Meter + counter + histogram
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` - Added Muonroi.Core.Abstractions ProjectReference (home of ITelemetryDescriptor)

## Decisions Made
- Hosted the descriptor in the engine rather than Abstractions (see deviation). OtelSetup's discovery is an `AppDomain` assembly scan, so the engine assembly being loaded by any consumer is sufficient for registration — location does not affect discoverability.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] PdfTelemetryDescriptor relocated from netstandard2.0 Abstractions to the net8.0 engine**
- **Found during:** Task 1
- **Issue:** The plan placed `PdfTelemetryDescriptor` in `Muonroi.Pdf.Abstractions` (netstandard2.0) with a ProjectReference to `Muonroi.Core.Abstractions`. But `Muonroi.Core.Abstractions` (home of `ITelemetryDescriptor`) targets **net8.0**, and a netstandard2.0 project cannot reference a net8.0 assembly. The plan's line 114 explicitly directed surfacing this mismatch rather than forcing the reference (and the orchestrator forbade retargeting Abstractions). The only working precedent, `Muonroi.Caching.Abstractions`, is itself net8.0.
- **Fix:** Created the descriptor in the net8.0 engine project at `src/Muonroi.Pdf/Internal/Telemetry/PdfTelemetryDescriptor.cs` (namespace `Muonroi.Pdf.Internal.Telemetry`) and added a `Muonroi.Core.Abstractions` ProjectReference to the engine csproj. Kept it `public sealed` so `Activator.CreateInstance` reflection discovery works. The note about the now-unneeded "const-only PdfLimits escape hatch" was already moot per prerequisite a208b7c.
- **Files modified:** src/Muonroi.Pdf/Internal/Telemetry/PdfTelemetryDescriptor.cs, src/Muonroi.Pdf/Muonroi.Pdf.csproj
- **Verification:** Engine builds 0 errors; 63/63 tests pass.
- **Committed in:** `44595a9`

---

**Total deviations:** 1 auto-fixed (1 blocking, framework-targeting constraint)
**Impact on plan:** Required by the netstandard2.0/net8.0 boundary. No scope creep — same type, same contract, discoverable identically; only the host assembly changed. No new NuGet packages (BCL + existing project refs only), so Directory.Packages.props was untouched.

## Issues Encountered
- Build/test run via PowerShell with `-m:1 -nodereuse:false` per the documented Windows obj-deletion race. Both projects build 0 errors.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- `PdfTelemetryDescriptor` and `PdfMetrics` are in place. Wave 2 (06-02) can wire OtelSetup discovery; 06-03 can reference `PdfMetrics.Source` to start render activities.
- No blockers. Honored existing contracts: Abstractions stays netstandard2.0, PdfLimits.Defaults backstop untouched, no parallel telemetry system introduced.

## Self-Check: PASSED
- FOUND: src/Muonroi.Pdf/Internal/Telemetry/PdfTelemetryDescriptor.cs
- FOUND: src/Muonroi.Pdf/Internal/Telemetry/PdfMetrics.cs
- FOUND commit 44595a9, FOUND commit 7e8dde4

---
*Phase: 06-di-telemetry-integration*
*Completed: 2026-05-27*
