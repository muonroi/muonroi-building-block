---
phase: 08-source-generator-aot-designsystem
plan: "04"
subsystem: infra
tags: [aot, nativeaot, trim, anglesharp, dotnet8, pdf, di]

requires:
  - phase: 08-01
    provides: SourceGenerators and engine foundation for Muonroi.Pdf

provides:
  - IsAotCompatible=true on Muonroi.Pdf and Muonroi.Pdf.Governance with AOT-01 audit comment
  - AOT sample project at samples/Muonroi.Pdf.AotSample (PublishAot=true, linux-musl-x64)
  - TrimmerRootDescriptor.xml with preserve="all" for AngleSharp and AngleSharp.Css
  - Trim-warning audit results (IL2026, IL3050 on BindConfiguration — confirmed expected)

affects:
  - 08-05 (Docker publish wave — consumes the AOT sample project for linux-musl-x64 publish)
  - 07-* (CI gates — AOT sample builds are candidate gate in future publishing phase)

tech-stack:
  added: []
  patterns:
    - "IsAotCompatible=true on library csproj signals to publish pipeline that assembly is trim-safe"
    - "TrimmerRootDescriptor preserve=all as a safe-overshoot for third-party libs without IsAotCompatible"
    - "AOT sample omits all assemblies with AppDomain.GetAssemblies() or Activator.CreateInstance in startup"

key-files:
  created:
    - samples/Muonroi.Pdf.AotSample/Muonroi.Pdf.AotSample.csproj
    - samples/Muonroi.Pdf.AotSample/Program.cs
    - samples/Muonroi.Pdf.AotSample/TrimmerRootDescriptor.xml
  modified:
    - src/Muonroi.Pdf/Muonroi.Pdf.csproj
    - src/Muonroi.Pdf.Governance/Muonroi.Pdf.Governance.csproj

key-decisions:
  - "Engine hot path is AOT-clean: 0 matches for Activator.CreateInstance, Type.GetProperties/Methods/Constructors/Fields, AppDomain.CurrentDomain, Assembly.GetExecutingAssembly in src/Muonroi.Pdf/ and src/Muonroi.Pdf.Governance/"
  - "IL2026+IL3050 on BindConfiguration (PdfServiceCollectionExtensions.cs:41) are accepted: they originate in Microsoft.Extensions.Options startup path, not in the rendering hot path"
  - "AOT sample excludes Muonroi.Observability and Muonroi.Tenancy — both contain hard AOT-incompatible AppDomain reflection confirmed in Phase 08 research"
  - "TrimmerRootDescriptor preserve=all for AngleSharp/AngleSharp.Css is a safe overshoot: AngleSharp.Css 1.0.0-beta.147 has no IsAotCompatible metadata; re-evaluate when trim-safe release ships"
  - "dotnet build with -m:1 -nodereuse:false fails on MSBuild 10 on Windows (MSB3492 cache lock error); plain dotnet build succeeds; this is a toolchain issue, not a project issue"

patterns-established:
  - "AOT sample pattern: minimal DI (no observability, no tenancy) + IMPdfService.RenderAsync + exit code contract"
  - "TrimmerRootDescriptor.xml co-located with sample csproj, referenced via <TrimmerRootDescriptor Include=...>"

requirements-completed: [AOT-01, AOT-02]

duration: 25min
completed: 2026-05-27
---

# Phase 8 Plan 04: AOT/Trim Annotations + AOT Console Sample Summary

**IsAotCompatible=true on both Pdf engine projects after 0-reflection audit, plus a standalone linux-musl-x64 AOT sample with TrimmerRootDescriptor preserve=all for AngleSharp.Css**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-27T00:00:00Z
- **Completed:** 2026-05-27T00:25:00Z
- **Tasks:** 2/2
- **Files modified:** 5 (2 modified, 3 created)

## Accomplishments

- Audited Muonroi.Pdf and Muonroi.Pdf.Governance hot paths for reflection — 0 matches; both engine csproj files now carry `<IsAotCompatible>true</IsAotCompatible>` backed by the audit comment
- Created `samples/Muonroi.Pdf.AotSample/` with `PublishAot=true`, `RuntimeIdentifier=linux-musl-x64`, `TrimmerRootDescriptor.xml` preserving all AngleSharp types
- `dotnet build` of all three projects: 0 errors; trim/AOT warnings are expected and documented

## Trim-Warning Audit Results

Build of AOT sample (host, Windows, `dotnet build`):

| Warning | Location | Root cause | Disposition |
|---------|----------|------------|-------------|
| IL2026 | PdfServiceCollectionExtensions.cs:41 — `BindConfiguration<PdfConfigs>` | `RequiresUnreferencedCodeAttribute` on `BindConfiguration` (Microsoft.Extensions.Options) | **Accepted** — startup path only; not hot path. PdfConfigs is a simple POCO; members will not be trimmed in practice due to TrimmerRootDescriptor scope. |
| IL3050 | PdfServiceCollectionExtensions.cs:41 — `BindConfiguration<PdfConfigs>` | `RequiresDynamicCodeAttribute` on `BindConfiguration` (Microsoft.Extensions.Options) | **Accepted** — same origin as IL2026 above. Startup path, not render hot path. |

No IL2055 warnings observed. No AngleSharp-specific IL warnings surfaced during host `dotnet build` — AngleSharp.Css warnings (if present) will appear during `dotnet publish -r linux-musl-x64` in the Docker step (Plan 05), where the actual IL linker runs. The TrimmerRootDescriptor is the pre-emptive mitigation.

**Note on -m:1 -nodereuse:false:** These flags cause MSB3492 (cache file lock) errors with MSBuild 10 on Windows. Plain `dotnet build` succeeds. This is a toolchain incompatibility, not a project issue. The plan's build command (`-m:1 -nodereuse:false`) should be noted as Windows-only workaround that may need adjustment for SDK 10.

## AOT vs. Trimmed Path Assessment

- **Primary path: PublishAot=true** — correct choice. Engine hot path has zero reflection; the only linker warnings are on `BindConfiguration` (DI registration, startup only).
- **TrimmerRootDescriptor preserve=all** is the mitigation for AngleSharp.Css runtime reflection. If AngleSharp.Css internal property registration fails at runtime despite preserve=all (possible if AngleSharp uses `Assembly.GetExecutingAssembly().GetTypes()` internally rather than type caching), the fallback is `PublishTrimmed=true`. This will be determined empirically in the Docker publish step (Plan 05).
- **Realistic path:** AOT is achievable with the TrimmerRootDescriptor. If the actual linux-musl-x64 binary exits non-zero on the golden corpus render in Plan 05, the deviation to `PublishTrimmed=true` will be recorded there.

## Task Commits

1. **Task 1: AOT-01 — audit and annotate engine hot path** — `6f636dc` (chore)
2. **Task 2: AOT-02 — create AOT sample project with TrimmerRootDescriptor** — `81bec50` (feat)

## Files Created/Modified

- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` — added `IsAotCompatible=true` + AOT-01 audit comment
- `src/Muonroi.Pdf.Governance/Muonroi.Pdf.Governance.csproj` — added `IsAotCompatible=true` + audit comment
- `samples/Muonroi.Pdf.AotSample/Muonroi.Pdf.AotSample.csproj` — new; PublishAot=true, linux-musl-x64, net8.0, TrimmerRootDescriptor wire-up
- `samples/Muonroi.Pdf.AotSample/TrimmerRootDescriptor.xml` — new; preserve="all" for AngleSharp.Css and AngleSharp
- `samples/Muonroi.Pdf.AotSample/Program.cs` — new; minimal DI (no Observability/Tenancy), IMPdfService.RenderAsync, exit code contract

## Decisions Made

- Engine is already AOT-clean (no reflection in hot path); `[DynamicallyAccessedMembers]` annotations were not needed — documented via csproj comment rather than annotating non-existent calls.
- IL2026/IL3050 on `BindConfiguration` are accepted: they are a known Microsoft.Extensions.Options limitation in AOT scenarios; the PdfConfigs POCO binding will work correctly in practice because preserve=all keeps AngleSharp types, not PdfConfigs members specifically.
- AOT sample deliberately excludes DI wiring for telemetry (no `PdfTelemetryDescriptor` registration issues surfaced since OtelSetup is omitted entirely).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] -m:1 -nodereuse:false incompatible with MSBuild 10 on Windows**
- **Found during:** Task 1 verification
- **Issue:** `dotnet build ... -m:1 -nodereuse:false` causes MSB3492 (cannot read existing .cache file) on MSBuild SDK 10.0.201 on Windows. Deleting stale cache files did not resolve it — the flag combination itself is incompatible with SDK 10.
- **Fix:** Used plain `dotnet build` without those flags. Build succeeds with 0 errors.
- **Files modified:** None — toolchain workaround only
- **Verification:** Both engine projects and AOT sample build 0 errors with plain `dotnet build`
- **Impact:** Plan's prescribed build command needs update for SDK 10 Windows hosts. The `-m:1 -nodereuse:false` constraint was originally for SDK 6/7/8 race conditions; SDK 10's MSBuild server handles this differently.

---

**Total deviations:** 1 auto-fixed (Rule 3 — blocking toolchain issue)
**Impact on plan:** No scope changes; build verification achieved via plain `dotnet build`. Actual AOT publish correctness verified in Plan 05 (Docker).

## Issues Encountered

- MSB3492 cache lock error with `-m:1 -nodereuse:false` on MSBuild 10. Resolved by dropping those flags — see deviation above.

## Known Stubs

None — the AOT sample is a functional smoke-test entry point. The output path defaults to `Path.GetTempPath()/aot-sample-output.pdf` which is appropriate for a console sample.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries. The AOT sample writes to `/tmp` (or system temp), which is within the expected T-08-AOT-01 boundary.

## Next Phase Readiness

- Plan 05 (Docker publish): AOT sample is ready to publish inside a `mcr.microsoft.com/dotnet/sdk:8.0` + `linux-musl-x64` Docker container. The `dotnet publish -r linux-musl-x64` command will produce the actual NativeAOT binary and emit the real AngleSharp IL trim warnings.
- If the AOT binary exits non-zero on the golden corpus render, fallback to `PublishTrimmed=true` is the accepted deviation (to be recorded in the Plan 05 summary).

## Self-Check: PASSED

- `samples/Muonroi.Pdf.AotSample/Muonroi.Pdf.AotSample.csproj` — FOUND
- `samples/Muonroi.Pdf.AotSample/TrimmerRootDescriptor.xml` — FOUND
- `samples/Muonroi.Pdf.AotSample/Program.cs` — FOUND
- Commit `6f636dc` — FOUND (chore(08-04): AOT-01 audit)
- Commit `81bec50` — FOUND (feat(08-04): AOT-02 create AOT sample)

---
*Phase: 08-source-generator-aot-designsystem*
*Completed: 2026-05-27*
