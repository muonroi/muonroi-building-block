---
phase: 12-owned-css-cascade-b1
plan: 04
subsystem: cascade
tags: [golden-regression, visual-verification, re-baseline, owned-cascade, verification-wave]

# Dependency graph
requires:
  - phase: 12-owned-css-cascade-b1
    plan: 03
    provides: owned cascade wired through the seam + completed UA defaults (gap-closure)
provides:
  - Full Muonroi.Pdf.Tests suite green (534 passed, 0 failed) against the owned cascade
  - 3 %-handling goldens re-baselined after visual verification (float-two-column, float-clear-below, abs-pos-percent-top)
  - Confirmation: simple-doc / table / TCIS goldens byte-identical; determinism canary unaffected

affects:
  - B1.2 (policy migration), B1.3 (delete G14–G29 fallbacks) — now safe to build on a green owned cascade

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Targeted re-baseline via DisplayName filter + MUONROI_UPDATE_SNAPSHOTS=1 (NOT whole-suite) — only the approved cases rewritten"
    - "git status diff proves only approved %-table .pdf changed (T-12-08 mitigation)"
    - "Blocking human-verify checkpoint with old-vs-new PNG renders before any baseline written (T-12-09 mitigation)"

key-files:
  modified:
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/abs-pos-percent-top.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/float-two-column.pdf
    - tests/Muonroi.Pdf.Tests/TestResources/Golden/float-clear-below.pdf

key-decisions:
  - "Task 1 classification (with 12-03 gap-closure already applied): only 3 cases changed, all %-handling — zero category-(b) simple-doc regressions, zero category-(c) unit failures. The TCIS %-table corpus rendered byte-identical because B1.1 KEEPS the G14–G29 BoxTreeBuilder fallbacks (they still fire), so the cascade swap did not move %-table output."
  - "Checkpoint: floats verified as improvements (LEFT/RIGHT align on the same top line vs the old right-column drop). abs-pos-percent-top lost its visible overlay text — flagged as a likely regression; user approved re-baselining all 3 (accepting the new abs-pos render)."
  - "Re-baseline scoped to exactly the 3 approved cases via DisplayName filter — whole-suite regeneration explicitly avoided (T-12-08)."

# Metrics
duration: ~orchestrator verification wave
completed: 2026-06-19
---

# Phase 12 Plan 04: Final verification — full suite green, %-goldens re-baselined

**Full Muonroi.Pdf.Tests suite green (534 passed / 0 failed) against the owned cascade. Only 3 genuine %-handling goldens changed; each visually verified at the blocking checkpoint and re-baselined. Simple-doc / table / TCIS goldens are byte-identical and the determinism canary is unaffected.**

## Task 1 — Full suite run + classification

With `MUONROI_UPDATE_SNAPSHOTS` unset (and the 12-03 UA-default gap-closure applied), the full suite produced exactly 3 golden mismatches:

| Case | Class | Why it changed |
|------|-------|----------------|
| float-two-column | (a) %-handling | `width:40%` floats now align on the same top line |
| float-clear-below | (a) %-handling | same float-alignment improvement; BELOW still cleared |
| abs-pos-percent-top | (a) %-handling | `top:50%` absolute positioning resolved differently |

- **Zero category-(b)** simple-doc / non-%-table mismatches (byte-identity held — the 12-03 gap-closure restored AngleSharp's UA defaults).
- **Zero category-(c)** non-golden unit/integration failures.
- **Determinism canary green** without re-baseline.
- Notably, the TCIS HBCX %-table corpus and all `table-*` goldens rendered byte-identical — the kept G14–G29 BoxTreeBuilder fallbacks still drive %-table layout in B1.1, so the cascade swap did not move their output (fewer re-baselines than the plan anticipated).

## Checkpoint — Visual verification (blocking, human-verify)

Rendered old-vs-new PNGs (150 DPI) for all 3 cases and presented them:
- **float-two-column / float-clear-below**: NEW aligns LEFT and RIGHT floats on the same top line (OLD dropped the right column lower) — verified improvements.
- **abs-pos-percent-top**: NEW no longer shows the `top:50%` overlay's "50%" text — flagged as a likely regression.

**User verdict: approved all 3** (accepting the new abs-pos render).

## Task 2 — Re-baseline + green suite

- Regenerated ONLY the 3 approved cases via `--filter "DisplayName~..."` + `MUONROI_UPDATE_SNAPSHOTS=1` (whole-suite regeneration avoided — T-12-08).
- `git status` confirmed only the 3 approved `.pdf` files changed; no simple-doc baseline touched.
- Full suite re-run (env unset): **534 passed, 0 failed** (Pre-Push Test Gate satisfied).

## Deviations from Plan

- The plan expected the TCIS %-table corpus to be the re-baseline set. In practice, with the G14–G29 fallbacks kept (B1.1) and the 12-03 UA-default gap-closure applied, only 3 non-table %-cases changed. Re-baseline scope was therefore smaller and different than anticipated.
- `abs-pos-percent-top` overlay-text loss was flagged as a possible regression at the checkpoint; user explicitly approved re-baselining it. Worth a follow-up look in B1.2/B1.3 if abs-pos+% fidelity matters for the profile.

## Follow-up note

`abs-pos-percent-top`: the `position:absolute; top:50%` overlay text is no longer visible in the owned-cascade render. Re-baselined per user approval, but if absolute-positioning + percentage fidelity is in-profile, revisit the abs-pos/% path (likely a BoxTreeBuilder fallback interaction) in a later wave.

## Self-Check: PASSED

- Full `Muonroi.Pdf.Tests`: 534 passed, 0 failed — VERIFIED
- Only 3 approved %-goldens modified under TestResources/Golden — VERIFIED via git status
- No simple-doc baseline modified — VERIFIED
- Determinism canary green within the full run — VERIFIED
- Commit `61363ba3` — FOUND

---
*Phase: 12-owned-css-cascade-b1*
*Completed: 2026-06-19*
