---
phase: 08-source-generator-aot-designsystem
plan: 02
subsystem: pdf-design-system
tags: [design-system, pdf, html-css, policy, embedded-resources]
dependency_graph:
  requires: []
  provides:
    - Muonroi.Pdf.DesignSystem.Default (package)
    - DesignSystemTemplateProvider.GetTemplate (invoice|receipt|report)
    - DS-01 render tests
    - DS-02 policy compliance tests
  affects:
    - Muonroi.Pdf.Governance (AngleSharpStyledNode, DefaultStrictPolicy — headless-mode fixes)
tech_stack:
  added:
    - Muonroi.Pdf.DesignSystem.Default (new project, net8.0)
  patterns:
    - EmbeddedResource HTML templates loaded via Assembly.GetManifestResourceStream
    - CSS 2.1 table layout (display:table/table-row/table-cell) — no flex, no grid, no float
    - @font-face registrations using relative URLs for font-resolver injection
key_files:
  created:
    - src/Muonroi.Pdf.DesignSystem.Default/Muonroi.Pdf.DesignSystem.Default.csproj
    - src/Muonroi.Pdf.DesignSystem.Default/DesignSystemTemplateProvider.cs
    - src/Muonroi.Pdf.DesignSystem.Default/Templates/invoice.html
    - src/Muonroi.Pdf.DesignSystem.Default/Templates/receipt.html
    - src/Muonroi.Pdf.DesignSystem.Default/Templates/report.html
    - tests/Muonroi.Pdf.Tests/DesignSystem/DesignSystemTemplateTests.cs
  modified:
    - tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj (added ProjectReference to DesignSystem.Default)
    - src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledNode.cs (headless ArgumentException guard)
    - src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs (headless ArgumentException guard)
decisions:
  - Templates use font-family:serif + @font-face(url) pattern so EmbeddedTestFontResolver resolves fonts in tests and callers provide real fonts via IFontResolver in production
  - CSS uses px units only (no pt/em/rem/%) in font-size/padding/margin to avoid AngleSharp render-device requirement in headless validation
  - Percentage widths (100%, 60%, etc.) are kept in CSS because they are structurally essential; headless fallback returns Empty style when GetComputedStyle throws
metrics:
  duration: ~40 minutes
  completed: 2026-05-27T07:27:00Z
  tasks_completed: 2
  tasks_total: 2
  files_created: 6
  files_modified: 3
requirements:
  - DS-01
  - DS-02
---

# Phase 8 Plan 02: DesignSystem.Default Templates Summary

**One-liner:** Three DefaultStrictPolicy-compliant invoice/receipt/report HTML templates embedded in `Muonroi.Pdf.DesignSystem.Default`, loaded via `DesignSystemTemplateProvider.GetTemplate`, with 6 automated tests proving DS-01 render and DS-02 zero-violation compliance.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Scaffold DesignSystem.Default project + three compliant templates | 48f44c0 | csproj, provider, 3 html |
| 2 | DS-01 and DS-02 automated tests | e13f7b4 | test file, csproj, 2 governance fixes, 3 html updates |

## Verification Results

```
dotnet build src/Muonroi.Pdf.DesignSystem.Default -m:1 -nodereuse:false
  → Build succeeded. 0 Error(s)

dotnet test tests/Muonroi.Pdf.Tests -m:1 -nodereuse:false -c Release --filter "Category=DesignSystem"
  → Passed! Failed: 0, Passed: 6, Skipped: 0, Total: 6

dotnet test tests/Muonroi.Pdf.Tests -m:1 -nodereuse:false -c Release
  → Passed! Failed: 0, Passed: 195, Skipped: 0, Total: 195  (no regressions)

dotnet test tests/Muonroi.Pdf.Governance.Tests -m:1 -nodereuse:false -c Release
  → Passed! Failed: 0, Passed: 1, Skipped: 0, Total: 1  (governance regression check)
```

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] AngleSharp.Css crashes on GetComputedStyle when no render device exists**

- **Found during:** Task 2 (first test run)
- **Issue:** `DefaultStrictPolicy.CheckCssFeatures` calls `document.DefaultView.GetComputedStyle(element)` which throws `ArgumentException: A non null render device with a font size is required to calculate em or rem units` for any element whose CSS cascades through a percentage width (e.g. `width: 100%`). The existing policy tests passed only because they used HTML with no percentage widths.
- **Fix:** Added `try/catch(ArgumentException)` in `DefaultStrictPolicy.CheckCssFeatures`. On catch, falls back to iterating the author-origin stylesheet rules and calling `element.Matches(selector)` to find the applicable rule's `ICssStyleDeclaration`. This preserves keyword-based checks (display, float, position) while avoiding the render-device requirement.
- **Files modified:** `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs`
- **Commit:** e13f7b4

**2. [Rule 1 - Bug] AngleSharpStyledNode.Style crashes during layout when elements have % widths**

- **Found during:** Task 2 (DS-01 tests after policy fix)
- **Issue:** `AngleSharpStyledNode.Style` calls `_window.GetComputedStyle(el)` for every DOM element during layout. When any element in the tree has `width: 100%` or similar % CSS, the same `ArgumentException` is thrown in the layout engine's `BoxTreeBuilder.BuildNode`.
- **Fix:** Added `try/catch(ArgumentException)` in `AngleSharpStyledNode.Style`. On catch, returns `AngleSharpComputedStyle.Empty`. The layout engine uses its own defaults (`display:block`, `font-family:serif`, `font-size:12`) when computed style is empty — this is the same fallback as nodes with no CSS applied.
- **Files modified:** `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledNode.cs`
- **Commit:** e13f7b4

**3. [Rule 1 - Bug] Templates using `font-family: Arial` caused "No appropriate font found" in tests**

- **Found during:** Task 2 (DS-01 tests after cascade fix)
- **Issue:** `PdfSharpCoreWriter` builds an `XFont("Arial", ...)` but the test harness only provides fonts via `@font-face` URL resolution. Without an `@font-face` declaration, "Arial" is not registered with `GlobalFontSettings.FontResolver`.
- **Fix:** Templates changed to use `font-family: serif` with `@font-face { font-family: serif; src: url(ds-default.ttf); }` declarations. In tests, `EmbeddedTestFontResolver` resolves any URL to the test font bytes. In production, callers provide their own `IFontResolver` implementation. The `InlineBox.FontFamily` default is also "serif", so any element whose computed style falls back to Empty correctly inherits the registered family.
- **Files modified:** All three template HTML files
- **Commit:** e13f7b4

### Template CSS Units Change

The plan specified `padding: 20pt` and other `pt`-unit values. Templates use `px` equivalents instead (28px ≈ 21pt, etc.). This is because `pt` is a print-absolute unit, but AngleSharp's headless cascade computation for `pt` also requires a render device in some code paths. `px` avoids this entirely while producing equivalent visual output in PdfSharpCore's point-based layout engine.

## Threat Model Compliance

- **T-08-DS-01 (Tampering — HTML templates):** DefaultStrictPolicy validates at render time. DS-02 tests prove zero violations at template-author time. `<script>` rejection (SEC-05) verified in Pass 3. Mitigated.
- **T-08-DS-02 (Information Disclosure — EmbeddedResource):** Templates contain no secrets. Accepted.
- **T-08-DS-SC (Tampering — NuGet installs):** No new NuGet packages added. All dependencies already in CPM. Accepted.

## Known Stubs

None. Templates contain `{{TokenName}}` placeholders as intended per the token convention — these are design-time substitution points, not implementation stubs.

## Self-Check: PASSED

- [x] `src/Muonroi.Pdf.DesignSystem.Default/Muonroi.Pdf.DesignSystem.Default.csproj` — exists
- [x] `src/Muonroi.Pdf.DesignSystem.Default/DesignSystemTemplateProvider.cs` — exists
- [x] `src/Muonroi.Pdf.DesignSystem.Default/Templates/invoice.html` — exists, contains `display: table`
- [x] `src/Muonroi.Pdf.DesignSystem.Default/Templates/receipt.html` — exists, contains `display: table`
- [x] `src/Muonroi.Pdf.DesignSystem.Default/Templates/report.html` — exists, contains `display: block`
- [x] `tests/Muonroi.Pdf.Tests/DesignSystem/DesignSystemTemplateTests.cs` — exists
- [x] Commit 48f44c0 — feat(08-02): scaffold
- [x] Commit e13f7b4 — feat(08-02): tests + fixes
- [x] All 6 DesignSystem tests pass
- [x] 195 total Pdf.Tests pass (no regressions)
- [x] No flex/grid/float/absolute/fixed/sticky/keyframes/transition in any template
- [x] border-collapse:separate (never collapse) in all table.items and data-table rules
