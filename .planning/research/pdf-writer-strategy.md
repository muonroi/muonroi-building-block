# PDF Writer Strategy: Replace, Fork, or Build the Final Serialization Layer

**Project:** Muonroi.Pdf (pure-managed PDF ecosystem, open-core)
**Date:** 2026-05-27
**Decision owner:** Muonroi building-block team
**Status:** Decision memo — awaiting approval
**Overall confidence:** HIGH on diagnosis and recommendation; MEDIUM on exact allocation-win magnitude (needs a spike to confirm).

---

## TL;DR

**Recommendation: BUILD an owned minimal PDF 1.7 content-stream writer (Option 4), executed as a phased migration with Option 2 (upstream PDFsharp 6.x) as the fallback.**

The engine already owns glyphs, positions, font-subset bytes, and determinism post-processing. PdfSharpCore is doing exactly one job — re-encoding text you have already laid out — and it does that job by re-deriving per word everything you already computed. That re-derivation is the 360 MB (92%) allocator. No third-party library swap removes the architectural mismatch, because the mismatch is "we hand a layout-complete word stream to an API designed to lay out and shape text from scratch." Owning the emitter is the only option that both hits SC4 (≥30% alloc reduction) and advances the strategic goal of a zero-third-party, AOT-trivial, fully deterministic core.

**Top 3 reasons:**
1. **You already own the inputs.** Positioned glyph boxes + subset font bytes + `NormalizeForDeterminism` mean PdfSharpCore's value-add is reduced to byte serialization of data you already have. A `Tj`/`TJ` emitter is a thin layer over assets the engine already produces.
2. **The allocation is structural, not tunable.** `DrawString` allocates per call because each call re-runs string→`CodePointGlyphIndexPair[]`→encoding-analysis→content-operator construction (verified against PDFsharp's documented glyph-mapping pipeline). ~3000 word-level calls = ~3000 such pipelines. You cannot cache your way out; the cost is *inside* the call. A content-stream-level emitter that writes one `TJ` array per line with precomputed glyph IDs and advances skips that pipeline entirely.
3. **Strategic ownership + AOT.** PdfSharpCore is a stale unofficial port (1.3.65, MIT) of an old PDFsharp; upstream PDFsharp 6.x is MIT and net8-native but uses reflection and is only *pragmatically* AOT-capable (no official AOT guarantee). An owned emitter with no reflection is AOT-trivial by construction and removes a third-party dependency from the core's critical path.

**Effort estimate:** Owned writer for the text+image+determinism path you ship today: **~3–4 engineering weeks** (1 wk core PDF object/xref/stream plumbing, 1 wk Type0/CID font embedding wired to the existing `TrueTypeFontSubsetter`, 0.5 wk image XObjects, 0.5 wk `TJ` text emitter, 1 wk re-baselining 57 goldens + determinism/round-trip validation). Fallback (Option 2 migration) is ~1 week if the spike disappoints.

---

## Context Recap (the trigger)

Per-stage allocation of a 50 KB text-heavy HTML→PDF render:

| Stage | Alloc | Share |
|-------|------:|------:|
| parse | 0.35 MB | 0.1% |
| cascade | 0.02 MB | <0.1% |
| policy | 3.45 MB | 0.9% |
| layout | 28.59 MB | 7.3% |
| **WRITE** | **360.80 MB** | **91.8%** |
| total | 393 MB | 100% |

SC4 target: ≥30% total reduction → ≤288.96 MB. With 92% of allocation in WRITE, **the writer is the only lever that matters.** A 30% cut in WRITE alone (−108 MB) clears the target; the realistic ceiling for an owned emitter is far higher.

Current writer (`PdfSharpCoreWriter.cs`): one `gfx.DrawString(word, font, brush, point)` per `PositionedElement`, ~3000 calls. `XFont` is already cached per `(family,size,style)` — and the code comment (ALLOC-01) confirms caching **did not** remove the cost, because the cost is inside `DrawString`/`Save`, not in `XFont` construction.

---

## (c) What makes DrawString allocate per word — and does a content-stream API avoid it?

**Verified mechanism** (PDFsharp glyph-mapping docs, which describe the same `XGraphics.DrawString` pipeline PdfSharpCore inherits):

Each `DrawString` call performs, *per call*:
1. Convert the UTF-16 .NET string into an array of `CodePointGlyphIndexPair` (allocates a code-point/glyph array).
2. Fire/evaluate the render-text path and **analyze every code point** to choose ANSI vs. glyph (Unicode) encoding.
3. Build PDF content-stream operators (`Tj` for ANSI, hex glyph runs for Unicode) — string/byte building per call.
4. Append to the page content stream (intermediate buffers).

Caching `XFont` only avoids the *typeface lookup*. Steps 1–4 re-run on **every word**. At ~3000 words you pay ~3000× the per-call glyph-array + encoding-analysis + operator-construction + buffer churn. This matches the profile: caching helped nothing, `Save()` and `DrawString` dominate.

**Does a content-stream-level API avoid it? YES — decisively.** The PDF text model is built for exactly your situation:

- A single `BT … ET` text block can position and show an entire line.
- The **`TJ` operator takes one array** mixing glyph-ID strings and inter-glyph numeric advances: `[ (glyphs) -120 (more) ] TJ`. One `TJ` can render a whole line of pre-positioned words with the kerning/spacing baked in as numbers.
- Because the engine **already has glyph indices** (via `GlyphCollector` + SixLabors.Fonts metrics) and **already has X positions per word**, you can emit one `Tj`/`TJ` per line instead of one `DrawString` per word — and skip steps 1–3 entirely (no re-mapping, no encoding analysis, no per-word operator synthesis).

Notably, **upstream PDFsharp 6.x already exposes the glyph layer** (`CodePointGlyphIndexPair`, `RenderTextEvent`) — confirming the lower-level primitive exists conceptually — but it does **not** expose a public "emit this precomputed glyph run at these positions" entry point that bypasses the per-call pipeline. So even on upstream you would be fighting the API rather than using it as intended. An owned emitter uses the PDF format the way it was designed.

**Expected allocation win (owned emitter):** eliminating per-word glyph-array allocation, per-word encoding analysis, and per-word operator buffers should remove the large majority of the 360 MB. Conservative estimate: WRITE drops from ~361 MB to the low tens of MB (CID font dict + xref + stream bytes), i.e. **a 70–90% WRITE reduction → well past the −30% total SC4 target.** (MEDIUM confidence on the exact number — confirm with a one-day `TJ`-per-line spike before committing the full build.)

---

## (b) Fact verification: PdfSharpCore vs. upstream PDFsharp

| Claim | Verdict | Evidence |
|-------|---------|----------|
| PdfSharpCore is an unofficial .NET Core port by ststeiger | **CONFIRMED** | GitHub `ststeiger/PdfSharpCore` — "Port of the PdfSharp library to .NET Core." |
| PdfSharpCore is a port of an **old** PDFsharp | **CONFIRMED** | Described as a partial port of PdfSharp.Xamarin / MigraDoc 1.32 — a pre-6.x lineage. |
| PdfSharpCore is MIT | **CONFIRMED** | NuGet + repo license MIT. |
| Pinned at 1.3.65 | **CONFIRMED** | NuGet `PdfSharpCore 1.3.65`. |
| PdfSharpCore maintenance is thin/stale | **CONFIRMED (LOW velocity)** | Activity is occasional community PR merges (e.g. a 2023 rotate fix); it is not actively developed against modern .NET. It depends on SixLabors.ImageSharp + SixLabors.Fonts (pure-managed — *not* the native Skia problem). |
| Upstream empira PDFsharp is at 6.x, MIT, net8-native | **CONFIRMED** | `empira/PDFsharp` — **6.2.4 final**, MIT, targets net8/net9/net10, net4.6, **and netstandard2.0** (Core packages). |
| Upstream PDFsharp is AOT/trim friendly | **PARTIAL / NOT GUARANTEED** | Forum (Thomas Hoevel, empira): AOT initially throws `NotImplementedException`; works only with `<TrimMode>partial</TrimMode>` **or** `<TrimmerRootAssembly Include="PdfSharp"/>` + `PublishAot`/`PublishTrimmed`. Library "uses reflection extensively." No officially-supported AOT stance. |
| Migration 1.x→6.x is low-friction | **MOSTLY YES, MINOR BREAKS** | Same core types persist (`PdfDocument`, `PdfPage`, `XGraphics`, `XFont`, `XImage`, `IFontResolver`, `GlobalFontSettings`). Known removals: `XGraphics.MUH` removed (no longer needed); manual `Encoding.RegisterProvider` no longer required (auto-registered). Full surface diff not exhaustively documented — assume small adapter changes. |

**Net assessment of upstream as a swap:** PDFsharp 6.x is genuinely better maintained, net8-native, MIT, and netstandard2.0-capable (matches your abstractions target). But it (1) has the **same per-call `DrawString` allocation model** — it will *not* by itself hit SC4, (2) is only *pragmatically* AOT-capable via trimmer roots, conflicting with your "AOT-trivial / 26.8 MB Alpine image" trajectory, and (3) keeps a third-party library on the core critical path. It is a solid **fallback**, not the strategic answer.

**Pure-managed MIT/Apache alternatives surveyed (build vs. buy for the writer):**
- **PDFsharp / PdfSharpCore (MIT):** same allocation model — rejected as the *solution*, retained as fallback.
- **PdfPig (Apache-2.0):** primarily a *reader*/extractor; creation support is basic and not a high-performance positioned-glyph emitter. Not a fit.
- **QuestPDF:** SkiaSharp-based → **NATIVE (libSkia) → DISQUALIFIED** under the pure-managed constraint, *and* dual-licensed (commercial above $2M revenue) → license-incompatible with open-core. Double-disqualified. **Flagged explicitly per constraint.**
- **SautinSoft / Docotic / Aspose / IronPDF / iText:** commercial (iText AGPL/commercial) → **rejected per hard constraints.**
- **FO.NET (Apache-2.0):** unmaintained, XSL-FO scope — not relevant.

There is **no off-the-shelf pure-managed MIT/Apache PDF *writer* that accepts precomputed glyph runs.** This is precisely the gap that justifies owning the layer.

---

## (a) Options Comparison

Scoring: 5 = best. Effort scored as low-effort = high score.

| Criterion (weight) | Opt 1: Coalesce runs on PdfSharpCore | Opt 2: Migrate to PDFsharp 6.x | Opt 3: Fork PdfSharpCore/PDFsharp | Opt 4: Build owned writer |
|---|---|---|---|---|
| **Effort** (low=better) | 5 (days) | 4 (~1 wk) | 2 (vendoring + ongoing) | 2 (~3–4 wks) |
| **Allocation win** (×3) | 2 (line-coalescing helps, but per-call glyph-mapping pipeline remains; partial) | 1 (same model; no structural win) | 4 (can add a low-level path) | 5 (eliminates the pipeline) |
| **AOT / trim** (×2) | 2 (PdfSharpCore not AOT-validated) | 3 (works only via trimmer roots; reflection-heavy) | 3 (inherits reflection unless gutted) | 5 (no reflection by construction) |
| **Determinism control** (×2) | 3 (still post-processing random tokens) | 3 (still post-processing subset prefix + /ID) | 4 (can emit deterministically at source) | 5 (full control; no NormalizeForDeterminism hacks needed) |
| **License / ownership** (×2) | 3 (3rd-party on core path) | 3 (3rd-party on core path) | 4 (MIT vendored; you own it but inherited code) | 5 (100% owned, zero 3rd-party) |
| **Maintenance** | 3 (depends on stale upstream) | 4 (active upstream) | 2 (you carry the fork forever) | 3 (you own it, but it's small + stable surface) |
| **Golden-baseline impact** (low churn=better) | 1 (breaks all 57 — output bytes change) | 1 (breaks all 57 — different serializer) | 2 (breaks goldens when hot path changes) | 1 (breaks all 57 — but it's a one-time, intentional, owned re-baseline) |
| **Ecosystem fit** (×2) | 2 (entrenches the dependency) | 3 (better dep, still a dep) | 3 (half-owned) | 5 (completes the "own the stack" thesis) |
| **Hits SC4 (≥30%)?** | Maybe (partial) | **No** | Likely | **Yes (high margin)** |

**Weighted leaders:** Option 4 (build) is the clear strategic + technical winner. Option 1 is the cheapest tactical patch but doesn't remove the dependency or fully solve allocation. Option 2 is the safe fallback. Option 3 (fork) is the worst trade — you take on perpetual maintenance of a large inherited codebase to add a path you could write standalone in less code.

**Note on goldens:** every option except a no-op breaks the 57 golden baselines because the output bytes change. This is therefore **not a differentiator against Option 4** — if you must re-baseline anyway, do it once for an owned writer you control forever rather than for a third-party serializer you don't.

---

## (d) Recommendation & Phased Plan

**BUILD the owned writer (Option 4).** Keep PdfSharpCore in the tree until the owned path passes the full golden + determinism suite, then delete it.

### Phase A — De-risk spike (2–3 days, do this FIRST)
- Add an experimental code path: for one page, emit raw content stream with **one `TJ` per line** using existing glyph IDs (`GlyphCollector`) and per-word X advances from `PositionedElement`.
- Wrap it in a minimal hand-written PDF skeleton (header, one page object, one font, xref, trailer) — or, to isolate the measurement, inject the content stream into a PdfSharpCore page if feasible.
- **Gate:** measure WRITE allocation. If it drops the total by ≥30%, proceed to Phase B. If not, fall back to Option 2.

### Phase B — Owned writer core (1.5–2 wks)
- `PdfObjectWriter`: indirect objects, xref table, trailer, deterministic `/ID` and version header **emitted correctly at source** (retire the regex `NormalizeForDeterminism` hacks — DET handled natively).
- Page tree, content stream (FlateDecode), resource dictionaries.
- `TJ`/`Tj` text emitter consuming `PositionedPage` directly (no re-mapping).

### Phase C — Fonts & images (1 wk)
- **Type0/CID font embedding** wired to the existing `TrueTypeFontSubsetter` output (you already produce subset bytes + the `ABCDEF+` prefix — now emit it deterministically instead of post-fixing it).
- Image XObjects from the existing `ImagePipeline`/`DecodedImage` (reuse current JPEG/PNG decode).
- Preserve **SEC-02 invariant** explicitly: the writer has no code path that can emit `/JavaScript`, `/Launch`, `/OpenAction`, `/EmbeddedFile` — easier to guarantee in owned code than to audit in a third party.

### Phase D — Cutover (1 wk)
- Run owned writer behind the `IPdfWriter` interface (already abstracted — `PdfSharpCoreWriter : IPdfWriter`). Swap the DI registration.
- Re-baseline 57 goldens once. Validate with an external PDF validator (e.g. veraPDF / qpdf `--check`) and a render round-trip, not just byte-equality.
- Confirm NativeAOT publish with **no** trimmer-root workarounds; confirm Alpine image size holds/improves.
- Delete the `PdfSharpCore` PackageReference and `PdfSharpFontResolverAdapter` / `GlobalFontSettings` global-state plumbing (removes the once-per-process font-resolver lock hazard at T-05-04).

### Fallback
If Phase A fails the SC4 gate or font/CID embedding proves too risky on schedule: **migrate to upstream PDFsharp 6.x (Option 2)** for the better-maintained, net8-native, netstandard2.0 base; apply line-coalescing (Option 1 technique) on top to recover *some* allocation; accept trimmer-root AOT config; defer the owned writer to a later milestone. This still requires the one-time golden re-baseline.

---

## (e) Risks

| Risk | Severity | Mitigation |
|---|---|---|
| CID/Type0 font embedding is the hard part of PDF (CMap, `/W` widths, `CIDToGIDMap`) | **HIGH** | You already have the subsetter + glyph metrics; reuse them. Validate against veraPDF + open in Acrobat/Chrome/Preview. Time-box; Phase A spike de-risks the text path first. |
| Allocation win underperforms the model | MEDIUM | Phase A gate measures before committing the full build; fallback to Option 2 is pre-defined. |
| Re-baselining 57 goldens hides a real regression | MEDIUM | Don't trust byte-equality alone — add PDF-validator + visual/round-trip checks before regenerating baselines. |
| Edge cases PdfSharpCore handled for free (color spaces, image formats, transparency, complex scripts) | MEDIUM | Scope the owned writer to exactly today's feature set (text + images + simple fills). Keep PdfSharpCore path available behind `IPdfWriter` until parity proven; expand incrementally. |
| Maintenance burden of owning a writer | LOW–MEDIUM | The PDF 1.7 serialization surface you need is small and stable (the spec doesn't move). This is far less code than maintaining a fork (Option 3). |
| RTL/complex-script shaping | LOW (current scope) | Layout/shaping is already yours (SixLabors.Fonts); the writer only emits positioned glyph IDs, so shaping correctness is unchanged by this decision. |

---

## Sources

- [empira/PDFsharp (GitHub)](https://github.com/empira/PDFsharp) — MIT, 6.x, net8-native.
- [PDFsharp 6.2.4 final — target frameworks (Discussion #326)](https://github.com/empira/PDFsharp/discussions/326) — net8/9/10, net4.6, netstandard2.0.
- [PDFsharp Platform Support (DeepWiki)](https://deepwiki.com/empira/PDFsharp/1.2-platform-support)
- [.NET 8 AOT support — PDFsharp forum t=4525](https://forum.pdfsharp.net/viewtopic.php?f=2&t=4525) — partial AOT via TrimMode partial / TrimmerRootAssembly; reflection-heavy.
- [Migrating to v6 — Discussion #183](https://github.com/empira/PDFsharp/discussions/183) — MUH removed, encoding auto-registered.
- [PDFsharp Glyph Mapping docs](https://docs.pdfsharp.net/PDFsharp/Topics/Fonts/Glyph-Mapping.html) — CodePointGlyphIndexPair, RenderTextEvent, Tj/hex-glyph emission, per-call mapping.
- [ststeiger/PdfSharpCore (GitHub)](https://github.com/ststeiger/PdfSharpCore) — unofficial port, MIT, SixLabors-based.
- [PdfSharpCore 1.3.65 (NuGet)](https://www-1.nuget.org/packages/PdfSharpCore/1.3.65)
- [Quest for Permissively Licensed PDF Library in C# (Dürrenberger, 2025-11)](https://duerrenberger.dev/blog/2025/11/04/quest-for-permissively-licensed-pdf-library-in-csharp/) — license/native survey; QuestPDF dual-license + Skia native.
- [QuestPDF (GitHub)](https://github.com/QuestPDF/QuestPDF) — SkiaSharp-based (native), dual-licensed.
- [PDF Graphic Operators Cheat Sheet (PDF Association)](https://pdfa.org/wp-content/uploads/2023/08/PDF-Operators-CheatSheet.pdf) — Tj/TJ operator semantics.

**Confidence:** Diagnosis (writer = allocator; cause = per-word DrawString pipeline) — **HIGH**. Recommendation (build owned writer) — **HIGH**. Exact allocation-win magnitude — **MEDIUM** (confirm in Phase A spike). Upstream PDFsharp facts — **HIGH** (official GitHub/forum/docs).
