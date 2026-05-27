---
phase: 08-source-generator-aot-designsystem
plan: 01
subsystem: source-generators
tags: [roslyn, incremental-generator, source-generator, pdf, iincrementalgenerator, netstandard2.0, cpm, di-extension, template-inlining]

# Dependency graph
requires:
  - phase: 01-abstractions-contracts
    provides: IMPdfRenderer<TModel>, IMPdfService, PdfRenderOptions, PdfRenderResult in Muonroi.Pdf.Abstractions
provides:
  - Muonroi.Pdf.SourceGenerators project (netstandard2.0, IsRoslynComponent=true)
  - PdfTemplateAttribute in Muonroi.Pdf.Abstractions namespace
  - PdfTemplateGenerator IIncrementalGenerator using ForAttributeWithMetadataName
  - Compile-time renderer emission — sealed {TypeName}PdfRenderer implementing IMPdfRenderer<TModel>
  - HTML template inlined as C# string interpolation ({{Token}} -> {model.Token})
  - AddPdfRenderer{TypeName}(IServiceCollection) DI extension via TryAddSingleton
  - Muonroi.Pdf.csproj wired as analyzer consumer (OutputItemType=Analyzer, ReferenceOutputAssembly=false)
  - SG unit test project (net9.0) with 2 passing tests
affects: [08-02, 08-03, 08-04, 08-05, consumers of Muonroi.Pdf]

# Tech tracking
tech-stack:
  added:
    - Muonroi.Pdf.SourceGenerators (new project, netstandard2.0)
    - Muonroi.Pdf.SourceGenerators.Tests (new project, net9.0)
    - Microsoft.CodeAnalysis.CSharp 4.13.0 (PrivateAssets=all, already in CPM)
    - Microsoft.CodeAnalysis.Analyzers 3.11.0 (PrivateAssets=all, already in CPM)
  patterns:
    - ForAttributeWithMetadataName preferred over CreateSyntaxProvider for attribute-gated SG (Roslyn perf best practice)
    - RegisterPostInitializationOutput to inject attribute type into consumer compilations
    - IsExternalInit polyfill for record types on netstandard2.0 (matches Pdf.Abstractions pattern)
    - Template tokens {{Word}} rewritten to {model.Word} in C# interpolated string at compile time
    - Non-token { } chars doubled to {{ }} to prevent C# interpolation syntax breakage (T-08-01 mitigation)
    - TryAddSingleton for DI extension — cannot override existing registration (T-08-02 accept)
    - SG test: exclude attribute stub from test compilation; SG injects it via RegisterPostInitializationOutput

key-files:
  created:
    - src/Muonroi.Pdf.SourceGenerators/Muonroi.Pdf.SourceGenerators.csproj
    - src/Muonroi.Pdf.SourceGenerators/PdfTemplateGenerator.cs
    - src/Muonroi.Pdf.SourceGenerators/PdfTemplateGeneratorDiagnostics.cs
    - src/Muonroi.Pdf.SourceGenerators/IsExternalInit.cs
    - src/Muonroi.Pdf.SourceGenerators/AnalyzerReleases.Shipped.md
    - src/Muonroi.Pdf.SourceGenerators/AnalyzerReleases.Unshipped.md
    - src/Muonroi.Pdf.Abstractions/PdfTemplateAttribute.cs
    - tests/Muonroi.Pdf.SourceGenerators.Tests/Muonroi.Pdf.SourceGenerators.Tests.csproj
    - tests/Muonroi.Pdf.SourceGenerators.Tests/PdfTemplateGeneratorTests.cs
  modified:
    - src/Muonroi.Pdf/Muonroi.Pdf.csproj (added Analyzer ProjectReference to SG)

key-decisions:
  - "IsRoslynComponent=true, GenerateDependencyFile=false, no IncludeBuildOutput=false — mirrors Tenancy.SiteProfile.SourceGenerators.csproj pattern; IncludeBuildOutput=false breaks P2P analyzer resolution"
  - "ForAttributeWithMetadataName used (not CreateSyntaxProvider) — required by plan and Roslyn incremental SG best practice for attribute-gated discovery"
  - "PdfTemplateAttributeStub excluded from SG test compilation — including it alongside RegisterPostInitializationOutput output causes duplicate type ambiguity and ForAttributeWithMetadataName fails to match"
  - "IsExternalInit polyfill added to SG project — required for C# record types on netstandard2.0 target"
  - "AnalyzerReleases.Shipped.md and AnalyzerReleases.Unshipped.md added — silences RS2008 release-tracking warnings"
  - "Token rewriting: {{Word}} substituted at compile time; lone { } escaped as {{ }} — satisfies T-08-01 tamper mitigation"

patterns-established:
  - "SG test pattern: use CSharpGeneratorDriver.Create directly; do not include attribute stubs the SG emits via RegisterPostInitializationOutput"
  - "Analyzer SG consumer wiring: OutputItemType=Analyzer + ReferenceOutputAssembly=false in ProjectReference"
  - "Template inlining: $@\"...\" verbatim interpolated string; null templateResourceName -> stub empty string renderer"

requirements-completed: [SG-01, SG-03]

# Metrics
duration: 35min
completed: 2026-05-27
---

# Phase 8 Plan 01: Muonroi.Pdf.SourceGenerators Summary

**IIncrementalGenerator emitting sealed IMPdfRenderer<TModel> classes with HTML template inlined as C# string interpolation at compile time, plus DI extension methods, wired into Muonroi.Pdf as OutputItemType=Analyzer**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-05-27T00:00:00Z
- **Completed:** 2026-05-27T00:35:00Z
- **Tasks:** 2
- **Files modified:** 10 (9 created, 1 modified)

## Accomplishments
- Scaffolded Muonroi.Pdf.SourceGenerators (netstandard2.0, IsRoslynComponent=true, GenerateDependencyFile=false, no IncludeBuildOutput=false)
- Implemented PdfTemplateGenerator using ForAttributeWithMetadataName to discover [PdfTemplate] classes — emits renderer + DI extension per decorated model
- PdfTemplateAttribute added to Muonroi.Pdf.Abstractions namespace; also emitted into consumer compilations via RegisterPostInitializationOutput
- Template HTML inlined as C# string interpolation — {{Token}} rewritten to {model.Token} at compile time, no runtime resource loading
- T-08-01 mitigation: only \w+ tokens substituted; lone { } escaped to {{ }}
- Muonroi.Pdf.csproj wired as analyzer consumer with OutputItemType=Analyzer ReferenceOutputAssembly=false
- SG unit test project (net9.0) with 2 passing xunit tests

## Task Commits

1. **Task 1: Scaffold Muonroi.Pdf.SourceGenerators project + PdfTemplateAttribute** - `4711ffb` (feat)
2. **Task 2: Implement PdfTemplateGenerator + wire Muonroi.Pdf + SG tests** - `2c3e9bd` (feat)

## Files Created/Modified
- `src/Muonroi.Pdf.SourceGenerators/Muonroi.Pdf.SourceGenerators.csproj` - netstandard2.0 Roslyn SG project
- `src/Muonroi.Pdf.SourceGenerators/PdfTemplateGenerator.cs` - IIncrementalGenerator implementation
- `src/Muonroi.Pdf.SourceGenerators/PdfTemplateGeneratorDiagnostics.cs` - PDFSG0001 (not partial) + PDFSG0002 (empty templateId)
- `src/Muonroi.Pdf.SourceGenerators/IsExternalInit.cs` - polyfill for record types on netstandard2.0
- `src/Muonroi.Pdf.SourceGenerators/AnalyzerReleases.Shipped.md` + `AnalyzerReleases.Unshipped.md` - RS2008 compliance
- `src/Muonroi.Pdf.Abstractions/PdfTemplateAttribute.cs` - marker attribute with templateId + templateResourceName
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` - added Analyzer ProjectReference to SG
- `tests/Muonroi.Pdf.SourceGenerators.Tests/Muonroi.Pdf.SourceGenerators.Tests.csproj` - net9.0 test project
- `tests/Muonroi.Pdf.SourceGenerators.Tests/PdfTemplateGeneratorTests.cs` - 2 tests (renderer class + DI extension)

## Decisions Made
- **No IncludeBuildOutput=false**: confirmed by Tenancy SG comment — breaks P2P analyzer resolution; NuGet None Pack items handle packaging
- **ForAttributeWithMetadataName** over CreateSyntaxProvider: required by plan; also the Roslyn-recommended approach for attribute-based discovery
- **Attribute stub excluded from test compilation**: including the same attribute type that the SG injects via RegisterPostInitializationOutput causes duplicate type definitions; ForAttributeWithMetadataName cannot match and no renderer is generated
- **IsExternalInit polyfill**: required for `record` types on netstandard2.0; consistent with Muonroi.Pdf.Abstractions pattern

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Added AnalyzerReleases tracking files**
- **Found during:** Task 1 build
- **Issue:** RS2008 warnings about missing analyzer release tracking for PDFSG0001/PDFSG0002 — would leave build dirty
- **Fix:** Added AnalyzerReleases.Shipped.md and AnalyzerReleases.Unshipped.md mirroring Tenancy SG pattern
- **Files modified:** src/Muonroi.Pdf.SourceGenerators/AnalyzerReleases.Shipped.md, AnalyzerReleases.Unshipped.md
- **Verification:** Build produces 0 warnings for RS2008
- **Committed in:** 4711ffb (Task 1)

**2. [Rule 1 - Bug] Added IsExternalInit.cs polyfill**
- **Found during:** Task 2 build
- **Issue:** `record PdfTemplateModel` in SG project (netstandard2.0) fails with CS0518 — IsExternalInit not defined
- **Fix:** Added IsExternalInit.cs polyfill matching Muonroi.Pdf.Abstractions pattern
- **Files modified:** src/Muonroi.Pdf.SourceGenerators/IsExternalInit.cs
- **Verification:** SG builds 0 errors
- **Committed in:** 2c3e9bd (Task 2)

**3. [Rule 1 - Bug] Removed PdfTemplateAttribute stub from test compilation**
- **Found during:** Task 2 test run (tests failing)
- **Issue:** Including the attribute stub alongside the SG's RegisterPostInitializationOutput output created duplicate type definitions; ForAttributeWithMetadataName could not match the attribute and no renderer was generated
- **Fix:** Removed attribute stub from test helper — SG injects the attribute via RegisterPostInitializationOutput, which is the correct approach
- **Files modified:** tests/Muonroi.Pdf.SourceGenerators.Tests/PdfTemplateGeneratorTests.cs
- **Verification:** Both tests pass (2/2)
- **Committed in:** 2c3e9bd (Task 2)

---

**Total deviations:** 3 auto-fixed (all Rule 1 — bugs/missing polyfill)
**Impact on plan:** All fixes essential for a clean build and passing tests. No scope creep.

## Issues Encountered
- `dotnet build ... -q` exits with "Question build FAILED" message on Windows with SDK 10.0.201 when obj/ directories do not pre-exist — this is an MSBuild question-mode artifact from the -q flag, not a real build failure. Verified by running without -q; actual build succeeded.

## Known Stubs
- Renderers for models with a non-null `templateResourceName` where the AdditionalText file is not found at build time emit `"" /* template file not found ... */` as the inlined HTML. This is intentional and documented behavior — the renderer is a valid stub; the template must be declared as `<AdditionalFiles>` in the consumer project.

## Self-Check

### Files exist:
- `src/Muonroi.Pdf.SourceGenerators/Muonroi.Pdf.SourceGenerators.csproj` — FOUND
- `src/Muonroi.Pdf.SourceGenerators/PdfTemplateGenerator.cs` — FOUND (contains ForAttributeWithMetadataName)
- `src/Muonroi.Pdf.Abstractions/PdfTemplateAttribute.cs` — FOUND
- `tests/Muonroi.Pdf.SourceGenerators.Tests/PdfTemplateGeneratorTests.cs` — FOUND

### Commits exist:
- `4711ffb` — FOUND (Task 1)
- `2c3e9bd` — FOUND (Task 2)

### Build/test status:
- Muonroi.Pdf.SourceGenerators: 0 errors, 0 warnings
- Muonroi.Pdf.Abstractions: 0 errors
- Muonroi.Pdf: 0 errors
- Tests: 2/2 PASSED

## Self-Check: PASSED

## Next Phase Readiness
- PdfTemplateGenerator ready for plan 08-02 (DesignSystem.Default templates wired as AdditionalFiles consumers)
- DI extension pattern established — `AddPdfRenderer{TypeName}` can be called in AddPdf() wiring
- SG test pattern documented: exclude attribute stubs injected by RegisterPostInitializationOutput

---
*Phase: 08-source-generator-aot-designsystem*
*Completed: 2026-05-27*
