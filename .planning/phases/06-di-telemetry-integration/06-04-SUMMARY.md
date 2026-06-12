---
phase: 06-di-telemetry-integration
plan: 04
subsystem: testing
tags: [xunit, fluentassertions, dependency-injection, opentelemetry, pdfsharpcore, integration-tests]

requires:
  - phase: 06-di-telemetry-integration (06-01)
    provides: PdfTelemetryNames, PdfMetrics, PdfTelemetryDescriptor
  - phase: 06-di-telemetry-integration (06-02)
    provides: MPdfService end-to-end orchestrator (IMPdfService)
  - phase: 06-di-telemetry-integration (06-03)
    provides: AddPdf DI extension with options validation
provides:
  - Executable assertions for all five Phase 6 success criteria (SC1-SC5)
  - Shared PDF DI/integration test harness (no-op IMLog, fake ITenantContext, embedded-font resolver)
affects: [phase verification, future PDF pipeline regression coverage]

tech-stack:
  added:
    - Microsoft.Extensions.DependencyInjection (test PackageReference)
    - Microsoft.Extensions.Configuration (test PackageReference)
    - Microsoft.Extensions.Hosting (test PackageReference)
    - Microsoft.Extensions.Options (test PackageReference)
  patterns:
    - ActivityListener / MeterListener BCL capture for telemetry assertions
    - Host.CreateApplicationBuilder + ValidateOnStart for startup-validation tests
    - Non-parallel xunit collection to guard PdfSharpCore process-global font state

key-files:
  created:
    - tests/Muonroi.Pdf.Tests/Service/DependencyInjectionTests.cs
    - tests/Muonroi.Pdf.Tests/Service/ConfigValidationTests.cs
    - tests/Muonroi.Pdf.Tests/Service/MPdfServiceIntegrationTests.cs
    - tests/Muonroi.Pdf.Tests/Service/PdfServiceTestHarness.cs
    - tests/Muonroi.Pdf.Tests/Telemetry/PdfTelemetryDescriptorTests.cs
    - tests/Muonroi.Pdf.Tests/PdfRenderCollection.cs
  modified:
    - tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj
    - tests/Muonroi.Pdf.Tests/Writer/PdfWriterTests.cs
    - tests/Muonroi.Pdf.Tests/Writer/DeterminismTests.cs
    - tests/Muonroi.Pdf.Tests/Writer/SecurityTests.cs

key-decisions:
  - "PdfTelemetryDescriptorTests placed in Muonroi.Pdf.Tests (which references the engine) because PdfTelemetryDescriptor lives in the Muonroi.Pdf engine assembly, not Abstractions; the planned Muonroi.Pdf.Abstractions.Tests project does not exist and could not reference the descriptor."
  - "SC5 asserted via host.StartAsync() throwing OptionsValidationException using Host.CreateApplicationBuilder with limits fed through the host IConfiguration (a bare HostBuilder + TryAddSingleton<IConfiguration> is shadowed by the host's own IConfiguration and does not bind the override)."
  - "Hand-written no-op IMLog<MPdfService> double instead of NSubstitute: Castle dynamic proxy cannot proxy a generic interface closed over the internal sealed MPdfService type."
  - "Integration font's internal name table rewritten to a unique FontName to avoid PdfSharpCore's process-global FontFactory cache colliding with the writer tests embedding the same TestFont.ttf."

patterns-established:
  - "PdfServiceTestHarness: single source of valid config + test doubles for all PDF DI/integration tests"
  - "PdfRender xunit collection: serialize every writer-driving test class against PdfSharpCore global state"

requirements-completed: [DI-01, DI-02, DI-03, DI-04, PIPE-08, TEL-01, TEL-02, TEL-03, TEL-04]

duration: ~55min
completed: 2026-05-27
---

# Phase 6 Plan 04: Phase 6 Success-Criteria Integration Suite Summary

**Executable SC1-SC5 coverage: AddPdf idempotency, end-to-end %PDF-1.7 render with real glyphs, pdf.render activity + page-count/operation metrics, MaxRenderDurationMs cancellation, and PdfConfigs startup validation — all green alongside the existing 63 tests (73 total).**

## Performance

- **Duration:** ~55 min
- **Tasks:** 3
- **Files modified:** 10 (6 created, 4 modified)

## Accomplishments
- Converted each of the five Phase 6 success criteria into a dedicated, passing executable assertion.
- Proved the full DI-resolved pipeline renders valid PDF/1.7 with embedded glyphs on a headless (no-OS-font) build host.
- Captured live telemetry (ActivityListener + MeterListener) to assert snake_case tags and metric emission.
- Hardened the writer test suite against PdfSharpCore's process-global font state.

## Task Commits

1. **Task 1: DI registration + telemetry descriptor tests (SC1, TEL-01)** - `ac8da06` (test)
2. **Task 2: Startup validation test (SC5)** - `e71702a` (test)
3. **Task 3: End-to-end render + telemetry + timeout tests (SC2, SC3, SC4)** - `f35f71f` (test)
4. **Cross-test PdfSharpCore font-state isolation (deviation fix)** - `e2644bd` (test)

## SC → Test mapping

| SC | Test | Proves |
|----|------|--------|
| SC1 | `DependencyInjectionTests.AddPdf_CalledTwice_DoesNotDuplicate` (+ `AddPdf_RegistersAllPipelineServices`, `AddPdf_DoesNotRegisterFontResolver`) | AddPdf registers full pipeline, no default IFontResolver, single IMPdfService/IPdfWriter descriptor after two calls |
| SC2 | `MPdfServiceIntegrationTests.RenderAsync_ValidHtml_ProducesPdf17Stream` | Non-empty stream, first 8 bytes `%PDF-1.7`, PageCount>=1, ByteCount>0 |
| SC3 | `RenderAsync_EmitsActivityWithSnakeCaseTags` + `RenderAsync_RecordsPageCountHistogram` | Completed `pdf.render` activity (Ok) with `pdf.template_id` + `tenant.id`; `pdf.page_count` histogram == PageCount; `pdf.operation` counter +1 with `pdf.status=ok` |
| SC4 | `RenderAsync_ExceedsTimeout_ThrowsOperationCanceled` | 1 ms MaxRenderDurationMs + slow IHtmlParser stub throws OperationCanceledException (PIPE-08) |
| SC5 | `ConfigValidationTests.AddPdf_MaxPagesZero_ThrowsAtStartup` (+ `AddPdf_ValidLimits_StartsSuccessfully`) | MaxPages:0 throws OptionsValidationException at host StartAsync; valid limits start cleanly |
| TEL-01 | `PdfTelemetryDescriptorTests.Descriptor_HasParameterlessCtor_AndCorrectNames` | Descriptor exposes `Muonroi.BuildingBlock.Pdf` source+meter names, is ITelemetryDescriptor |

## Files Created/Modified
- `Service/PdfServiceTestHarness.cs` - valid config + no-op IMLog, fake ITenantContext, name-renamed embedded-font resolver
- `Service/DependencyInjectionTests.cs` - SC1 / DI-02 / DI-04
- `Service/ConfigValidationTests.cs` - SC5 startup validation
- `Service/MPdfServiceIntegrationTests.cs` - SC2 / SC3 / SC4 end-to-end
- `Telemetry/PdfTelemetryDescriptorTests.cs` - TEL-01
- `PdfRenderCollection.cs` - non-parallel collection for writer-driving tests
- `Muonroi.Pdf.Tests.csproj` - DI/Configuration/Hosting/Options PackageReferences
- `Writer/{PdfWriterTests,DeterminismTests,SecurityTests}.cs` - joined PdfRender collection

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] PdfTelemetryDescriptorTests relocated to Muonroi.Pdf.Tests**
- **Found during:** Task 1
- **Issue:** Plan placed the test in `tests/Muonroi.Pdf.Abstractions.Tests/` and asserted that project exists. It does not exist, and `PdfTelemetryDescriptor` lives in the **engine** assembly (`Muonroi.Pdf`, namespace `Muonroi.Pdf.Internal.Telemetry`), not Abstractions — an Abstractions-only test project could not reference it.
- **Fix:** Authored the test as `tests/Muonroi.Pdf.Tests/Telemetry/PdfTelemetryDescriptorTests.cs`; that project already references the engine.
- **Verification:** Test passes; asserts source/meter names and ITelemetryDescriptor assignability.
- **Committed in:** `ac8da06`

**2. [Rule 3 - Blocking] Hand-written no-op IMLog double instead of NSubstitute**
- **Found during:** Task 1
- **Issue:** `Substitute.For<IMLog<MPdfService>>()` throws at runtime — Castle DynamicProxy cannot generate a proxy for a generic interface closed over the internal sealed `MPdfService` (no InternalsVisibleTo to the proxy assembly).
- **Fix:** Added a hand-written `NoOpLog<T>` implementing `IMLog<T>` in the harness.
- **Committed in:** `ac8da06`

**3. [Rule 1 - Bug] Removed unrestorable Microsoft.Extensions.Configuration.Memory reference**
- **Found during:** Task 1 (build)
- **Issue:** That package is not available in the offline NuGet sources (NU1101). `AddInMemoryCollection` ships in `Microsoft.Extensions.Configuration` itself.
- **Fix:** Dropped the `.Memory` PackageReference.
- **Committed in:** `ac8da06`

**4. [Rule 3 - Blocking] SC5 host wiring**
- **Found during:** Task 2
- **Issue:** A bare `HostBuilder` did not run the `ValidateOnStart` validator, and a `TryAddSingleton<IConfiguration>` override was shadowed by the host's own IConfiguration, so the `MaxPages:0` override never bound.
- **Fix:** Used `Host.CreateApplicationBuilder()` and fed the limits via `builder.Configuration.AddConfiguration(config)`; `host.StartAsync()` now throws `OptionsValidationException` as required.
- **Committed in:** `e71702a`

**5. [Rule 1 - Bug] PdfSharpCore process-global font cache collision across test classes**
- **Found during:** Task 3 (full-suite run)
- **Issue:** PdfSharpCore caches font sources in a static `FontFactory` keyed by internal FontName. The writer tests embed the full `TestFont.ttf` ("Noto Sans Regular"); the integration render embeds a SUBSET of the same font under family `serif` — identical FontName, different bytes — so `FontFactory.CacheFontSource` threw "same key already added" (and a follow-on NullReferenceException) when both classes ran in the same process.
- **Fix:** The integration font resolver rewrites the font's internal `name` table to a unique, equal-length token (distinct FontName); additionally all writer-driving test classes were placed in a single non-parallel `PdfRender` collection to guard the once-per-process `GlobalFontSettings.FontResolver`.
- **Verification:** Full suite 73/73, stable across repeated runs.
- **Committed in:** `e2644bd`

---

**Total deviations:** 5 auto-fixed (3 blocking, 2 bug)
**Impact on plan:** All deviations were necessary to make the planned assertions compile, bind, and pass deterministically. No success criterion was weakened or skipped. No scope creep.

## Issues Encountered

- **SC2/SC3 "No appropriate font found":** the box tree assigns inline text the default family `serif` (block-level `font-family` is not inherited to synthesized inline text nodes in the current cascade). The integration HTML therefore declares the embedded face under `@font-face{font-family:serif}` so PdfSharp's resolver finds it, producing real glyphs. Documented inline in the test. This is a real glyph-producing render (not a proxy), satisfying SC2/SC3 fully.

## Next Phase Readiness
- All five Phase 6 success criteria are now locked behind executable tests; the phase verification gate is satisfied.
- No blockers. `PdfConfigs.PdfLimits` exposes settable instance properties, so RESEARCH Pitfall 3 (const-only limits) did not apply — SC5 is fully satisfied.

## Self-Check: PASSED

---
*Phase: 06-di-telemetry-integration*
*Completed: 2026-05-27*
