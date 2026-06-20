---
phase: 12-owned-css-cascade-b1
verified: 2026-06-19T18:20:00Z
status: passed
score: 4/4 plans' must-haves verified (B1.1 success criteria)
overrides_applied: 0
re_verification:
  previous_status: none
  note: initial verification
---

# Phase 12: Owned CSS Cascade (B1.1) Verification Report

**Phase Goal:** Replace AngleSharp.Css `GetComputedStyle` (beta, throws on em/rem/% headless) with an owned cascade; demote AngleSharp.Css to a parser. B1.1 slice — G14–G29 BoxTreeBuilder fallbacks are KEPT (deleted later in B1.3).
**Verified:** 2026-06-19T18:20:00Z
**Status:** passed
**Re-verification:** No — initial verification

## Goal Achievement

B1.1 success criteria (per 12-CONTEXT + plan frontmatter): owned cascade built and wired; `GetComputedStyle`/`ComputeCurrentStyle` removed; Abstractions interfaces unchanged; full suite green; simple-doc goldens byte-identical; only %-cases re-baselined.

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `CssRuleSet`, `CascadeResolver`, `OwnedComputedStyle`, `OwnedStyledNode` exist as the owned cascade | ✓ VERIFIED | All four `internal sealed class` types found: `CssRuleSet.cs:28`, `CascadeResolver.cs:23`, `OwnedComputedStyle.cs:9` (`: IComputedStyle`), `OwnedStyledNode.cs:21` (`: IStyledNode`) |
| 2 | `AngleSharpStyledDocument` builds one `CssRuleSet` + `CascadeResolver`, root = `OwnedStyledNode` | ✓ VERIFIED | `AngleSharpStyledDocument.cs:27` `CssRuleSet.FromDocument(document)`; `:28` `new CascadeResolver(ruleSet)`; `:29` `Root = new OwnedStyledNode(...)` |
| 3 | `OwnedStyledNode.Style` lazily resolves via `CascadeResolver`, threads parent map, caches per node | ✓ VERIFIED | `OwnedStyledNode.cs:87` `_resolver.Resolve(el, _parentResolved)`; cached in `_cachedStyle`/`_resolvedMap` (`:81-88`); children constructed with this node's map (`:114`) |
| 4 | Selector matching uses AngleSharp core `element.Matches` (no em/rem throw) | ✓ VERIFIED | `CascadeResolver.cs:221` `element.Matches(rule.SelectorText)` |
| 5 | Zero `GetComputedStyle`/`ComputeCurrentStyle` CALLS under Cascade/ (doc-comments OK) | ✓ VERIFIED | grep: all 9 hits are XML doc-comments / inline comments stating absence; no invocation. `AngleSharpComputedStyle.cs` retains no call either |
| 6 | `AngleSharpStyledNode.cs` deleted; the `catch(ArgumentException)` inline-only path gone | ✓ VERIFIED | File absent from Cascade/; deletion in commit `f5853112` (`git log --diff-filter=D`) |
| 7 | `@page` (`AngleSharpPageRule.TryExtract`) + `@font-face` (`ExtractFontFaces`) unchanged | ✓ VERIFIED | `AngleSharpStyledDocument.cs:35-36` both still called; `ExtractFontFaces` still defined `:81` |
| 8 | Abstractions Engine interfaces (`IComputedStyle`/`IStyledNode`/`IStyledDocument`) unchanged | ✓ VERIFIED | Last commit touching these = `063d7aee` (2026-05-27) / `14c8ad2e` (Phase 04); none in phase-12 commit range. Byte-unchanged by phase 12 |
| 9 | BoxTreeBuilder G14–G29 fallbacks KEPT (B1.1, not deleted) | ✓ VERIFIED | `git grep -c` shows 31 occurrences of `LookupClassProperty`/`LookupDescendantClassProperty`/`ParseInlineStyleProperty` still in `BoxTreeBuilder.cs` |
| 10 | Full `Muonroi.Pdf.Tests` suite green | ✓ VERIFIED | **Ran it myself:** `534 passed, 0 failed, 0 skipped` (22s). Includes CssRuleSet 6, CascadeResolver 22, wiring 7, determinism canary |
| 11 | Simple-doc / table / TCIS goldens byte-identical | ✓ VERIFIED | Net `git diff --stat 3fe80579..HEAD` on `Golden/*.pdf` shows ONLY 3 files changed; all simple-doc/table/TCIS goldens unchanged (12-03 blanket regeneration reverted in `a26b8d88`) |
| 12 | Only the 3 %-cases re-baselined | ✓ VERIFIED | Net golden diff = `abs-pos-percent-top.pdf`, `float-clear-below.pdf`, `float-two-column.pdf` only (3 files). Re-baselined in `61363ba3` (12-04) |
| 13 | Determinism canary unaffected | ✓ VERIFIED | `DeterminismCanaryTests.cs` present and passing within the green 534/0 run; not re-baselined |

**Score:** 13/13 truths verified — all 4 plans' must-haves satisfied.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Cascade/CssRuleSet.cs` | Rule index, grouped-selector split, specificity, source order, declarations + !important | ✓ VERIFIED | 18KB; `class CssRuleSet`; `StyleSheets` walk + `GetPropertyPriority`; supplemental raw-text parser for CSS3 props dropped by beta.147 |
| `Cascade/CascadeResolver.cs` | 7-step cascade: match, sort, inline overlay, shorthand, UA defaults, inherit, em/rem→px | ✓ VERIFIED | 34KB (>200 line min); `class CascadeResolver`; `element.Matches` at :221 |
| `Cascade/OwnedComputedStyle.cs` | `IComputedStyle` over resolved map | ✓ VERIFIED | `: IComputedStyle`; `Empty` singleton |
| `Cascade/OwnedStyledNode.cs` | `IStyledNode` via resolver, parent-threaded inheritance, per-node cache | ✓ VERIFIED | `: IStyledNode`; lazy resolve + cache |
| `Cascade/AngleSharpStyledDocument.cs` | Builds rule set once; root OwnedStyledNode; @page/@font-face kept | ✓ VERIFIED | Rewired; metrics + extraction preserved |
| `tests/.../Cascade/CssRuleSetTests.cs` | Collection/split/specificity/!important | ✓ VERIFIED | Present; passing in suite run |
| `tests/.../Cascade/CascadeResolverTests.cs` | Cascade incl. G25/G27/G28/G29 | ✓ VERIFIED | Present; passing in suite run |
| `tests/.../Cascade/OwnedStyledNodeWiringTests.cs` | E2E wiring; width:50% no longer empty | ✓ VERIFIED | Present; passing in suite run |
| `tests/.../TestResources/Golden/` (3 %-pdfs) | Re-baselined %-cases | ✓ VERIFIED | Exactly 3 changed, net |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|----|--------|---------|
| AngleSharpStyledDocument | CssRuleSet + CascadeResolver + OwnedStyledNode | ctor builds rule set + resolver, root node | ✓ WIRED | `:27-29` |
| OwnedStyledNode | CascadeResolver.Resolve | lazy Style getter threading parent map | ✓ WIRED | `:87` |
| CascadeResolver | element.Matches | AngleSharp core matching (non-throwing) | ✓ WIRED | `:221` |
| CascadeResolver | CssRuleSet | consumes CssMatchableRule entries | ✓ WIRED | ctor takes CssRuleSet |
| owned cascade | golden snapshot suite | full render path | ✓ WIRED | 534/0 green, only 3 %-goldens moved |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full suite green | `dotnet test tests/Muonroi.Pdf.Tests/...` | `534 passed, 0 failed` (22s) | ✓ PASS |
| AngleSharpStyledNode deleted | `git log --diff-filter=D` | Deleted in `f5853112`; file absent | ✓ PASS |
| Zero GetComputedStyle calls | grep Cascade/ | 9 hits, all comments | ✓ PASS |
| Net golden change = 3 %-cases | `git diff --stat 3fe80579..HEAD Golden/*.pdf` | 3 files | ✓ PASS |

### Requirements Coverage

Phase 12 declares `requirements: []` in all plans — driven by ROADMAP success criteria + 12-CONTEXT decisions, no registered REQ-IDs. REQUIREMENTS.md maps no IDs to Phase 12. No orphaned requirements.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Cascade/* | — | TODO/FIXME/XXX/TBD/HACK | none | ℹ️ None found in any cascade source |
| Cascade/* | — | bare `catch {}` | none | ℹ️ None found; catches log selector/property + message (No-Silent-Catch honored) |
| `Internal/Layout/Geometry/Rect.cs` | 1-11 | Layout file changed in phase-12 commit range (`e6a581ba "fixn bug"`) despite plans forbidding Layout edits | ℹ️ Info | Cosmetic primary-constructor refactor (behaviorally identical); suite green; G14–G29 fallbacks intact. Not a goal regression. |

### Code Review Disposition (12-REVIEW.md, 11 findings)

- **CR-01 (BLOCKER — shared-list aliasing in supplemental map):** ✓ FIXED in commit `608a57de`. Verified at `CssRuleSet.cs:215` — `new List<CssDeclaration>(supplementalDecls)` per key (defensive copy), exactly as the review prescribed.
- **WR-01..WR-06, IN-01..IN-04 (warnings/info):** correctness edge cases (em vs inherited %-font-size, specificity packing radix, `! important` spacing, custom-tag default display, shorthand-after-longhand ordering, silent shorthand discard, children list caching). Not exercised by the green golden corpus; out of the B1.1 contract. Appropriate to carry into B1.2/B1.3 as hardening — NOT B1.1 goal blockers.

### Gaps Summary

No gaps blocking the B1.1 goal. Every B1.1 success criterion is verified directly against the codebase, not the SUMMARY narrative:
- Owned cascade (4 types) built and wired through `AngleSharpStyledDocument` → `OwnedStyledNode` → `CascadeResolver.Resolve` → `OwnedComputedStyle`.
- `GetComputedStyle`/`ComputeCurrentStyle` calls eliminated (comments only); `AngleSharpStyledNode.cs` deleted; `catch(ArgumentException)` path gone.
- Abstractions Engine interfaces byte-unchanged by phase 12; BoxTreeBuilder G14–G29 fallbacks kept (31 references).
- Full suite green (534/0) — independently re-run, matching SUMMARY.
- Only the 3 %-cases re-baselined; all other goldens byte-identical; determinism canary green.

The B1.1 BLOCKER from review (CR-01) was fixed before this verification and is confirmed in source.

**Note for the human (not a blocker):** Two stray, poorly-described commits (`e6a581ba "fixn bug"`, `c8769847 "fxi using"`) landed inside the phase-12 commit range. `e6a581ba` touched `Internal/Layout/Geometry/Rect.cs`, which the plans explicitly placed off-limits. The change is a behaviorally-identical primary-constructor refactor and the suite is green, so it is not a goal regression — but it is process noise worth a glance before merge.

---

_Verified: 2026-06-19T18:20:00Z_
_Verifier: Claude (gsd-verifier)_
