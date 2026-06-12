# Phase 8.16 — Image Polish Sweep (VERIFICATION)

> **Closed:** 2026-05-29
> **Branch:** `phase/08.16-image-polish` → merged develop
> **Predecessor:** Phase 8.15 (`848767a` — G23g/G23h FIXED)
> **Scope:** Image-related polish before v1 packaging — 1 visual gap (G24), 1 test-harness debt (#33), 1 charter audit (C2).

## Commits

| # | SHA | Wave | Subject |
|---|-----|------|---------|
| 1 | `ce57636` | — | docs(08.16): open phase — Image Polish Sweep (G24 + #33 + C2) |
| 2 | `0aa34fb` | B | test(08.16): replace 4×4 red placeholder with real PNG stub for {{logo}} (#33) |
| 3 | `1bc6a09` | A | fix(08.16): img NaturalWidth/Height seeded from DecodedImage so intrinsic px→pt size applies when no CSS width/height (G24) |
| 4 | `cdbb526` | C | docs(08.16): 18-template image render audit (C2 — Wave C) |

> Note: Waves A and B ran in parallel; Wave B's commit timestamp landed before Wave A's, and the G24 unit test file (`IntrinsicImageSizeTests.cs`) ended up bundled with Wave B's commit by timing. Both fixes are present on HEAD, all 446+1 tests pass — cosmetic split, not a functional issue.

## Findings & fixes

### G24 — `<img>` without CSS width/height stretches to container

**Root cause:** `BlockLayoutEngine.ResolveWidth` had no branch for `ReplacedBox` — when no CSS `width`/`WidthRaw` was set, it fell through the auto-width path (`ctx.AvailableWidth - margins`), stretching the image to fill the container instead of using its intrinsic px-to-pt size. `ReplacedBox.NaturalWidth/Height` already existed and were already seeded from `DecodedImage` by `BoxTreeBuilder` (line ~582–583), but layout never consulted them on the width axis.

**Fix:** added `else if (box is ReplacedBox { NaturalWidth: > 0f } replaced)` branch in `ResolveWidth` between the explicit-CSS-width case and the auto-width fallback. Uses `replaced.NaturalWidth` (already `DecodedImage.Width * Units.PxToPt`). `max-width`/`min-width` clamps still apply after this branch.

**Goldens regenerated (3):** `image-intrinsic-size.pdf`, `image-jpeg-datauri.pdf`, `image-png-datauri.pdf` — they previously captured the stretched-image regression behaviour; `image-intrinsic-size` was a failing baseline that G24 resolves.

### #33 — Test harness `{{logo}}` real PNG stub

**Root cause:** Dummy templates substituted `{{logo}}` with a 4×4 solid-red PNG (~80 bytes base64). Every visual diff or audit involving logo-bearing templates showed solid red rectangles where logos should appear — useless for visual fidelity work.

**Fix:** introduced `LogoStubTests.RealLogoBase64` — a 32×32 8-bit RGB PNG (320 bytes decoded; blue background with white "M" pattern). Five test fixtures rewired to point `{{logo}}` substitution at the new stub: `RealTemplateBaselineTests`, `TableCellWidthDoubleApplicationTests`, `HslaERootCauseDiagnostic`, `HbndFLogoPositionDiagnostic`, plus the new `LogoStubTests` itself. Barcode slots retained the 4×4 placeholder (those are structural-layout tests where image content is irrelevant).

### C2 — 18-template image render audit

**Outcome:** 17 production templates render OK (one file in the directory was a `.csv` not an HTML — 17 is the corpus). Per-template visual inspection at 100 dpi found:

- 9 templates with `width:170×100` explicit logo declaration: **MATCH** — render at declared size.
- 6 templates with `max-width:Npx + pos:absolute` logo pattern: **MINOR** — engine correctness verified (G9 abs-pos containment + G24 intrinsic sizing both hold). Visual stub appears small because audit PNG is 32×32; production logos at ~300×200 will hit the max-width cap.
- HBL: **MATCH** — 0 images, layout regression-free.
- HSLA_E: **MINOR** — logo `<img>` has no CSS width/height; G24 intrinsic sizing kicks in correctly.

**No new gaps discovered** (G25+ table empty). Background-image data URIs decode and render across 10 templates that use them.

Full per-template results in `AUDIT.md`.

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | `<img>` without CSS width/height uses intrinsic px→pt size, not container width | PASS — `1bc6a09` + 4 unit tests |
| SC2 | `{{logo}}` test substitution emits a real PNG (not 4×4 red) | PASS — `0aa34fb` + 1 integration test |
| SC3 | All 17 production templates render without exception | PASS — `cdbb526` AUDIT.md |
| SC4 | No new image-related gaps (G25+) discovered after G24 + #33 | PASS — empty gaps table in AUDIT |
| SC5 | All prior 441 tests pass + new tests; goldens regenerated where intrinsic sizing changed output | PASS — 447/447 (4 G24 + 1 #33 + 1 audit harness = +6 from 441) |

## Files changed

- `src/Muonroi.Pdf/Internal/Layout/BlockLayoutEngine.cs` — `ResolveWidth` ReplacedBox branch (G24)
- `tests/Muonroi.Pdf.Tests/Layout/IntrinsicImageSizeTests.cs` (new, 4 tests)
- `tests/Muonroi.Pdf.Tests/Service/LogoStubTests.cs` (new, 1 test + `RealLogoBase64` constant)
- `tests/Muonroi.Pdf.Tests/Diagnostic/TemplateImageAudit.cs` (new, 1 audit harness)
- `tests/Muonroi.Pdf.Tests/Golden/RealTemplateBaselineTests.cs` — `Dummies[logo]` → real PNG
- `tests/Muonroi.Pdf.Tests/Layout/TableCellWidthDoubleApplicationTests.cs` — `dummies[logo]` → real PNG
- `tests/Muonroi.Pdf.Tests/Diagnostic/HslaERootCauseDiagnostic.cs` — `{{logo}}` → real PNG
- `tests/Muonroi.Pdf.Tests/Diagnostic/HbndFLogoPositionDiagnostic.cs` — `dummies[logo]` → real PNG
- `tests/Muonroi.Pdf.Tests/TestResources/Golden/image-intrinsic-size.pdf` — regen post-G24
- `tests/Muonroi.Pdf.Tests/TestResources/Golden/image-jpeg-datauri.pdf` — regen post-G24
- `tests/Muonroi.Pdf.Tests/TestResources/Golden/image-png-datauri.pdf` — regen post-G24

## Lessons learned

- **The pre-G24 visual symptom looked like an engine bug, but the trigger was test fixture noise.** User reported "ảnh chỉ tô đỏ" — investigation traced this to a 4×4 red PNG embedded in the user's filled HTML and replicated by the test harness. The actual G24 engine gap (stretched intrinsic size) was a separate, subtler issue surfaced only by rendering the real SNP logo at full size. Lesson: when a visual symptom is "wrong colour", check the input bytes before the rendering pipeline. When the symptom is "wrong size", suspect the layout cascade.
- **Auto-width fallback should never be the first stop for replaced elements.** `ResolveWidth` had a complete cascade for block elements (explicit → percentage → max-width → auto) but no replaced-element branch. The fix slotted in cleanly between the explicit and auto cases. Any future replaced-element types (video, iframe, canvas if ever supported) should hit the same branch shape.
- **Visual audits are cheap once tooling is in place.** Spawning a single test that renders all 17 templates + rasterising via pdftoppm took ~1 minute end-to-end. The audit harness `TemplateImageAudit.cs` is now a permanent diagnostic for future image-related phases — no need to rebuild it each time.

## References

- `.planning/phases/08.16-image-polish/PLAN.md`
- `.planning/phases/08.16-image-polish/AUDIT.md`
- `.planning/GAPS-AND-DEBT.md`
- CSS 2.1 §10.3 — replaced element width
- CSS 2.1 §10.6 — replaced element height
