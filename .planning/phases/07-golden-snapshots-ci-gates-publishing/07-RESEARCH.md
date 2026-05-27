# Phase 7: Golden Snapshots + CI Gates + Publishing — Research

**Researched:** 2026-05-27
**Domain:** xunit golden-snapshot harness, deterministic perf gating, .NET CPM packaging, PowerShell CI gates
**Confidence:** HIGH — all repo claims verified from source files; external library claims tagged CITED/ASSUMED

---

## Summary

Phase 7 is a **regression-lock + release-prep** phase, not a feature phase. The engine is already deterministic and byte-stable (`DeterminismTests` proves `WriteAsync(same input)` → identical bytes, verified). Phase 7's job is to (1) capture that determinism as a committed golden corpus of 40+ cases plus 10+ Vietnamese cases, (2) add a determinism canary and perf gate to CI, (3) make the three convention gate scripts exit 0, and (4) produce CPM-compliant `.nupkg` artifacts for the four Pdf packages at `1.0.0-alpha.N`.

The most important finding: **the existing test infrastructure already solves the two hardest problems.** The `TestFont.ttf` embedded resource (`WriterTestFonts`) removes OS-font nondeterminism, and `PdfRenderCollection` (`DisableParallelization = true`) already serializes all writer tests around PdfSharpCore's process-global `GlobalFontSettings.FontResolver` / `FontFactory` cache. The golden harness must run inside this same collection and reuse the embedded font. No new determinism mechanism is needed.

Second finding: several Phase-7 requirements are **already partially done or have shifted scope.** `Directory.Build.props` already drives versioning (`VersionPrefix=1.0.0`, `VersionSuffix=alpha.14`) for all packages via CPM — GATE/PKG-07 is largely satisfied. `KNOWN-DEVIATIONS.md` already exists (Phase 3 created it; needs Phase-7 additions, not creation). Conversely, **PKG-05 (meta-package) and PKG-06 (OSS-BOUNDARY.md) are NOT done** — neither lists the three OSS Pdf packages. And `InjectAssemblyHash.ps1` does **not** target any Pdf assembly today; it hardcodes `Muonroi.BuildingBlock/Shared/License/CodeIntegrityVerifier.cs` (see GATE-03 note below).

**Primary recommendation:** Hand-roll a thin xunit golden comparer (byte-equality with an `UPDATE_SNAPSHOTS` env-var regen path, baselines stored as embedded resources), NOT Verify.Xunit. Rationale in Golden Harness section. Sequence: Wave 1 harness + corpus scaffolding → Wave 2 the 40+/10+ cases + canary → Wave 3 perf gate → Wave 4 packaging (PKG-04..07) + gate-script wiring.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Golden baseline storage | `Muonroi.Pdf.Tests` (embedded resources) | — | Mirrors existing `TestResources/**` `EmbeddedResource` glob; immune to working-dir/CRLF issues |
| Byte comparison + regen | `Muonroi.Pdf.Tests` (test helper) | — | Pure test concern; no production code touched |
| Determinism canary | `Muonroi.Pdf.Tests` (Writer collection) | CI script | Render-twice-compare; already proven by `DeterminismTests` |
| Perf gate (cold/warm) | `Muonroi.Pdf.Tests` (Stopwatch test) | CI script | Hardware-dependent; gate expresses tolerance, not absolute ms in CI |
| Versioning (`alpha.N`) | `Directory.Build.props` | `build/Version.props` | Single source of truth already wired (CPM) |
| Package metadata | per-package `.csproj` + `Directory.Build.props` | — | License/README/icon `Pack` items already centralized |
| Boundary enforcement | `check-modular-boundaries.ps1` | — | Scans `src/**/*.csproj` for commercial refs |
| Meta-package membership (PKG-05) | `Muonroi.BuildingBlock.All.csproj` | — | Add 3 OSS Pdf ProjectReferences |
| OSS allowlist (PKG-06) | `OSS-BOUNDARY.md` | — | Add 3 OSS Pdf package names |

---

## User Constraints (from CONTEXT.md)

> **No CONTEXT.md exists for Phase 7 yet.** `.planning/config.json` has `skip_discuss: false`, so `/gsd:discuss-phase` is expected to run and produce `07-CONTEXT.md` before planning. This research surfaces the decisions that discussion must lock. Until then, treat every item in the Assumptions Log as needing user confirmation. The most consequential open decisions:
>
> 1. **Golden harness**: hand-rolled comparer (this doc recommends) vs Verify.Xunit.
> 2. **Baseline storage**: embedded resources (recommended) vs loose files under `TestResources/Golden/`.
> 3. **Perf gate strategy in CI**: how absolute-ms budgets (300 ms cold / 80 ms warm) translate to a tolerance-based or skip-in-CI gate (see Performance section).
> 4. **GATE-03 scope**: whether `InjectAssemblyHash.ps1` must be generalized to `Muonroi.Pdf.Enterprise`, or whether SC4 is satisfied by the script exiting 0 against its current `Muonroi.BuildingBlock` target.
> 5. **SC5 publish scope**: confirmed OUT OF SCOPE for execution — `dotnet pack` artifacts only, no live `dotnet nuget push` (no nuget.config / feed in repo).

---

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PKG-04 | `Muonroi.Pdf.Enterprise` stub with `<IsCommercialPackage>true</IsCommercialPackage>` | **Already done** — verified in Enterprise csproj (line 6) |
| PKG-05 | `Muonroi.BuildingBlock.All` includes the 3 OSS Pdf packages | **NOT done** — meta-package csproj has zero Pdf ProjectReferences (verified) |
| PKG-06 | `OSS-BOUNDARY.md` allowlists the 3 OSS Pdf packages | **NOT done** — grep for `Muonroi.Pdf` in OSS-BOUNDARY.md returns nothing |
| PKG-07 | Publish `1.0.0-alpha.N`, no per-csproj `Version` | **Largely done** — `Directory.Build.props` VersionPrefix/Suffix drives all; verify no inline `<Version>` in Pdf csprojs (none found in the 4 read) |
| TEST-01 | ≥40 golden snapshots: block/inline/table/page-breaks/image/font | Harness pattern + corpus design below |
| TEST-02 | ≥10 Vietnamese golden snapshots | Corpus design below; `TestFont.ttf` must contain Vietnamese glyphs (VERIFY) |
| TEST-03 | CSS 2.1 subset ≥95% on declared modules; deviations documented | KNOWN-DEVIATIONS.md exists; ≥95% is a measurement claim — see Assumptions A4 |
| TEST-04 | `KNOWN-DEVIATIONS.md` lists every intentional deviation | **File exists** (Phase 3); add any Phase 4–6 deviations |
| PERF-01 | Cold render 50 KB template ≤300 ms (1 thread) | Stopwatch+warmup pattern; tolerance guidance below |
| PERF-02 | Warm render 50 KB template ≤80 ms (1 thread) | Same; warm = post-JIT/post-font-cache |
| GATE-01 | `check-modular-boundaries.ps1` exits 0 | Script verified; Pdf OSS projects must not ref commercial pkgs |
| GATE-02 | `pre-publish-gate.ps1` exits 0 | Runs full `dotnet test` (Release, `Category!=SlowIntegration`) + boundary check |
| GATE-03 | `InjectAssemblyHash.ps1` locks Enterprise assembly hash | **Script targets `Muonroi.BuildingBlock` today** — scope decision needed (A2) |

---

## What Already Exists (verified)

| Asset | Location | Relevance to Phase 7 |
|-------|----------|----------------------|
| `DeterminismTests` | `tests/Muonroi.Pdf.Tests/Writer/DeterminismTests.cs` | Proves byte-stability; canary is a generalization of this |
| `WriterTestFonts` + `TestFont.ttf` | `tests/.../Writer/WriterTestFonts.cs`, `TestResources/TestFont.ttf` | Embedded deterministic font; golden corpus reuses it |
| `PdfRenderCollection` | `tests/.../PdfRenderCollection.cs` | `DisableParallelization=true` — solves FontFactory race; golden tests join it |
| `EmbeddedResource Include="TestResources/**"` | `Muonroi.Pdf.Tests.csproj` (line 21) | Baseline-as-embedded-resource pattern already wired |
| `MPdfService` + `AddPdf()` | `src/Muonroi.Pdf/Internal/Service/`, `Extensions/` | End-to-end HTML→PDF path exists (Phase 6 complete) — corpus drives this |
| Versioning | `Directory.Build.props` (VersionPrefix `1.0.0`, VersionSuffix `alpha.14`) + `build/Version.props` | PKG-07 single-source-of-truth already in place |
| `KNOWN-DEVIATIONS.md` | repo root | Exists with KD-03-01..05; needs Phase 4–6 additions only |
| Package `Pack` items | `Directory.Build.props` (LICENSE/README/icon, OSS vs commercial split) | `.nupkg` metadata already centralized |
| Enterprise stub | `src/Muonroi.Pdf.Enterprise/` | PKG-04 done |

**NOT present (Phase 7 must create/change):**
- `.gitattributes` — **does not exist** (critical for committed binary baselines if loose-file storage is chosen)
- Golden harness helper + corpus + baselines
- Perf gate test
- Meta-package + OSS-BOUNDARY.md Pdf entries (PKG-05/06)
- Any Verify/Snapshooter package (none in `Directory.Packages.props`)

---

## Golden Snapshot Harness — Recommendation: Hand-Rolled (not Verify.Xunit)

### Recommendation

**Build a thin, ~60-line xunit golden comparer.** Do NOT add Verify.Xunit.

### Pattern

```csharp
// tests/Muonroi.Pdf.Tests/Golden/GoldenPdf.cs
internal static class GoldenPdf
{
    // Opt-in regeneration: set MUONROI_UPDATE_SNAPSHOTS=1 to rewrite baselines.
    private static bool UpdateMode =>
        Environment.GetEnvironmentVariable("MUONROI_UPDATE_SNAPSHOTS") is "1" or "true";

    public static async Task VerifyAsync(string caseName, string html, PdfRenderOptions options)
    {
        byte[] actual = await RenderToBytes(html, options); // via MPdfService or PdfSharpCoreWriter
        string baselineRel = $"TestResources.Golden.{caseName}.pdf";

        if (UpdateMode)
        {
            // Write to the SOURCE tree (not bin/) so the new baseline is committable.
            File.WriteAllBytes(SourcePathFor(caseName), actual);
            return;
        }

        byte[] expected = LoadEmbedded(baselineRel)
            ?? throw new Xunit.Sdk.XunitException(
                $"No baseline for '{caseName}'. Run with MUONROI_UPDATE_SNAPSHOTS=1 to create it.");

        actual.SequenceEqual(expected).Should().BeTrue(
            $"golden '{caseName}' must match committed baseline byte-for-byte; " +
            $"baseline {expected.Length} B, actual {actual.Length} B");
    }
}
```

- **Comparison: byte-equality**, not structural. The engine is already proven byte-deterministic, so byte-equality is the *strongest* available assertion and the cheapest. A structural/PDF-object comparer would mask determinism regressions (e.g. an object-ID ordering change) that byte-equality catches. Structural comparison is only needed when output is legitimately non-deterministic — which this engine, by design (SEC-03/04, DET-01..03), is not.
- **Regen: `MUONROI_UPDATE_SNAPSHOTS=1`** env var writes baselines back to the *source* `TestResources/Golden/` dir so they can be `git add`-ed. Deliberate, opt-in, never fires in CI.
- **Determinism:** every golden test carries `[Collection(PdfRenderCollection.Name)]` and uses `WriterTestFonts.Embedded()` — same guarantees that make `DeterminismTests` stable.

### Why hand-rolled over Verify.Xunit

| Factor | Hand-rolled byte comparer | Verify.Xunit |
|--------|---------------------------|--------------|
| Fit for "deterministic exact bytes" | Native — exact-match IS the goal | Verify is built for *accepting* diffs via a launchable diff tool; exact binary match is a degenerate use [CITED: github.com/VerifyTests/Verify] |
| New dependency | None | `Verify.Xunit` 31.x [VERIFIED: nuget — but treat name as ASSUMED per provenance rule] |
| CI behavior | Pure pass/fail, no tooling | Needs `DiffEngine`/CI guards to avoid launching diff tools; extra config [CITED: blog.jetbrains.com] |
| Binary PDFs | `SequenceEqual` | Supported, but richest PDF support is via extension pkgs (Verify.PdfPig/QuestPDF/Aspose) that re-parse — adds deps + non-byte semantics [CITED: github.com/VerifyTests/Verify] |
| Regen workflow | One env var | `.received`→`.verified` accept dance |
| Repo precedent | Matches existing embedded-resource + `SequenceEqual` style (`DeterminismTests`) | No Verify usage anywhere in repo (verified) |

Verify is excellent for *serialized object* snapshots where diffs are expected and reviewed. For a **byte-exact, browserless, deterministic PDF**, it adds ceremony and a dependency without buying anything `SequenceEqual` doesn't already give. The existing `DeterminismTests` already demonstrate the idiom.

### Baseline storage: embedded resources (recommended)

Store baselines under `tests/Muonroi.Pdf.Tests/TestResources/Golden/*.pdf` — they're picked up automatically by the existing `EmbeddedResource Include="TestResources/**"` glob. Embedded resources are read as raw bytes from the assembly, so they are **immune to working-directory and CRLF/LF normalization issues**. This sidesteps the `.gitattributes` problem entirely for read-back (Git could still mangle the committed file on checkout — see Pitfalls; add `.gitattributes` regardless).

---

## Corpus Design (TEST-01: ≥40, TEST-02: ≥10)

Each case = one HTML+CSS string (or embedded `.html`) + a committed baseline PDF. Exercise the **declared v0.1 CSS 2.1 subset only** — anything the policy gate rejects (flex/grid/float/position/border-collapse) is out of scope and tested separately as a policy violation, not a golden.

### TEST-01 — 40+ structural cases

| Group | Count | Cases |
|-------|-------|-------|
| Block layout | 6 | single block; nested blocks; margin-collapse (adjacent); margin-collapse (parent/child); BFC root (overflow:hidden) no-collapse; padding/border box sizing |
| Inline layout | 6 | single line wrap; multi-line wrap; mixed font-size baseline; vertical-align variants; white-space:normal vs pre; trailing-space handling |
| Tables | 7 | simple 2×2; colspan=2; rowspan=2; colspan+rowspan combined; `border-collapse:separate` + border-spacing; auto column width; fixed column width |
| Page breaks / paged media | 7 | `page-break-before:always`; `page-break-after`; `page-break-inside:avoid`; multi-page flow (content overflow); `@page` margins; A4 vs A5 vs Letter vs Legal; portrait vs landscape |
| Counters / headers-footers | 4 | `@page` top margin-box header repeats; bottom footer repeats; `counter(page)`; `counter(pages)` "Page X of Y" |
| Images | 5 | PNG data-URI; JPEG data-URI; PNG via IResourceResolver stub; image intrinsic sizing; image explicit width/height |
| Fonts | 5 | embedded TTF subset; bold weight; italic style; font-size scale; `@font-face` resolved via IFontResolver |

= **40 cases.** Pad to comfortable margin (e.g. 45) so dropping one flaky case doesn't breach the floor.

### TEST-02 — 10+ Vietnamese cases

| # | Case |
|---|------|
| 1 | All-diacritic word "Tiếng Việt" — combining marks stacked |
| 2 | Stacked tone+vowel mark (ế, ộ, ữ) on multiple base glyphs |
| 3 | Mixed Latin + Vietnamese in one line (break opportunities) |
| 4 | Vietnamese line-wrap across two lines |
| 5 | Vietnamese in a table cell |
| 6 | Vietnamese in `@page` header (counter context) |
| 7 | Uppercase Vietnamese with diacritics (Ầ, Ữ) |
| 8 | Long Vietnamese paragraph forcing page break |
| 9 | Vietnamese + digits ("Trang 1 / 3") |
| 10 | Vietnamese with mixed bold/italic runs |
| 11–12 | (margin) Vietnamese-only counter footer; Vietnamese in multi-page flow |

**Hard dependency to verify (A3):** `TestFont.ttf` must actually contain the Vietnamese glyph set. If it does not, these tests render `.notdef` and the "stable diacritics" claim is vacuous. A planner task must confirm glyph coverage (or substitute a font that has it) before baselines are captured. SixLabors.Fonts shaping was integrated in Phase 4 (FONT-04 verified passing), so a covering font already exists somewhere in the pipeline — locate and reuse it.

---

## Performance Gate (PERF-01/02)

### Recommendation: Stopwatch test with warmup, tolerance-aware, CI-gated by env

**Use a simple `Stopwatch` test, not BenchmarkDotNet,** for the *gate*. BenchmarkDotNet is the right tool for the Phase 8 `≥3×`/`≥30% alloc` comparisons (it spins up isolated processes, multiple iterations, statistical analysis) but it is far too slow and heavyweight to run as a per-CI pass/fail gate, and it cannot run meaningfully inside the non-parallel xunit collection.

```csharp
[Collection(PdfRenderCollection.Name)]
public sealed class PerfGateTests
{
    private const int ColdBudgetMs = 300;
    private const int WarmBudgetMs = 80;

    [Fact]
    public async Task Reference50KbTemplate_ColdAndWarm_WithinBudget()
    {
        string html = LoadReferenceTemplate(); // ~50 KB single-page template, embedded resource

        // COLD: first render in a fresh process pays JIT + font-cache warmup.
        var coldSw = Stopwatch.StartNew();
        await RenderToBytes(html, new PdfRenderOptions());
        coldSw.Stop();

        // WARM: subsequent render, caches hot. Take best of N to reduce noise.
        long warmMs = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            await RenderToBytes(html, new PdfRenderOptions());
            sw.Stop();
            warmMs = Math.Min(warmMs, sw.ElapsedMilliseconds);
        }

        coldSw.ElapsedMilliseconds.Should().BeLessThanOrEqualTo(ColdBudgetMs);
        warmMs.Should().BeLessThanOrEqualTo(WarmBudgetMs);
    }
}
```

### Express tolerance, not a brittle absolute

**Absolute ms budgets are hardware-dependent** — a shared CI runner is often 2–5× slower than a dev machine, so a literal `≤300/≤80` gate that passes locally will flag false failures in CI. Recommended handling (lock in discussion, A5):

1. **Definitive run on a developer machine** (per SC3's wording "on a single developer-machine thread") — this is the requirement's measurement context.
2. **In CI**, either (a) skip the perf gate (`[Trait("Category","SlowIntegration")]` so `pre-publish-gate.ps1`'s `Category!=SlowIntegration` filter excludes it), or (b) apply a documented CI multiplier (e.g. ×3) via an env var `PERF_BUDGET_MULTIPLIER`.
3. **Warm = best-of-N** (min), not mean — the budget is a floor on achievable performance, and min eliminates GC/scheduler noise.
4. Single thread: ensure no parallelism — guaranteed by `PdfRenderCollection`.

The "cold" measurement is only truly cold once per process; running cold+warm in the same test process makes "cold" really "first-call-in-test-run." Document this as an accepted approximation (it still captures JIT+font-cache amortization, which is the intended signal).

---

## Packaging (PKG-04..07) & `dotnet pack`

### Versioning — already wired

`Directory.Build.props` sets `VersionPrefix=1.0.0` + `VersionSuffix=alpha.14`; `dotnet pack` composes these into `1.0.0-alpha.14`. To ship `alpha.N`, bump `VersionSuffix` (a `bump-version.ps1` script exists in `scripts/`). **No per-csproj `<Version>` exists** in any of the 4 Pdf csprojs (verified) → PKG-07 CPM compliance holds. NOTE: the Pdf csprojs set `<AssemblyVersion>1.0.0.0</AssemblyVersion>`/`<FileVersion>` — these are assembly identity, not NuGet `Version`, and do **not** violate PKG-07.

### Producing artifacts (SC5 scope)

```powershell
dotnet pack src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj -c Release
dotnet pack src/Muonroi.Pdf/Muonroi.Pdf.csproj -c Release
dotnet pack src/Muonroi.Pdf.Governance/Muonroi.Pdf.Governance.csproj -c Release
dotnet pack src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj -c Release
```

Output: `bin/Release/Muonroi.Pdf.*.1.0.0-alpha.N.nupkg`. **SC5 "appears in NuGet feed" is OUT OF SCOPE for execution** — there is no `nuget.config` or feed in this repo (verified). Phase 7 delivers *verifiable, CPM-compliant `.nupkg` artifacts* and treats `dotnet nuget push` as a downstream release-pipeline step. The plan must state this explicitly and assert on the produced `.nupkg` files (existence, version string, no commercial deps in OSS packages) rather than on a feed.

### PKG-05 / PKG-06 (not done)

- **PKG-05**: add 3 `<ProjectReference>` (Abstractions, Pdf, Governance — NOT Enterprise, which is commercial) to `Muonroi.BuildingBlock.All.csproj`. Note the meta-package itself is `<IsCommercialPackage>true</IsCommercialPackage>`, so the boundary check skips it (verified: script `continue`s on commercial packages).
- **PKG-06**: add the 3 OSS Pdf package names to `OSS-BOUNDARY.md`.

---

## CI Gate Scripts — what each requires (verified)

### GATE-01 — `check-modular-boundaries.ps1`

Scans every `src/**/*.csproj`; for non-commercial projects, fails if any `ProjectReference`/`PackageReference` name matches the hardcoded `$commercialPackages` list (Redis, MassTransit, Hangfire, Grpc, SignalR, BuildingBlock.All, etc.). **Pdf OSS projects must not reference any commercial package.** Verified: the 4 Pdf csprojs reference only `Muonroi.Pdf.*`, `Muonroi.Tenancy.Abstractions`, `Muonroi.Logging.Abstractions`, `Muonroi.Core.Abstractions`, and clean NuGet packages — **none commercial → GATE-01 should already pass.** Enterprise stub is `IsCommercialPackage=true` so it's skipped.

### GATE-02 — `pre-publish-gate.ps1`

Runs `dotnet test Muonroi.BuildingBlock.sln -c Release --filter "Category!=SlowIntegration"` then calls `check-modular-boundaries.ps1`. **Implication:** all 73 existing tests + the new 50+ golden tests + canary must pass in Release config, and any test you don't want in the gate (e.g. the perf gate, if treated as slow) must carry `[Trait("Category","SlowIntegration")]`. The golden/canary tests must NOT be excluded — they are the regression lock.

### GATE-03 — `InjectAssemblyHash.ps1` — SCOPE FLAG

The script (verified) takes `-AssemblyPath`, computes SHA-256, and injects it into a **hardcoded path**: `src/Muonroi.BuildingBlock/Shared/License/CodeIntegrityVerifier.cs`. It has **no knowledge of `Muonroi.Pdf.Enterprise`.** Two readings of SC4 ("`InjectAssemblyHash.ps1` exits 0 in CI"):

- **Literal (lower scope):** the script exits 0 when run — it already does against its `Muonroi.BuildingBlock` target. SC4 is satisfied by demonstrating a clean exit in CI; no Pdf change needed.
- **Intent (higher scope):** PKG-04's "locking the namespace and assembly hash pipeline" implies the Enterprise stub should be wired into a hash-injection step too, which would require generalizing the script (parameterize the verifier path) and adding a `CodeIntegrityVerifier`-equivalent to `Muonroi.Pdf.Enterprise`.

**This is the single biggest scope ambiguity in Phase 7.** Resolve in discussion (A2). Recommendation: adopt the literal reading for v0.1 (Enterprise is an empty stub with no code to protect yet) and defer real hash-locking to Phase 9 when Enterprise gains commercial code — but document the decision in CONTEXT.md.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Non-parallel test isolation | New collection / locks | Existing `PdfRenderCollection` | Already solves the FontFactory race (verified) |
| Deterministic font | Ship/install a font | Existing `WriterTestFonts` + `TestFont.ttf` | Removes OS-font nondeterminism; already used |
| Baseline file loading | Working-dir path juggling | `EmbeddedResource` + `GetManifestResourceStream` | CRLF/cwd-immune; pattern already in csproj |
| Version composition | Manual `<Version>` strings | `Directory.Build.props` VersionPrefix/Suffix | CPM single-source-of-truth; PKG-07 compliance |
| Perf statistics (Phase 8) | Custom timing harness | BenchmarkDotNet | But NOT for the Phase 7 pass/fail gate — Stopwatch there |
| PDF structural diff | Custom PDF object parser | Byte `SequenceEqual` | Engine is byte-deterministic; exact match is strongest + simplest |

---

## Common Pitfalls

### Pitfall 1: PdfSharpCore process-global FontFactory cache (already bit the team)
PdfSharpCore sets `GlobalFontSettings.FontResolver` once per process and keeps a static `FontFactory` source cache keyed by internal font name. Parallel renders in different collections race → intermittent "same key already added" / `NullReferenceException` (documented verbatim in `PdfRenderCollection.cs`).
**Mitigation:** every golden, canary, and perf test MUST carry `[Collection(PdfRenderCollection.Name)]`. Do not create a second renderer collection.

### Pitfall 2: CRLF/LF corruption of committed binary baselines
There is **no `.gitattributes`** in the repo (verified). On Windows checkout, Git's autocrlf can mangle a committed `.pdf` baseline, producing phantom byte mismatches that only reproduce on some machines.
**Mitigation:** add `.gitattributes` with `*.pdf binary` and `*.ttf binary` (and ideally `tests/**/TestResources/** -text`). Even though embedded-resource read-back is cwd/CRLF-immune at *runtime*, the committed file itself must be checked out byte-exact. This is a required Phase-7 task, not optional.

### Pitfall 3: Baselines written to bin/ during regen
A naive `MUONROI_UPDATE_SNAPSHOTS` path that writes next to the running assembly puts new baselines in `bin/` where they can't be committed.
**Mitigation:** regen must resolve the **source-tree** `TestResources/Golden/` path (walk up from `AppContext.BaseDirectory` or use a compile-time `[CallerFilePath]`), write there, then the developer `git add`s.

### Pitfall 4: Perf gate false-failures on slow CI runners
Absolute ms budgets calibrated on a dev machine fail on shared CI hardware.
**Mitigation:** see Performance section — skip in CI via `Category=SlowIntegration`, or apply a documented multiplier.

### Pitfall 5: Vietnamese baselines render `.notdef` if TestFont lacks glyphs
If `TestFont.ttf` has no Vietnamese coverage, the 10+ Vietnamese goldens "pass" against baselines that are themselves wrong (boxes/.notdef), making TEST-02 vacuous.
**Mitigation:** verify glyph coverage before capturing baselines; reuse the covering font that Phase 4's FONT-04 tests already rely on.

### Pitfall 6: `dotnet pack` picks up wrong License/README for OSS vs commercial
`Directory.Build.props` conditions `LICENSE-APACHE` vs `LICENSE-COMMERCIAL` on `IsCommercialPackage`. The 3 OSS Pdf packages must NOT set `IsCommercialPackage` (they don't — verified); the Enterprise stub correctly sets it `true`.
**Mitigation:** no change needed, but the packaging task must assert the OSS `.nupkg`s carry `LICENSE-APACHE` and Enterprise carries `LICENSE-COMMERCIAL`.

### Pitfall 7: Golden tests excluded from the pre-publish gate
If a golden test accidentally gets `Category=SlowIntegration`, `pre-publish-gate.ps1` skips it and the regression lock silently disappears.
**Mitigation:** only the perf gate may be `SlowIntegration`; goldens + canary must run in the default category.

---

## Determinism Canary (SC1)

Generalize `DeterminismTests` to the whole corpus: render every golden HTML case twice in one CI run and assert pairwise byte-equality. This is distinct from baseline comparison (which catches *output drift over time*); the canary catches *intra-run nondeterminism* (e.g. a hash-ordering or timestamp regression) even before a baseline exists.

```csharp
[Theory]
[MemberData(nameof(AllCorpusCases))]
public async Task Corpus_RenderedTwice_IsByteIdentical(string caseName, string html, PdfRenderOptions opts)
{
    byte[] a = await RenderToBytes(html, opts);
    byte[] b = await RenderToBytes(html, opts);
    a.SequenceEqual(b).Should().BeTrue($"'{caseName}' must be byte-deterministic within a run (SC1 canary)");
}
```

---

## Validation Architecture

`nyquist_validation` is absent from `.planning/config.json` → treat as enabled.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 + FluentAssertions 7.2.0 + NSubstitute 5.3.0 (verified, CPM-managed) |
| Config file | none — inherited via `Directory.Build.props` test-project block |
| Quick run | `dotnet test tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj --filter "Category!=SlowIntegration"` |
| Full suite | `dotnet test Muonroi.BuildingBlock.sln -c Release --filter "Category!=SlowIntegration"` (= the gate) |

### Phase Requirements → Test Map
| Req | Behavior | Type | Command | Exists? |
|-----|----------|------|---------|---------|
| TEST-01 | 40+ goldens match baselines | golden | `dotnet test --filter "FullyQualifiedName~Golden"` | ❌ Wave 1–2 |
| TEST-02 | 10+ VN goldens match | golden | same | ❌ Wave 2 |
| SC1 | corpus byte-deterministic twice | canary | `--filter "FullyQualifiedName~Canary"` | ❌ Wave 2 |
| PERF-01/02 | cold ≤300 / warm ≤80 ms | perf | `--filter "FullyQualifiedName~PerfGate"` (dev) | ❌ Wave 3 |
| GATE-01 | boundary clean | script | `pwsh scripts/check-modular-boundaries.ps1` | ✅ passes today |
| GATE-02 | full gate green | script | `pwsh scripts/pre-publish-gate.ps1` | ✅ script exists |
| GATE-03 | hash inject exits 0 | script | `pwsh scripts/InjectAssemblyHash.ps1 -AssemblyPath …` | ⚠️ scope (A2) |
| PKG-05/06/07 | packages + allowlist | pack/assert | `dotnet pack …` + file asserts | ❌ Wave 4 |

### Wave 0 Gaps
- [ ] `.gitattributes` (`*.pdf binary`, `*.ttf binary`) — REQUIRED before committing baselines
- [ ] `tests/.../Golden/GoldenPdf.cs` harness helper
- [ ] `tests/.../TestResources/Golden/` baseline dir + reference 50 KB template
- [ ] Confirm `TestFont.ttf` Vietnamese glyph coverage (or source a covering font)

---

## Security Domain

`security_enforcement` absent → treat as enabled. Phase 7 adds **no new attack surface** (test + packaging only), but two ASVS-adjacent concerns apply:

| Category | Applies | Control |
|----------|---------|---------|
| V14 Build/Dependency | yes | CPM pins all versions (verified); golden baselines are inert PDFs, not executable; `dotnet pack` produces signed-on-push artifacts (push out of scope) |
| V1 Architecture (supply chain) | yes | `check-modular-boundaries.ps1` enforces OSS↔commercial separation; PKG-06 keeps the OSS allowlist authoritative |

The engine's own security guarantees (SEC-01..06: no JS/Launch/EmbeddedFile, no timestamps, file:// rejection) are Phase 5 concerns; Phase 7 should add **at least one golden case asserting a hardened PDF** (no `/JavaScript`, `%PDF-1.7` header) to lock SEC-01/02 into the regression corpus.

---

## Package Legitimacy Audit

Phase 7 (as recommended) installs **no new packages** — the hand-rolled harness uses only the existing xunit/FluentAssertions stack. If discussion instead chooses Verify.Xunit:

| Package | Registry | slopcheck | Disposition |
|---------|----------|-----------|-------------|
| `Verify.Xunit` | nuget (31.x exists) | not run (slopcheck is pip-only; .NET pkg) | **[ASSUMED]** — name from WebSearch; planner must gate behind `checkpoint:human-verify` before install |

slopcheck targets PyPI/npm; for NuGet, verify via `dotnet add package Verify.Xunit --version 31.x` against nuget.org and confirm publisher `Simon Cropp / VerifyTests`. Recommendation stands: avoid the dependency entirely.

---

## State of the Art

| Old Approach | Current Approach | Impact |
|--------------|------------------|--------|
| Image-diff golden PDFs (rasterize + SSIM) | Byte-exact goldens (only viable because engine is deterministic) | Simpler, stronger; SSIM deferred to Phase 9 CANARY-02 where output *can* legitimately differ |
| BenchmarkDotNet as a gate | Stopwatch best-of-N as gate; BDN reserved for Phase 8 ratios | Fast CI; BDN's process isolation reserved for `≥3×`/`≥30%` claims |

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | Hand-rolled comparer preferred over Verify.Xunit | Golden Harness | If team standardizes on Verify elsewhere, hand-rolled diverges from convention |
| A2 | GATE-03 satisfied by literal "script exits 0" reading; Enterprise hash-locking deferred to Phase 9 | CI Gates | If intent is to wire Enterprise into hash pipeline now, scope expands (parameterize script + add verifier to stub) |
| A3 | `TestFont.ttf` (or a Phase-4 font) covers Vietnamese glyphs | Corpus | TEST-02 baselines vacuous if `.notdef` rendered |
| A4 | "CSS 2.1 ≥95% on declared modules" (TEST-03) is satisfiable by the corpus | Requirements | "95%" is unmeasured; needs a definition of the conformance denominator |
| A5 | Perf gate skipped/multiplied in CI, definitive run on dev machine | Performance | Literal CI gate → false failures on slow runners |
| A6 | `Verify.Xunit` package name/version (only if chosen) | Package Audit | Per provenance rule, treat as ASSUMED until verified on nuget.org |
| A7 | SC5 publish = `dotnet pack` artifacts only; push out of scope | Packaging | Confirmed by absence of nuget.config; but confirm with stakeholder |

---

## Open Questions

1. **GATE-03 scope (A2)** — literal vs intent reading of Enterprise hash-locking. Recommendation: literal for v0.1, document deferral.
2. **TEST-03 "95%" denominator (A4)** — what is the declared-module conformance baseline being measured against? Recommendation: define as "the corpus exercises every non-rejected property in the declared subset; deviations are exhaustively listed in KNOWN-DEVIATIONS.md" rather than a numeric coverage metric.
3. **Perf gate in CI (A5)** — skip vs multiplier. Recommendation: `Category=SlowIntegration` skip in `pre-publish-gate.ps1`, definitive measurement on dev machine per SC3 wording.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build/test/pack | ✓ | host SDK (no global.json); targets net8.0 | — |
| PowerShell | gate scripts | ✓ (Windows host) | 5.1+ | `pwsh` cross-platform if CI is Linux |
| `dotnet pack` | PKG-07/SC5 | ✓ | bundled with SDK | — |
| NuGet feed / nuget.config | live publish | ✗ | — | OUT OF SCOPE — artifacts only |
| BenchmarkDotNet | (Phase 8 only) | not in CPM | — | not needed Phase 7 |

**No blocking missing dependencies.** The only "missing" item (NuGet feed) is intentionally out of scope.

---

## Sources

### Primary (HIGH — verified from repo)
- `scripts/check-modular-boundaries.ps1`, `scripts/pre-publish-gate.ps1`, `scripts/InjectAssemblyHash.ps1` — gate logic
- `Directory.Build.props` (VersionPrefix/Suffix, Pack items, test deps), `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` (EmbeddedResource glob)
- `tests/.../Writer/DeterminismTests.cs`, `WriterTestFonts.cs`, `PdfRenderCollection.cs` — determinism + isolation idioms
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj`, `Muonroi.Pdf.Enterprise.csproj`, `Muonroi.BuildingBlock.All.csproj`, `OSS-BOUNDARY.md`, `KNOWN-DEVIATIONS.md` — package/boundary state
- `.planning/ROADMAP.md`, `.planning/REQUIREMENTS.md`, `06-RESEARCH.md`

### Secondary (MEDIUM/CITED — external)
- [Verify (VerifyTests/Verify) — snapshot tool, binary/PDF support](https://github.com/VerifyTests/Verify)
- [NuGet Gallery — Verify.Xunit 31.x](https://www.nuget.org/packages/Verify.Xunit/)
- [Snapshot Testing in .NET with Verify — JetBrains .NET blog](https://blog.jetbrains.com/dotnet/2024/07/11/snapshot-testing-in-net-with-verify/)

## Metadata

**Confidence breakdown:**
- Golden harness recommendation: HIGH — repo idioms + Verify behavior cross-checked
- Gate scripts: HIGH — all three read line-by-line
- Packaging/versioning: HIGH — csprojs + Build.props verified; push scope confirmed by absence of feed
- Perf gate: MEDIUM — strategy sound, but absolute budgets need dev-machine calibration (A5)
- Vietnamese corpus: MEDIUM — depends on unverified font glyph coverage (A3)

**Research date:** 2026-05-27
**Valid until:** 2026-06-26 (stable repo; external Verify version may move)
