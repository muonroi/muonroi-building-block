---
phase: 08-source-generator-aot-designsystem
plan: "03"
subsystem: pdf-benchmarks
tags: [benchmarkdotnet, alloc-baseline, memorydiagnoser, sc4-baseline, sc2-harness]
dependency_graph:
  requires: []
  provides: [ALLOC-01-baseline, SG-02-harness]
  affects: [08-05-PLAN.md]
tech_stack:
  added:
    - BenchmarkDotNet 0.15.8 (CPM)
  patterns:
    - BDN MemoryDiagnoser open-generic IMLog<T> no-op for benchmarks
    - SystemFontResolver for headless PdfSharpCore font embedding
key_files:
  created:
    - benchmarks/Muonroi.Pdf.Benchmarks/Muonroi.Pdf.Benchmarks.csproj
    - benchmarks/Muonroi.Pdf.Benchmarks/PdfRenderBenchmarks.cs
    - benchmarks/Muonroi.Pdf.Benchmarks/Program.cs
    - benchmarks/Muonroi.Pdf.Benchmarks/reference-50kb.html
  modified:
    - Directory.Packages.props
decisions:
  - "Open-generic IMLog<T> registration (services.AddSingleton(typeof(IMLog<>), typeof(BenchmarkNoOpLog<>))) avoids InternalsVisibleTo requirement for MPdfService"
  - "SystemFontResolver reads arial.ttf from C:\\Windows\\Fonts so PdfSharpCore embeds a real font in the BDN child process context"
  - "reference-50kb.html copied to benchmark project directory and included as <Content CopyToOutputDirectory=Always> rather than EmbeddedResource"
metrics:
  duration: "~35 min (including full BDN warmup+measurement run)"
  completed_date: "2026-05-27"
---

# Phase 08 Plan 03: BenchmarkDotNet Harness + ALLOC-01 Baseline Summary

## One-liner

BDN 0.15.8 harness with MemoryDiagnoser wired to IMPdfService via minimal DI; ALLOC-01 v0.1 baseline captured: RuntimeFactory allocates 412.8 MB per operation on the unoptimised path.

## ALLOC-01 Baseline

**Captured:** 2026-05-27, .NET 8.0.25, Intel Core i7-12700, BenchmarkDotNet v0.15.8

| Method | Mean | Error | StdDev | Gen0 | Gen1 | Gen2 | Allocated |
|---|---|---|---|---|---|---|---|
| RuntimeFactory (Baseline=true) | 270.8 ms | 5.36 ms | 8.81 ms | 32000.0000 | 10000.0000 | 5000.0000 | **412.8 MB** |
| SourceGenerated (Wave 1 = runtime path) | 271.0 ms | 5.35 ms | 8.18 ms | 32000.0000 | 10000.0000 | 5000.0000 | 412.8 MB |

**SC4 target (Wave 3, Plan 05):** RuntimeFactory Allocated must drop to ≤ 288.96 MB (30% reduction from 412.8 MB).

**SC2 note:** SourceGenerated currently uses the same runtime path (Wave 1). Wave 3 Plan 05 will swap in the real SG renderer; the SC2 target is ≥3× warm throughput vs RuntimeFactory.

**Template used:** `reference-50kb.html` (21 KB HTML, same file as tests/Muonroi.Pdf.Tests/TestResources/Perf/reference-50kb.html). The file is named "reference-50kb.html" but is 21 KB on disk — it is the canonical perf gate template, not padded to exactly 50 000 bytes.

## Tasks Completed

| Task | Description | Files |
|---|---|---|
| Checkpoint (pre-approved) | BenchmarkDotNet 0.15.8 package legitimacy gate | — |
| Task 1 | Add BenchmarkDotNet to CPM + create benchmark project | Directory.Packages.props, Muonroi.Pdf.Benchmarks.csproj, PdfRenderBenchmarks.cs, Program.cs |
| Baseline run | `dotnet run -c Release` — BDN full warmup + measurement | BDN Artifacts written to BenchmarkDotNet.Artifacts/ |

## Build Status

```
Build succeeded.  0 Error(s)
dotnet build benchmarks/Muonroi.Pdf.Benchmarks/Muonroi.Pdf.Benchmarks.csproj -m:1 -nodereuse:false
```

Two CS1591 (missing XML doc) warnings on public `Setup()` and `Cleanup()` benchmark methods — pre-existing pattern in the benchmark project (no doc generation enabled). Not errors.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ITenantContext.TenantId requires getter+setter**
- **Found during:** Build after Task 1
- **Issue:** `ITenantContext.TenantId` is `{ get; set; }` — inline `BenchmarkTenantContext` used `=>` expression which only provides a getter
- **Fix:** Changed to `{ get; set; } = "benchmark-tenant";`
- **Files modified:** benchmarks/Muonroi.Pdf.Benchmarks/PdfRenderBenchmarks.cs
- **Commit:** part of feat commit

**2. [Rule 1 - Bug] PdfSharpCore "No appropriate font found" in BDN child process**
- **Found during:** First benchmark run
- **Issue:** BDN spawns a child process; the `reference-50kb.html` has `@font-face{src:url(test.ttf);}` requiring font resolution. Without a `IFontResolver` registration, `PdfSharpCore.Drawing.XGlyphTypeface.GetOrCreateFrom` throws `InvalidOperationException: No appropriate font found`, killing the child process with exit code -1. All benchmark results were NA.
- **Fix:** Added `SystemFontResolver` inner class that reads `C:\Windows\Fonts\arial.ttf` and registers it via `services.AddSingleton<IFontResolver>(new SystemFontResolver())` before `AddPdf()`. The resolver returns the same TTF bytes for all `@font-face` requests.
- **Files modified:** benchmarks/Muonroi.Pdf.Benchmarks/PdfRenderBenchmarks.cs
- **Commit:** part of feat commit (same task commit, both fixes applied before first passing run)

**3. [Rule 1 - Observation] MSB3492 "Question build FAILED" with -q flag**
- **Found during:** First build attempt
- **Issue:** `dotnet build -q` triggers MSB3492 errors on locked AssemblyInfoInputs.cache files from a parallel VS build. These files are locked by VS IDE.
- **Fix:** Deleted the locked cache files and switched to `dotnet build` without `-q` for subsequent invocations. The build succeeded with 0 errors. The `-q` flag produces false error messages in this MSBuild version even when the underlying compilation succeeds.
- **Impact:** None on output — compilation succeeded after cache cleanup.

### Plan Notes

- The plan specified `Microsoft.Extensions.Configuration` as a separate PackageReference in the csproj, but it is already in CPM at `$(MicrosoftExtensionsVersion)`. Added as requested (CPM entry exists, no inline version).
- `reference-50kb.html` is 21 KB on disk (not 50 KB as implied by the filename). The file is the canonical perf gate template used in `PerfGateTests.cs`. This is the correct file per plan instruction.
- The plan mentioned three ProjectReferences including `Muonroi.Pdf.Governance` — included as specified. Governance is pulled in transitively via `Muonroi.Pdf` but explicit reference is fine.

## Threat Flags

None — the benchmark project has no network endpoints, no auth paths, no schema changes. `IResourceResolver` defaults to `ThrowingResourceResolver` (registered by `AddPdf`), preventing any external resource fetch during benchmarks.

## Self-Check: PASSED

- `Directory.Packages.props` contains `<PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />`: confirmed
- `benchmarks/Muonroi.Pdf.Benchmarks/Muonroi.Pdf.Benchmarks.csproj` exists with `IsPackable=false`, `OutputType=Exe`: confirmed
- `PdfRenderBenchmarks.cs` contains `[MemoryDiagnoser]`, `[Benchmark(Baseline = true)]` on RuntimeFactory, `[Benchmark]` on SourceGenerated: confirmed
- Build: 0 errors
- BDN run: completed with Allocated column = 412.8 MB for RuntimeFactory
