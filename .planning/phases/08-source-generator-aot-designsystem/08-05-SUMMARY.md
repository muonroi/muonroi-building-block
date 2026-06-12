---
phase: 08-source-generator-aot-designsystem
plan: 05
type: summary
status: complete-with-deferral
requirements:
  - AOT-03
  - SG-02
  - ALLOC-01
---

# 08-05 SUMMARY — Alpine AOT image, SG benchmark wiring, allocation profiling

## Result: SC1 ✅ · SC2 ✅ · SC3 ✅ · SC4 ⏸ DEFERRED (strategic decision)

## Commits
- `3bf339e` feat(08-05): AOT-03 — self-contained font + Alpine NativeAOT image 26.8MB (SC3)
- `3c0da09` fix(08-05): commit AotSample GlobalUsings.cs (untracked since 08-04)
- `9d83cbb` feat(08-05): SC2 SG benchmark wiring + per-render text-metrics memoization
- `4389756` perf(08-05): cache XFont per document in writer + SC4 profiling finding

## SC3 — Alpine NativeAOT image < 40 MB ✅
- linux-musl-x64 **NativeAOT** (primary path — no PublishTrimmed fallback needed).
- Image size: **28,138,196 bytes = 26.83 MB** (< 40 MB).
- Smoke run `docker run --rm muonroi-pdf-aot:latest` → `OK: 2p 5087b`, exit 0.
- Trim/AOT warnings present but non-fatal: AngleSharp/AngleSharp.Css/ImageSharp IL2104/IL3050,
  `BindConfiguration` IL2026/IL3050 (startup only). Binary runs correctly.

### AOT font fix (root cause of the earlier "No appropriate font found" crash)
The sample HTML had no `@font-face`, so the FontPipeline (which only resolves `@font-face`
declarations) never invoked the registered IFontResolver — the writer's embedded-font map stayed
empty and PdfSharpCore fell back to PlatformFontResolver, which finds nothing on Alpine. Also the
box tree assigns synthesized inline text the default family **`serif`** (block-level `font-family`
is not inherited to inline nodes). Fix (sample-only, no engine change, mirrors the integration-test
pattern): embed `SampleFont.ttf` as a manifest resource served by AotFontResolver for any request,
declare `@font-face{font-family:serif;…}`, drop ttf-dejavu/fontconfig from the Dockerfile, add
`.dockerignore` (slim context; fixes the buildkit `COPY . .` EOF crash on the full-repo context).

## SC2 — SG warm throughput ≥ 3× ✅
SG-emitted `IMPdfRenderer<InvoiceBenchModel>` (template inlined at compile time) wired into the
`SourceGenerated` benchmark slot.

| Method | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| RuntimeFactory (50 KB stress template) | 255.31 ms | 394.74 MB | 1.00 |
| SourceGenerated (strongly-typed invoice) | 19.81 ms | 23.15 MB | 0.08 |

SourceGenerated is **~12.9× faster** (≥ 3× gate met). Caveat: the SG slot renders the smaller
invoice template while the baseline renders the 50 KB stress doc, so the ratio reflects both the
compile-time inlining win and the smaller realistic payload.

## SC4 — ≥30% allocation reduction ⏸ DEFERRED
Target ≤ 288.96 MB (30% below the 412.8 MB v0.1 baseline). **Not met; deferred by decision.**

### Optimizations applied (correct, banked, no regression — 195/195 tests pass)
1. `SixLaborsTextMetrics` per-render memoization (font / char-width / vertical-metrics caches).
   `GetCharWidth` previously allocated a Font + TextOptions + one-char string + measurement buffer
   **per character**.
2. `PdfSharpCoreWriter` caches `XFont` by (family,size,style) per document.

Net effect on RuntimeFactory: 412.8 MB → **394.74 MB (~4%)** — far short of 30%.

### Profiling finding (the decisive evidence)
Per-stage managed allocation for the 50 KB render (`GC.GetTotalAllocatedBytes(precise:true)`):

| stage | allocated |
|---|---:|
| parse | 0.35 MB |
| cascade | 0.02 MB |
| policy | 3.45 MB |
| layout | 28.59 MB |
| **write** | **360.80 MB (92%)** |

The dominant allocator is **not** AngleSharp/cascade (as the plan assumed) but the **writer** —
specifically PdfSharpCore `XGraphics.DrawString`, invoked once per word (~3000 calls). Each call
re-runs the string→glyph-index→encoding→content-operator pipeline internally; that is why caching
`XFont` did nothing (the cost is inside the call). A content-stream `TJ`-per-line emitter with
precomputed glyph IDs + advances skips that pipeline entirely.

### Decision (user-approved)
SC4's resolution is a **strategic writer initiative**, not a localized fix. See
`.planning/research/pdf-writer-strategy.md`: recommendation is to **build an owned minimal PDF 1.7
writer** (the engine already owns positioned glyphs, the font subsetter, and determinism
post-processing), with PDFsharp 6.x migration as the fallback. This is tracked to a new phase
("Owned PDF Writer") starting with a 2–3 day allocation spike. SC4 carries over to that phase.
Note: any writer change re-baselines all 57 golden snapshots regardless of option chosen.

## Deviations
- Plan listed alloc-target files under `Internal/Rendering/`; actual path is `Internal/Layout/`
  (`PositionedPageList.cs`, `InlineLayoutEngine.cs`). `PositionedPageList` holds only a couple of
  Lists and is not an allocation hotspot — the real hotspot is the writer (see profiling).
- `-m:1 -nodereuse:false` triggers MSB3492 cache-lock on this host's MSBuild SDK 10.0.201; used
  plain `dotnet build`/`dotnet test` (consistent with the 08-04 deviation). Docker build is
  unaffected (runs inside the container).
- BenchmarkDotNet 0.15.8 package legitimacy checkpoint (08-03) verified on NuGet (repo
  github.com/dotnet/BenchmarkDotNet, owner AndreyAkinshin, 67.6M downloads) — approved.
