# Warnings Cleanup Report — Muonroi.BuildingBlock.sln

**Branch:** develop
**Goal:** Drive the solution to zero actionable compiler/analyzer warnings (excluding RS1038 and xUnit1030, already NoWarn'd) so a global `TreatWarningsAsErrors` ship gate can be enabled.

## Result

- Full clean build (`dotnet build Muonroi.BuildingBlock.sln -c Debug --no-incremental`): **0 actionable warnings, 0 errors.**
- The 16 (raw 32) remaining warnings reported in the summary are **all RS1038**, sourced only from the two Roslyn-component projects (`Muonroi.CodeStandards`, `Muonroi.RuleEngine.SourceGenerators`), which already carry `<NoWarn>RS1038</NoWarn>` via the `IsRoslynComponent` condition in `Directory.Build.props`. RS1038 is explicitly out of scope per the task.
- Test suite: **62 assemblies, 0 failed** (sequential `-m:1` run; ~3586 tests including multi-target double-counts).

## Verbatim final build summary

```
Build succeeded.
    16 Warning(s)
    0 Error(s)
```
(The 16 Warning(s) = RS1038 only, deduplicated from 32 raw occurrences across the dependency graph; excluded by task scope.)

## Verbatim warning-code breakdown of final build

```
Name   Count
----   -----
RS1038    32
```
No CS / CA / IL / MBB / NU warnings remain.

## Test result

First (parallel) `dotnet test` pass hit transient `CS0006: Metadata file ...obj/Debug/net8.0/ref/*.dll could not be found` build-race errors on 2 projects (RuleEngine.Runtime.Web.Tests, Pdf.Enterprise chain) — the documented nested/parallel-build flake (`test_flakiness_nested_build.md`), not a code defect. Re-run sequentially (`dotnet test Muonroi.BuildingBlock.sln -c Debug -m:1`):

```
Passed! assemblies: 62
Failed! lines:       0
CS0006 errors:       0
```

All tests green. Doc/warning fixes did not affect any test.

## Commits (in order, newest last)

| Hash | Scope |
|------|-------|
| `e5ce516e` | docs(pdf-abstractions): XML docs for public API (CS1591/CS1574) |
| `db164fc3` | docs(pdf): Pdf.Governance/Enterprise/SourceGen docs + IL2026/IL3050 annotations |
| `428b476d` | docs(integration-connectors): preset connector docs (CS1591/CS1573/CS1734) |
| `250b02e2` | docs(rule-engine): NRules docs, NU1504, NU5100, CS8669 |
| `97b21670` | fix(core): AspNetCore/Data.Dapper crefs (CS1584/CS1658/CS1574/CS1734) + CA2255 module-init |
| `a4111853` | fix(samples): quickstart doc/obsolete/guard warnings (CS0618/CS1573/CS1574/CS1734/MBB010) |
| `8f57fa84` | fix(pdf-tests): CA1416 pragma on platform-gated SavePng |

## Per-project warning categories fixed (~170 actionable)

- **Muonroi.Pdf.Abstractions** (~61 CS1591 + 1 CS1574): documented all engine contracts, value records, exception hierarchy, PdfConfigs/PdfLimits; fixed `FontWeight.Regular` -> `FontWeight.Normal` cref.
- **Muonroi.Integration.Connectors** (~34 CS1591 + CS1573 + CS1734): documented 8 preset connectors; added `<param name="logger">` on HttpConnector; relocated misplaced `<paramref name="html">` to HasVisibleText.
- **Muonroi.RuleEngine.NRules** (~22 CS1591): documented contributor, controller, DTOs, engine ctor.
- **Muonroi.Pdf.Governance** (~17 CS1591 + CS1734): documented parser/cascade/policies; moved doc comment before `[SuppressMessage]` on AngleSharpHtmlParser; removed invalid `<paramref name="signatureVerifier">` on a method (it is a primary-ctor param).
- **Muonroi.Pdf.Enterprise** (CS1591): documented FeatureNotLicensedException ctor, PngDecoder, SsimScorer; reordered doc/attribute trivia.
- **Muonroi.Pdf** (CS1591 generated + IL2026/IL3050): added docs to the emitted `PdfTemplateAttribute` template (in `Muonroi.Pdf.SourceGenerators`); annotated `AddPdf` with `[RequiresUnreferencedCode]`/`[RequiresDynamicCode]` for `BindConfiguration`.
- **Muonroi.AspNetCore** (CS1584/CS1658/CS1574): fixed the generic `AddDbContextConfigure` cref — backtick arity (`` ``2``) is invalid in a cref literal; replaced with `{TDbContext,TPermission}(IServiceCollection, IConfiguration, bool, string)`.
- **Muonroi.Data.Dapper** (CS1734): replaced invalid `<paramref name="services">` in a class-level `<remarks>` with `<c>services</c>`.
- **Muonroi.Experience.Runtime** (CS1574): fully qualified `FileExperienceStore` cref in FileExperienceArchive.
- **Muonroi.BackgroundJobs.Quartz / Hangfire** (CA2255): justified `[SuppressMessage]` on intentional `[ModuleInitializer]` provider registration.
- **Muonroi.RuleEngine.SourceGenerators** (CS8669): emit a nullable-enable directive at the top of generated rule files.
- **Samples** (CS0618/CS1573/CS1574/CS1734/MBB010): Quickstart.BackgroundJobs obsolete `UseHangfireServer` pragma; Quickstart.Data.EF MDbContext cref; Quickstart.Integration `ct` params; Quickstart.Messaging `cancellationToken` params + invalid paramref; RuleSourceGen MGuard.NotNull.
- **tests/Muonroi.Pdf.Tests** (CA1416): pragma around platform-gated `Conversion.SavePng`.

## Per-project NoWarn added (with reason)

- **`src/Muonroi.RuleEngine.Testing/Muonroi.RuleEngine.Testing.csproj`**: `<NoWarn>$(NoWarn);NU5100</NoWarn>` — `testhost.dll` is packed as a content/tooling asset (test-authoring helper package), intentionally not under `lib/`; it is not meant to be a compile reference. Scoped to this one project only.

## Concern: TreatWarningsAsErrors vs RS1038 NoWarn

A test build with `dotnet build ... /p:TreatWarningsAsErrors=true` promotes RS1038 to **errors** despite the per-project `<NoWarn>RS1038</NoWarn>`, because a command-line-global `TreatWarningsAsErrors` is evaluated after `<NoWarn>` in the current SDK (10.0.201) and re-promotes suppressed codes. To enable the ship gate cleanly, the gate must use `<WarningsNotAsErrors>RS1038;xUnit1030</WarningsNotAsErrors>` (which DOES survive the global TWAE) rather than relying solely on `<NoWarn>`. That change belongs in `Directory.Build.props`, which is **out of scope for this task** (explicitly must not be touched/committed). The plain verification build — the command specified by the task — reports 0 actionable warnings, and the `0 Warning(s)` line under TWAE confirms every non-RS1038 warning is resolved.

## Files excluded from staging (per instructions)

- `Directory.Build.props` (modified, left unstaged)
- `.superpowers/` (untracked, left unstaged)
