# Phase 8.10 — ExcludedShapes Float Refactor (VERIFICATION)

> **Closed:** 2026-05-28
> **Branch:** `phase/08.10-excluded-shapes` → merged develop
> **Predecessor:** Phase 8.9 (commit `a5fa3b2` on develop)
> **Goal achieved:** Byte-identical float layout via WeasyPrint `avoid_collisions` algorithm; cursor model removed.

## Commits (6 atomic, sequential)

| # | SHA | Subject |
|---|-----|---------|
| 1 | `25508bc` | feat(layout): add FloatExclusion + FloatPlacementSolver stubs |
| 2 | `ee952d3` | feat(layout): implement FloatPlacementSolver with unit tests |
| 3 | `807e050` | feat(layout): mirror float placements into Exclusions list alongside cursors |
| 4 | `2d61007` | feat(layout): flip all float reads to FloatPlacementSolver |
| 5 | `289a11f` | refactor(layout): remove cursor fields; Exclusions list is sole float state |
| 6 | `6687c2d` | feat(layout): add clear:left/right/both tests and verify ClearY behavior |

## Success criteria

| ID | Criterion | Result |
|----|-----------|--------|
| SC1 | New types added without behavior change (Step 1) | PASS — 363/363 |
| SC2 | Solver unit-tested with 14 synthetic cases (Step 2) | PASS — 377/377 |
| SC3 | Mirror writes do not change golden coordinates (Step 3) | PASS — 34 real-template tests green |
| SC4 | Read-flip produces byte-equivalent output (Step 4) | PASS — 377/377, no PDF coord delta |
| SC5 | Cursor fields fully removed (Step 5) | PASS — 0 grep hits, -41 LOC, 377/377 |
| SC6 | `clear:` tests added (Step 6) | PASS — 380/380 |

## Test summary

- **Pre-8.10 baseline:** 363 tests (post-8.9).
- **Final:** 380 tests (14 solver unit tests + 3 ClearY tests).
- **Real-template golden:** 34/34, byte-identical PDFs.
- **No regression** on HSLA_E (page 1 float columns), HSLA_F (table), HBL, HBND_F.

## Architectural change

**Before (cursor model — TD2/TD3/TD4/TD5/TD10):**
- Four scalar cursors `LeftFloatRight`, `RightFloatLeft`, `LeftFloatBottom`, `RightFloatBottom`.
- Reads scattered across `BlockLayoutEngine`, `InlineLayoutEngine`.
- Fragile for nested BFC, position:absolute, multi-line floats.

**After (ExcludedShapes model):**
- Single `List<FloatExclusion>` in `LayoutContext` (per BFC).
- `FloatPlacementSolver` static helper: `AvoidCollisions`, `AvailableWidthAtY`, `ClearY`.
- All float geometry derived from the immutable exclusion record set.
- Foundation for nested BFC stacks, page-break-aware floats, shrink-to-fit auto (8.11).

## Tech debt resolved

- **TD2** (cursor-based float positioning, fragile for nested BFC) — RESOLVED.
- **TD3** (float BFC detection) — UNCHANGED; still uses `IsBfcRoot(box)` but no longer relies on cursor reset semantics.
- **TD4** (float context propagation) — RESOLVED via shared `Exclusions` list reference.
- **TD5** (right-float symmetric fix coupling) — RESOLVED; solver handles both sides uniformly.
- **TD10** (Bug 7 symmetric `BlockBox` blockX origin) — RESOLVED via `AvailableWidthAtY(startY, 0f, cb, exclusions).StartX`.

## Out of scope (deferred to 8.11)

Per PLAN §6:
1. Nested BFC list stacks — single shared list per RunLayout retained.
2. `position:absolute` float interaction — abs-pos still post-pass.
3. Page-break-inside floats — exclusion list not persisted across page boundaries.
4. Shrink-to-fit `width:auto` floats — still requires explicit/percentage width.
5. Column-count interaction — out of v1 profile.

## Open gaps after 8.10

- **G9** (image inside float renders as colored placeholder, HBND_F) — UNCHANGED; investigation deferred to dedicated sub-phase or 8.11.

## References

- `.planning/phase-08.10/PLAN.md` — design spec.
- `.planning/phase-08.7/RESEARCH-OSS-REFS.md` §1 — WeasyPrint avoid_collisions pseudocode source.
- CSS 2.1 §9.5 — https://www.w3.org/TR/CSS21/visuren.html#floats
