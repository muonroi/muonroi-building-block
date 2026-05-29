# Phase 8.16 — Image Polish Sweep (PLAN)

> **Branch:** `phase/08.16-image-polish`
> **Predecessor:** 8.15 (`848767a` — G23g/G23h FIXED)
> **Scope:** Image-related polish before v1 packaging. 1 visual gap + 1 test-harness debt + 1 charter audit.

## Gaps

| ID | Symptom | Root cause | Wave |
|----|---------|------------|------|
| G24 | `<img>` without CSS width/height stretches to container width instead of using intrinsic pixel dims | `BoxTreeBuilder.CreateBox` for `<img>` builds `ReplacedBox` with `Src` only; `NaturalWidth/Height` never populated from `DecodedImage`. Downstream `BlockLayoutEngine` falls back to `childWidth = available` when NaturalWidth==0. | A |
| #33 | Test harness `{{logo}}` template substitution uses 4×4 red placeholder PNG → all golden renders show red blocks where logos should appear | `PdfServiceTestHarness` (or upstream Dummies fixture) embeds a hand-crafted 4×4 red PNG for the `{{logo}}` token; intent was placeholder but blocks visual-diff usefulness. | B |
| C2 | Charter item: 18-template image render audit incomplete (only HBND_F G9, CHNG_E G16 specifically verified) | Audit deferred during 8.7–8.15 chasing layout gaps. With G24 + #33 fixed, full visual diff is now meaningful. | C |

## Wave A — G24 (intrinsic image size)

**Files:** `BoxTreeBuilder.cs`, `Boxes/ReplacedBox.cs` (if NaturalWidth/Height missing), `BlockLayoutEngine.cs` (fallback path)

1. `BoxTreeBuilder` (constructor or `BuildNode`) accepts the `_resolvedImages` dict (already wired for image XObject resolution — see line 578).
2. In `CreateBox` for `<img>`: after creating `ReplacedBox`, lookup `_resolvedImages[src]` if available; set `NaturalWidth = decoded.Width * Units.PxToPt`, `NaturalHeight = decoded.Height * Units.PxToPt`.
3. Verify `BlockLayoutEngine.cs:442-446` fallback chain — `Height` (CSS) → `NaturalHeight` → line-height — already correct for height. Confirm width path uses same priority: `ResolveWidth(replacedChild, ctx)` should return `NaturalWidth` when no `WidthRaw` declared, NOT container width.
4. Unit tests:
   - `<img>` with NaturalWidth=64pt and no CSS width → rendered width = 64pt (NOT container width)
   - `<img>` with `width:100px` overrides NaturalWidth
   - `<img>` with `max-width:50%` clamps NaturalWidth proportionally
   - `<img>` with NaturalWidth=0 (decode failed) falls back to container width (current behaviour preserved as last resort)

## Wave B — #33 (test harness real PNG stub)

**Files:** `tests/Muonroi.Pdf.Tests/Service/PdfServiceTestHarness.cs`, optionally `tests/Muonroi.Pdf.Tests/TestResources/` for the PNG asset.

1. Embed a small (32×32 or 64×64) real PNG asset as base64 — recommend a simple non-red color block or SNP-like motif distinguishable from the legacy red placeholder.
2. Replace any 4×4 red PNG substitution in test harness with the new asset.
3. Regenerate any golden PDFs that depended on the red placeholder (audit `tests/Muonroi.Pdf.Tests/TestResources/Golden/*.pdf` for affected files).
4. Confirm `{{logo}}` token in test templates resolves to the new asset.

## Wave C — C2 (18-template image audit)

**Outputs:** `.planning/phases/08.16-image-polish/AUDIT.md`

1. Render all 18 templates in `D:\Data\Template\Htmls\PreviewRegistion` with current engine.
2. Capture Chrome reference renders for each via `chrome-devtools-mcp` at the same page size.
3. For each template, visually diff first page (focus on image regions). Record:
   - Template name
   - Image count (img tags + bg-image data URIs)
   - Render verdict: MATCH / MINOR / GAP
   - Notes
4. Any GAP-level issue → file as G25+ in `GAPS-AND-DEBT.md`. Trivial fixes (≤1 commit) — fix in 8.16 Wave D. Larger fixes → defer to 8.17.

## Acceptance

- 441/441 prior tests pass + new G24 unit tests
- `{{logo}}` token renders as a real PNG (not red placeholder) in test harness output
- 18-template AUDIT.md complete with verdicts
- Logo render natural-size test reproduces Chrome at intrinsic pixel dims

## Sequencing

- **A** (sonnet, ~1 commit) — orthogonal to B
- **B** (sonnet, ~1 commit) — orthogonal to A; can run in parallel
- **C** (opus visual review, ~1 commit for AUDIT.md + any micro-fixes)
- Close-out (opus) — VERIFICATION.md + GAPS-AND-DEBT.md updates + merge develop

## Out of scope

- GIF / WebP / SVG decoders (no corpus demand)
- background-image full URL resolver (only data URI used in corpus)
- Image lazy-loading / progressive decode
