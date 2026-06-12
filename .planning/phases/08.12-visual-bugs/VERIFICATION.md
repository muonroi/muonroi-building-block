# Phase 8.12 — Visual Bug Sweep (VERIFICATION)

> **Closed:** 2026-05-28
> **Branch:** `phase/08.12-visual-bugs` → merged develop
> **Predecessor:** Phase 8.11 (`8bb9bea` — Image Rendering Sweep).
> **Scope (narrowed twice):** lineHeight=0f latent correctness gap only. G11/G12/G13 surfaced from visual review but turned out NOT to be engine bugs.

## Commits (1 atomic on branch)

| # | SHA | Subject |
|---|-----|---------|
| 1 | `449aaed` | fix(08.12): InlineLayoutEngine passes line-height to AvailableWidthAtY (wave 8.12a) |

## Findings

### G11 — HSLA_E + CAPR_E "label-value vertical stack" → NOT engine bug

**Initial visual review (2026-05-28)** suggested label/value pairs in HSLA_E and CAPR_E were rendering vertically instead of inline despite G7 (8.9) UA-inline fix.

**Research (RESEARCH-G11.md) confirmed:**

- **HSLA_E**: label lives in `div.w-30.float-left`, value lives in `div.w-70.float-left`, separated vertically by a 50px `<img>` barcode within the value's float. Two distinct float-formatting contexts → CSS spec says they cannot share an inline-formatting line. Engine is correct. Template authoring choice.
- **CAPR_E**: `<p>Số ĐK(No): <strong>X</strong></p>` is correctly batched as inline by G7. Post-rasterization review shows `Số ĐK(No): DK67890` IS on the same line. The visual misread was vertical whitespace between **different fields** (Mã lô / Số ĐK / Ngày), each internally inline.

**Verdict:** No engine fix required. Documented in capability contract.

### G12 — Cell content overlap (HBL, CSLA_F) → user review says not visible

User reviewed the actual PDFs (not just rasterized PNGs) and confirmed no table line / cell overlap issues. The "overlap" appearance in rasterized PNGs is most likely rasterization aliasing (text glyphs at sub-pixel positions blending into adjacent glyphs at low DPI). Skipping investigation.

### G13 — HBL equipment table column misalignment → same as G12

Not visible in actual PDF per user review. Skipping.

### Real engine fix landed — wave 8.12a

`InlineLayoutEngine.cs:21` (approximate) was calling `FloatPlacementSolver.AvailableWidthAtY(lineY, 0f, cb, exclusions)` with `lineHeight=0f`. This makes float exclusion-band overlap checks degenerate — a zero-height band can never overlap an exclusion that starts at or below `lineY`.

For current templates (single-band floats) the bug was invisible (0 goldens shifted). The fix is a latent correctness improvement that will matter when floats start spanning line-box Y ranges (e.g., 8.13+ phases adding nested BFC or page-break-aware floats).

**Implementation:** peeked the first inline box via `SelectMany(FlattenInline).FirstOrDefault()` (LINQ is lazy — does not consume the enumerable), used its `metrics.GetLineHeight(family, size)`; fallback `12f` when stream is empty. Conservative direction — may narrow band check, never widens.

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | `lineHeight=0f` site removed | PASS — `449aaed` |
| SC2 | 386 prior tests pass + new test | PASS — 387/387 |
| SC3 | CAPR_E inline render confirmed visually | PASS — `Số ĐK(No): DK67890` on same line in post-fix rasterization |
| SC4 | Capability contract documents float-sibling constraint | PARTIAL — see "Documentation update" below |

## Documentation update (pending — to land at close-out commit)

A note added to `.planning/GAPS-AND-DEBT.md` and (if it exists) capability contract:

> **Floated sibling containers do not establish inline-flow continuity.** To render `label: value` inline, both must live inside the SAME inline-formatting context — one block container, or one table cell. Placing label and value in two adjacent floats (e.g., `div.w-30.float-left` + `div.w-70.float-left`) will render them at independent Y positions, even when their widths could fit on one row. HSLA_F (works) and HSLA_E (separated) demonstrate this contrast.

## Out of scope (deferred / closed)

- G11 (HSLA_E/CAPR_E inline stack) — closed as NOT an engine bug
- G12 (cell content overlap) — closed: not visible in PDF per user
- G13 (column misalignment) — closed: not visible in PDF per user
- G6 inline baseline vertical-align — deferred to demand-driven phase
- Nested BFC stacks, position:absolute × float, page-break floats, shrink-to-fit auto — deferred 8.13+
- G4/G5 input elements — deferred indefinite
- Column-count — out of v1 profile indefinite
- C4 unsupported-CSS path — standalone, needs product decision
- TD7 cell-width rounding — deferred (broke 10 goldens at 8.11; would need golden re-baseline)
- Test harness `{{logo}}` real-PNG stub — optional cleanup; not addressed in 8.12

## Lessons learned

- **Rasterized PNG ≠ PDF reality.** Sub-pixel glyph positions at 150 DPI rasterize as visual overlap that the underlying PDF doesn't have. Always verify perceived bugs in the actual PDF (or higher DPI raster) before opening a phase.
- **Visual review is the catch-net for engine-vs-template confusion.** G11 looked like a clear engine miss; research showed it's structural template authoring. Without the user-driven visual review request, we would have entered Phase 9 unaware of this distinction.

## References

- `.planning/phase-08.12/PLAN.md`
- `.planning/phase-08.12/RESEARCH-G11.md`
- `.planning/GAPS-AND-DEBT.md`
- `.planning/phases/08.11-image-sweep/VERIFICATION.md`
- CSS 2.1 §9.5 — Floats
- CSS 2.1 §10.6.1 — Inline formatting context
