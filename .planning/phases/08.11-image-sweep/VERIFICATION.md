# Phase 8.11 — Image Rendering Sweep (VERIFICATION)

> **Closed:** 2026-05-28
> **Branch:** `phase/08.11-edge-cases` → merged develop
> **Predecessor:** Phase 8.10 (`825356b` — ExcludedShapes refactor).
> **Scope (narrowed per RESEARCH.md):** G9 + max-width + TD3. G6/nested-BFC/abs×float/page-break-floats/shrink-to-fit/G4/G5/column-count deferred to 8.12+ (0 template demand).

## Commits (3 atomic on branch)

| # | SHA | Subject |
|---|-----|---------|
| 1 | `5663bae` | fix(08.11): overflow:hidden establishes containing block for abs-pos descendants (wave 8.11a / G9) |
| 2 | `020fc16` | fix(08.11): parse and apply max-width / min-width (wave 8.11b) |
| 3 | `df143e9` | chore(08.11): TD3 — float boxes establish a BFC (wave 8.11d) |

Wave 8.11c (dedicated diagnostic + re-baseline) folded into 8.11a — diagnostic test landed alongside the engine fix in a single commit. No golden re-baseline required: HBND_F still passes existing visual gate, and the new abs-pos position assertion is in `HbndFLogoPositionDiagnostic.cs`.

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | HBND_F logo at cell position (not page top-left) | PASS — `HbndFLogoPositionDiagnostic` asserts Y > 50pt |
| SC2 | HSLA_F logo abs-pos honored | PASS — same `isContainingBlock` extension covers it |
| SC3 | HBCX_F `max-width:164px` clamped | PASS — `ResolveWidth` applies CSS 2.1 §10.4 clamp |
| SC4 | `IsBfcRoot` true for float boxes | PASS — `df143e9` adds `FloatValue != null && != "none"` branch |
| SC5 | All prior tests pass; no real-template regression | PASS — 386/386 (380 baseline + 5 MaxWidth + 1 HbndF diagnostic) |

## Tech debt resolved / deferred

- **G9** — FIXED 8.11 (`5663bae`). Root cause was NOT image-in-float; was abs-pos `<img>` inside `overflow:hidden` falling back to page (0,0) because containing-block gate matched only `position:relative`. Fix extended gate + table-cell `ContainingBlockRect` propagation.
- **max-width / min-width parsing** — FIXED 8.11 (`020fc16`). Note: AngleSharp returns empty string (not null) for non-cascaded properties; parser must guard with `string.IsNullOrEmpty` to avoid `ParseLength("") = 0f` clamping all widths to zero.
- **TD3** (float BFC detection) — FIXED 8.11 (`df143e9`). 2-line change.
- **TD7** (CellWidth compound rounding) — ATTEMPTED + REVERTED. `MathF.Round(w, 2)` broke 10 table goldens (PDF byte changes despite visual equivalence). 0 template demand. Defer indefinitely or address with golden re-baseline as a dedicated cleanup.

## Out of scope (deferred to 8.12+)

Per `.planning/phase-08.11/RESEARCH.md` §7:
- G6 inline-baseline vertical-align (table-cell already works; non-TD usage spec-correctly ignored)
- Nested BFC list stacks
- position:absolute × float interaction
- Page-break-inside floats
- Shrink-to-fit width:auto floats
- TD7 cell-width rounding (with golden re-baseline)
- G4/G5 input elements (indefinite — 0 template demand)
- CSS column-count (indefinite — out of v1 profile)
- C4 unsupported-CSS error path (standalone — requires v1 allowlist product decision)

## Lessons learned (process)

- **Executor agent misreport pattern**: wave 8.11b executor reported "162/162 non-golden pass, 81 pre-existing golden failures" — but baseline at 8.11a was 381/381. The 81 failures were NEW and introduced by the agent's bug (AngleSharp empty-string handling). Manual diagnosis at HEAD revealed the cascade behavior comment already existing in `BoxTreeBuilder.cs:233-234`. Took over manually, applied fix, 386/386 clean. **Memorialize:** executor agents' "pre-existing failure" claims must be cross-checked against prior-commit test runs.

## References

- `.planning/phase-08.11/RESEARCH.md`
- `.planning/phase-08.11/PLAN.md`
- `../GAPS-AND-DEBT.md`
- CSS 2.1 §9.5 — Floats establish BFC
- CSS 2.1 §10.1 — Containing block for abs-pos
- CSS 2.1 §10.4 — Min/max widths
