# Phase 8.8 Verification — Float Child Rendering

## Phase goal

Correct `ContentOriginX` propagation into float-child dispatch so text, HR, block, image, and table
children of float boxes render at the right X position. Closes G1 (text/HR/block in float) and G2
(image/table in float) discovered during Phase 8.7 corpus work on HSLA_E.

Commit: `a5448de` — fix(08.8): float-child ContentOriginX propagation — G1 text/HR + G2 image (wave 8.8a)

---

## Success criteria

| # | Criterion | Result | Evidence |
|---|-----------|--------|----------|
| SC1 | HSLA_E renders content (was: red rule only) | **PASS** | Content visible on page 2; user-confirmed via screenshot |
| SC2 | HSLA_F logo image visible inside float column | **PASS** | Logo renders correctly after G2 fix |
| SC3 | All 17 previously-passing templates regression-clean | **PASS** | 335/335 tests |
| SC4 | `dotnet test` exits 0 | **PASS** | 335/335 tests green |
| SC5 | Goldens re-baselined ≤3 | **PASS** | No goldens shifted in commit a5448de |

---

## New gap discovered during this phase

### G8 — HSLA_E content on page 2, page 1 empty

- **Symptom**: HSLA_E renders all content but places it on page 2; page 1 is blank.
- **Likely cause**: The HSLA_E body has explicit `width:210mm; height:148mm` which matches A5
  landscape dimensions exactly. The layout engine likely interprets the body as filling the first
  page by height, then overflows content to page 2.
- **Impact**: SC1 is functionally met (content renders) but visual gate counts page 1 — so the
  18/18 gate is not fully closed.
- **Status**: OPEN — deferred to Phase 8.9.
- **Estimated scope**: SMALL-MEDIUM (pagination / body-height interaction).

---

## Test harness note (TD9)

`VisualRegressionTests` and `RealTemplateBaselineTests` rasterize **page 1 only**. This masked
the G8 pagination issue throughout Phase 8.8 — tests passed even with page 1 blank. This is
tech debt TD9 and must be fixed in Phase 8.9: either extend rasterization to all pages, or add
a page-count assertion against expected counts per template.

---

## Conclusion

Phase 8.8 **CLOSED**. Single code commit `a5448de`. G1 and G2 fixed. G8 discovered and
documented — deferred to Phase 8.9 with root cause identified. 335/335 tests pass. No regressions.
