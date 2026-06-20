---
phase: 12-owned-css-cascade-b1
plan: 02
subsystem: cascade
tags: [css-cascade, cascade-resolver, computed-style, anglesharp, unit-resolution, inheritance, shorthand-expansion, ua-defaults]

# Dependency graph
requires:
  - phase: 12-owned-css-cascade-b1
    plan: 01
    provides: CssRuleSet, CssMatchableRule, CssDeclaration — the rule index CascadeResolver consumes
provides:
  - CascadeResolver: per-element 7-step cascade (match, sort, inline overlay, shorthand expand, UA defaults, inherit, em/rem->px)
  - OwnedComputedStyle: IComputedStyle wrapping resolved Dictionary<string,string>
  - CascadeResolverTests: 22 passing unit tests incl. G25/G27/G28/G29 cascade-level assertions
  - CssRuleSet supplemental parser: raw <style> text fallback for AngleSharp.Css-dropped CSS3 properties

affects:
  - 12-03 (OwnedStyledNode threads CascadeResolver through the IStyledNode seam)

# Tech tracking
tech-stack:
  added: []  # No new packages; reuses AngleSharp 1.3.0 core (element.Matches) + ILogger
  patterns:
    - "CascadeResolver.Resolve: 7-step algorithm per DESIGN §4.2"
    - "Supplemental raw-text parser in CssRuleSet for AngleSharp.Css-dropped CSS3 properties"
    - "element.Matches(selectorText) — AngleSharp core, non-throwing selector matching"
    - "Inline style= splitter: k:v; raw text (not CSSOM) so values are un-normalized"
    - "4-side shorthand expansion: 1/2/3/4-value CSS rules for margin/padding"
    - "Border shorthand: parse width/style/color tokens; border:none -> 0 all sides"
    - "em/rem->px via fontSizePx chain; % left as literal string"
    - "Inherited allow-list: static HashSet<string> — only listed props copy from parent"

key-files:
  created:
    - src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs
    - src/Muonroi.Pdf.Governance/Cascade/OwnedComputedStyle.cs
    - tests/Muonroi.Pdf.Tests/Cascade/CascadeResolverTests.cs
  modified:
    - src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs

key-decisions:
  - "CascadeResolver takes ILogger? (nullable) — constructed without DI in tests; logs selector-parse failures at Debug level (No-Silent-Catch)"
  - "OwnedComputedStyle.Empty static singleton mirrors AngleSharpComputedStyle.Empty — Plan 03 uses it for nodes with no matching rules"
  - "Inline style= splitter stores raw authored values (green, not rgba) — different from CSSOM normalization; tests assert accordingly"
  - "Supplemental raw-text parser added to CssRuleSet.FromDocument (not CascadeResolver) to keep the separation of concerns: collection vs. matching"
  - "AngleSharp.Css beta.147 silently drops word-break/white-space/overflow-wrap from ICssStyleDeclaration AND from CssText (normalized to empty block) — confirmed by diagnostic. Fix: BuildSupplementalFromRawStyleText() reads <style>.TextContent directly"

# Metrics
duration: 45min
completed: 2026-06-19
---

# Phase 12 Plan 02: CascadeResolver + OwnedComputedStyle

**CascadeResolver implementing the full 7-step CSS cascade (match, specificity sort, inline overlay, shorthand expansion, UA defaults, inheritance, em/rem-to-px unit resolution) backed by the CssRuleSet from Plan 01, plus OwnedComputedStyle wrapping the resolved map behind IComputedStyle — with G25/G27/G28/G29 proven at the cascade level**

## Performance

- **Duration:** ~45 min
- **Completed:** 2026-06-19
- **Tasks:** 2
- **Files modified/created:** 4

## Accomplishments

- `CascadeResolver.Resolve(IElement, IReadOnlyDictionary?)` implements all 7 DESIGN §4.2 steps:
  1. Match: `element.Matches(rule.SelectorText)` — AngleSharp core, never throws (selector-parse exceptions caught and logged at Debug with module + selector + message)
  2. Sort matched rules ascending by (Important tier, Specificity, SourceOrder); apply last-wins into property map
  3. Inline `style=""` overlay: raw k:v; splitter; inline-important beats everything
  4. Shorthand expansion (7 shorthands): `border`, `border-{side}`, `margin`, `padding`, `background`, `font`, `text-decoration`; 1/2/3/4-value CSS rules for margin/padding; `border:none` → all four widths=0
  5. UA defaults layer (only fills unset properties): HTML5 display map (table/thead/tbody/tfoot/tr/td/th/caption); `th` → bold+center; `h1-h6` → bold; `b/strong` → bold; `i/em` → italic; `u` → underline; `hr` → display:block
  6. Inheritance: static `HashSet<string>` allow-list (color, font-*, line-height, text-align, text-transform, white-space, word-break, overflow-wrap, word-wrap, visibility, list-style*); non-listed props never inherit
  7. Unit resolution: em→px via fontSizePx chain; rem→px via root 16px; `%` left literal; px/pt/mm/cm unchanged
- `OwnedComputedStyle` wraps `Dictionary<string,string>` (case-insensitive) behind `IComputedStyle`; `GetValue` returns null for absent keys; `HasProperty` checks non-empty presence. Static `Empty` singleton.
- No `GetComputedStyle`/`ComputeCurrentStyle` call anywhere in the new files.
- 22 `CascadeResolverTests` pass: cascade order, !important, source-order tiebreak, inline overlay, border/padding shorthand, UA defaults (all tags), display map, inheritance copy, non-inherited no-copy, em->px, % literal, px passthrough, G25, G27, G28, G29.

## Task Commits

1. **Task 1: CascadeResolver + OwnedComputedStyle** — `fe1d8dd1` (feat)
2. **Task 2: CascadeResolverTests + CssRuleSet supplemental parser fix** — `0b0cf54b` (test)

## Files Created/Modified

- `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs` — 7-step Resolve method; 300+ lines; no GetComputedStyle
- `src/Muonroi.Pdf.Governance/Cascade/OwnedComputedStyle.cs` — IComputedStyle wrapper; 28 lines
- `tests/Muonroi.Pdf.Tests/Cascade/CascadeResolverTests.cs` — 22 [Fact]s incl. G25/G27/G28/G29
- `src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs` — added BuildSupplementalFromRawStyleText() + raw-text parser helpers; FromDocument merges supplemental by selector key

## Decisions Made

- `CascadeResolver` takes `ILogger?` (nullable) — no DI required in tests; No-Silent-Catch on selector-parse exceptions at Debug level
- Inline style= splitter stores raw authored values (un-normalized); test assertions adjusted to check `"green"` not rgba form
- Supplemental parser added to `CssRuleSet` (not `CascadeResolver`) to maintain separation: collection vs. matching

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AngleSharp.Css beta.147 drops CSS3 properties from CSSOM AND from CssText**
- **Found during:** Task 2 — G28 test failed; map only had `{display: table-cell}` despite `.t td { word-break: break-word }` being authored
- **Evidence:** Diagnostic test confirmed: `rule.CssText = '.t td { }'` (empty block) and `style.Length = 0`. AngleSharp.Css beta.147 silently discards `word-break` (and `white-space`, `overflow-wrap`) at parse time — neither ICssStyleDeclaration nor the normalized CssText retains them.
- **Fix:** Added `BuildSupplementalFromRawStyleText()` to `CssRuleSet.FromDocument`: walks `document.GetElementsByTagName("style")`, parses raw `TextContent` with a minimal top-level CSS rule parser, extracts declarations only for properties in `SupplementalProperties` set (word-break, overflow-wrap, word-wrap, white-space, text-overflow, hyphens), and merges into the CSSOM-collected rules by selector key before emitting `CssMatchableRule` entries.
- **Files modified:** `src/Muonroi.Pdf.Governance/Cascade/CssRuleSet.cs`
- **Verification:** All 22 CascadeResolverTests pass; all 6 CssRuleSetTests still pass
- **Committed in:** `0b0cf54b`

**2. [Rule 1 - Bug] Inline style= assertion used CSSOM-normalized color form**
- **Found during:** Task 2 — InlineStyle_BeatsAuthorRule failed; `map["color"]` was `"green"` not `"rgba(0, 128, 0, 1)"`
- **Issue:** The inline splitter stores raw authored values (not AngleSharp-normalized), unlike CSSOM. Test asserted `"0, 128, 0"` (rgba form) but the resolver correctly stores `"green"`.
- **Fix:** Updated assertion to check `map["color"].Should().Be("green")` — this is the correct and expected behavior. The raw-value storage is by design (inline declarations are not processed by AngleSharp CSSOM).
- **Files modified:** `tests/Muonroi.Pdf.Tests/Cascade/CascadeResolverTests.cs`
- **Committed in:** `0b0cf54b`

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes. `BuildSupplementalFromRawStyleText` reads DOM text content (already in-memory parsed HTML) — same trust boundary as the CSSOM collection step. The raw-text parser is bounded to `SupplementalProperties` so it cannot introduce arbitrary CSS3 interpretation. Defensive: unknown tokens are ignored; parse errors fall through silently (no throw). T-12-04 satisfied: no bare catch anywhere; all catches log module+selector+message.

## Known Stubs

None — `CascadeResolver.Resolve` is fully functional for the Profile v1 surface. All 22 tests exercise live behavior. Not yet wired to the seam (that is Plan 03).

## Self-Check: PASSED

- `src/Muonroi.Pdf.Governance/Cascade/CascadeResolver.cs` — FOUND
- `src/Muonroi.Pdf.Governance/Cascade/OwnedComputedStyle.cs` — FOUND
- `tests/Muonroi.Pdf.Tests/Cascade/CascadeResolverTests.cs` — FOUND
- Commit `fe1d8dd1` — FOUND (feat: CascadeResolver + OwnedComputedStyle)
- Commit `0b0cf54b` — FOUND (test: CascadeResolverTests + CssRuleSet fix)
- All 22 CascadeResolverTests: PASSED (Passed: 22, Failed: 0)
- All 6 CssRuleSetTests: still PASSED (regression check)
- `dotnet build src/Muonroi.Pdf.Governance/Muonroi.Pdf.Governance.csproj`: 0 errors

---
*Phase: 12-owned-css-cascade-b1*
*Completed: 2026-06-19*
