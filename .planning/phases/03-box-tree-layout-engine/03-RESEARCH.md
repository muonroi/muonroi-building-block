# Phase 3: Box Tree + Layout Engine — Research

**Researched:** 2026-05-27
**Domain:** CSS 2.1 layout engine — hand-written box tree, BFC/IFC, table, pagination, page counters
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**D1 — Layout engine package**: `Muonroi.Pdf` only; layout walks DOM via `IStyledNode` traversal interface in Abstractions; no `InternalsVisibleTo`, no AngleSharp dependency in layout.

**D2 — IStyledNode traversal interface**: Add `IStyledNode`, `IComputedStyle`, `IPageRule` to `Muonroi.Pdf.Abstractions/Engine/`; extend `IStyledDocument` with `Root : IStyledNode` and `PageRule : IPageRule?`.

**D3 — @page parsing**: Parsed in Governance (`AngleSharpStyledDocument` at construction); exposed via `IStyledDocument.PageRule`; layout engine never touches AngleSharp stylesheet AST.

**D4 — Two-pass layout**: Full two-pass for `counter(pages)`. Pass 1 = `totalPages: 0`, pass 2 = `totalPages: pass1.PageCount`. Third pass NOT implemented (multi-digit boundary shift documented in KNOWN-DEVIATIONS.md).

**D5 — Box tree types**: All `internal sealed` in `Muonroi.Pdf/Internal/Layout/Boxes/`. `IPositionedPageList` stays marker interface; `PositionedPageList` is internal, cast in same-assembly Phase 5 writer.

**D6 — BFC + margin collapsing**: Per CSS 2.1 §8.3.1. BFC roots: root element, table cells, `overflow:hidden`, `inline-block`. Negative margins supported. Float clearance NOT in scope (float rejected by policy gate).

**D7 — Inline layout**: Simplified two-level (line box + inline/inline-block). Baseline = max ascender in line. `vertical-align: top/middle/bottom` supported. Unicode TR#14 UAX#14 line-break algorithm (default). Phase 3 uses `EstimatedTextMetrics` (monospace approximation); Phase 4 replaces with `SixLaborsTextMetrics`.

**D8 — Table layout**: Fixed-width (`table-layout: fixed`) and auto-width (`table-layout: auto`) per CSS 2.1 §17.5.2. `colspan` via spanning algorithm. `rowspan` via two-pass. `border-collapse: separate` + `border-spacing` honored. Each table cell = independent BFC.

**D9 — Pagination**: `page-break-before/after: always` forced; `page-break-inside: avoid` best-effort (break anyway if element > available height). Orphans/widows NOT implemented. `MaxPages` enforced after pass 1 (throws `PdfInputLimitException` before pass 2).

**D10 — Muonroi.Pdf.csproj**: Create targeting `net8.0`; reference `Muonroi.Pdf.Abstractions` only. Add to `Muonroi.BuildingBlock.sln`. Governance reference deferred to Phase 6.

**D11 — ITextMetrics seam**: `internal interface ITextMetrics` in `Muonroi.Pdf/Internal/Layout/`. Phase 3 ships `EstimatedTextMetrics` (fontSize * 0.6 per char, fontSize * 1.2 line height). Phase 4 replaces; layout engine constructor receives `ITextMetrics`.

### Deferred Ideas (OUT OF SCOPE for Phase 3)
- Real font metrics — Phase 4 (`SixLaborsTextMetrics`)
- Image decoding — Phase 4
- `@font-face` resolution — Phase 4
- PDF file writing — Phase 5
- `AddPdf()` DI registration — Phase 6
- Orphans/widows — KNOWN-DEVIATIONS.md
- CSS `@page { size: ... }` override — KNOWN-DEVIATIONS.md
- Two-pass third iteration — KNOWN-DEVIATIONS.md
- `counter(page)` inside header/footer content (nested counter) — KNOWN-DEVIATIONS.md
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PIPE-05 | Hand-written box tree from styled DOM; no HtmlRenderer.PdfSharp, no GDI+ | D5: box type hierarchy; `BoxTreeBuilder` walks `IStyledNode` |
| PIPE-06 | Layout engine produces `IPositionedPageList` before `IPdfWriter` is called | D4 two-pass `LayoutEngine.Layout()` returns `PositionedPageList : IPositionedPageList` |
| LAYOUT-01 | BFC + margin collapsing per CSS 2.1 §8.3.1 | D6: BFC roots defined; collapsing rules enumerated |
| LAYOUT-02 | IFC with white-space, line-break, vertical-align | D7: simplified two-level line boxes; UAX#14 |
| LAYOUT-03 | Baseline alignment for mixed inline content | D7: max ascender baseline; `ITextMetrics.GetAscender()` |
| LAYOUT-04 | `display:table` column/row sizing | D8: fixed + auto per CSS 2.1 §17.5.2 |
| LAYOUT-05 | `colspan` + `rowspan` respected | D8: spanning algorithm; two-pass rowspan |
| LAYOUT-06 | `border-collapse:separate` + `border-spacing` | D8: separate enforced; PolicyViolation for collapse already emitted by Phase 2 |
| LAYOUT-07 | `border-collapse:collapse` → PolicyViolation | Already handled by `DefaultStrictPolicy` in Phase 2; Phase 3 need not re-check |
| PAGE-01 | `@page` margin boxes applied | D3+D9: `IPageRule.Margins` applied per-page |
| PAGE-02 | A4/A5/Letter/Legal portrait+landscape | `PdfPageSize` enum already in Abstractions; `PdfPageSizeDimensions` helper needed in Phase 3 |
| PAGE-03 | page-break-before/after/inside | D9: forced breaks; avoid best-effort |
| PAGE-04 | Repeated header from `@page` top margin box | D9: header laid out as mini-doc per page |
| PAGE-05 | Repeated footer from `@page` bottom margin box | D9: footer as mini-doc per page |
| PAGE-06 | `counter(page)` = current 1-based page number | D4: resolved during layout pass |
| PAGE-07 | `counter(pages)` = total page count | D4: two-pass; `totalPages` parameter |
| PAGE-08 | `MaxPages` enforcement | D9: throw `PdfInputLimitException` if pass-1 count > `PdfConfigs.Limits.MaxPages` |
</phase_requirements>

---

## Summary

Phase 3 implements a hand-written CSS 2.1 layout engine entirely in `Muonroi.Pdf` (targeting net8.0), consuming the styled DOM via the `IStyledNode` traversal interface added to `Muonroi.Pdf.Abstractions`. No third-party layout library is used. The engine covers block formatting contexts (BFC), inline formatting contexts (IFC), table layout, pagination with forced/avoided page breaks, header/footer repetition, and CSS counters (`counter(page)` and `counter(pages)`). All 11 architectural decisions are locked in CONTEXT.md.

The phase introduces zero new NuGet packages. Existing CPM-pinned packages cover all needs: `xunit 2.9.2`, `FluentAssertions 7.2.0`, `NSubstitute 5.3.0` for the new `Muonroi.Pdf.Tests` project. The layout engine operates exclusively on `IStyledNode`/`IComputedStyle`/`IPageRule` interfaces — AngleSharp types never appear in `Muonroi.Pdf` code.

The `ITextMetrics` seam decouples Phase 3 (estimated metrics) from Phase 4 (SixLabors.Fonts real metrics). `EstimatedTextMetrics` uses `fontSize * 0.6` per character (monospace approximation) and `fontSize * 1.2` line height — sufficient for pagination logic correctness at Phase 3 scope. Two-pass layout is the authoritative design for `counter(pages)`: pass 1 runs with `totalPages = 0`, pass 2 uses `pass1.PageCount`. The planner must budget for running the layout engine twice per render call.

**Primary recommendation:** Implement in strict dependency order — Abstractions gap (3a) → Governance gap (3b) → `Muonroi.Pdf.csproj` (3c) → box tree (3d) → layout engine (3e) → verification (3f). Each wave must compile before the next begins.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| DOM traversal interface | `Muonroi.Pdf.Abstractions` | — | Stable seam; enables v0.2 SG/AOT without AngleSharp |
| @page rule parsing | `Muonroi.Pdf.Governance` | — | Cascade phase has AngleSharp; layout must not |
| Box tree construction | `Muonroi.Pdf` (internal) | — | Implementation detail; no external consumer |
| Block/inline layout | `Muonroi.Pdf` (internal) | — | Layout engine owns all formatting context logic |
| Table layout | `Muonroi.Pdf` (internal) | — | CSS 2.1 §17.5.2 algorithm; internal only |
| Pagination + counters | `Muonroi.Pdf` (internal) | — | Interacts with layout context state |
| Positioned page list | `Muonroi.Pdf.Abstractions` (marker seam) | `Muonroi.Pdf` (concrete impl) | Marker in Abstractions; impl internal to Pdf |
| Font metrics | `Muonroi.Pdf` (ITextMetrics seam) | Phase 4 replaces impl | Seam in Pdf; estimate now, real in Phase 4 |
| Unit conversion (mm→pt) | `Muonroi.Pdf` (internal) | — | Internal geometry concern |

---

## Standard Stack

### Core (no new packages — all pre-existing in Directory.Packages.props)

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| `Muonroi.Pdf.Abstractions` | local | `IStyledNode`, `IStyledDocument`, `IPositionedPageList` interfaces | Already in CPM |
| `AngleSharp` | 1.3.0 | Consumed indirectly via `IStyledNode` wrapper in Governance | Already in CPM [VERIFIED: Directory.Packages.props] |
| `AngleSharp.Css` | 1.0.0-beta.147 | Cascade engine in Governance; layout never sees this | Already in CPM [VERIFIED: Directory.Packages.props] |

### Test Stack

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| `xunit` | 2.9.2 | Test framework | Already in CPM [VERIFIED: Directory.Packages.props] |
| `FluentAssertions` | 7.2.0 | Assertion fluent API (Apache 2.0, PINNED per D4 note) | Already in CPM [VERIFIED: Directory.Packages.props] |
| `NSubstitute` | 5.3.0 | Mock `IStyledNode`, `IComputedStyle`, `ITextMetrics` | Already in CPM [VERIFIED: Directory.Packages.props] |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | Test runner host | Already in CPM [VERIFIED: Directory.Packages.props] |
| `xunit.runner.visualstudio` | 2.8.2 | IDE test discovery | Already in CPM [VERIFIED: Directory.Packages.props] |
| `coverlet.collector` | 6.0.2 | Coverage | Already in CPM [VERIFIED: Directory.Packages.props] |

**Installation:** No new packages. Test project `tests/Muonroi.Pdf.Tests/` must be created referencing `Muonroi.Pdf` and the CPM test packages.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-written layout engine | HtmlRenderer.PdfSharp | HtmlRenderer.PdfSharp is archived; GDI+ dependency; blocked by project rules (PIPE-05) |
| Hand-written layout engine | ExCSS + custom engine | ExCSS handles parsing not layout; still need to implement CSS 2.1 layout algorithms |
| EstimatedTextMetrics | Platform text services | Platform-dependent; violates OS-neutral constraint; Phase 4 adds SixLabors.Fonts |

---

## Package Legitimacy Audit

No new packages are installed in Phase 3. All packages consumed exist in `Directory.Packages.props` as CPM-pinned entries. No slopcheck audit required.

| Package | Registry | Age | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|
| xunit 2.9.2 | NuGet | 10+ yrs | N/A (pre-existing) | Approved |
| FluentAssertions 7.2.0 | NuGet | 10+ yrs | N/A (pre-existing) | Approved |
| NSubstitute 5.3.0 | NuGet | 10+ yrs | N/A (pre-existing) | Approved |

**Packages removed due to [SLOP]:** none
**Packages flagged as [SUS]:** none

---

## Architecture Patterns

### System Architecture Diagram

```
IStyledDocument (Abstractions)
  │  .Root : IStyledNode
  │  .PageRule : IPageRule?
  │
  ▼
BoxTreeBuilder (Muonroi.Pdf internal)
  │  Walks IStyledNode tree depth-first
  │  Generates anonymous boxes per CSS 2.1 §9.2.1
  │
  ▼
BoxNode hierarchy (internal)
  ├── BlockBox → BlockLayoutEngine
  ├── InlineBox → InlineLayoutEngine (line boxes)
  ├── TableBox → TableLayoutEngine (column/row sizing)
  └── ReplacedBox → placeholder (Phase 4 fills actual decode)
  │
  ▼
LayoutEngine.Layout(doc, options) — two-pass entry point
  │  Pass 1: RunLayout(doc, options, totalPages: 0)
  │    │  LayoutContext (available width, y-cursor, page index, totalPages)
  │    │  BlockLayoutEngine → InlineLayoutEngine → TableLayoutEngine
  │    │  PaginationEngine (page breaks, header/footer per page)
  │    └─→ PositionedPageList (pass 1 result)
  │  Pass 2: RunLayout(doc, options, totalPages: pass1.PageCount)
  │    └─→ PositionedPageList (authoritative result)
  │
  ▼
IPositionedPageList (Abstractions marker)
  │  Concrete: PositionedPageList (internal to Muonroi.Pdf)
  │  Contains: IReadOnlyList<PositionedPage>, int PageCount
  │
  ▼
IPdfWriter (Phase 5) — casts IPositionedPageList to PositionedPageList internally
```

### Recommended Project Structure

```
src/Muonroi.Pdf/
├── Muonroi.Pdf.csproj          — net8.0, Abstractions ref only
├── Internal/
│   ├── GlobalUsings.cs
│   └── Layout/
│       ├── ITextMetrics.cs
│       ├── EstimatedTextMetrics.cs
│       ├── LayoutContext.cs
│       ├── LayoutEngine.cs        — two-pass entry point
│       ├── Geometry/
│       │   ├── Units.cs           — mm→pt, px→pt conversions
│       │   └── Rect.cs
│       ├── Boxes/
│       │   ├── BoxNode.cs         — abstract base
│       │   ├── BlockBox.cs
│       │   ├── InlineBox.cs
│       │   ├── AnonymousBox.cs
│       │   ├── TableBox.cs
│       │   ├── TableRowGroupBox.cs
│       │   ├── TableRowBox.cs
│       │   ├── TableCellBox.cs
│       │   └── ReplacedBox.cs
│       ├── BoxTreeBuilder.cs      — IStyledNode → BoxNode tree
│       ├── BlockLayoutEngine.cs   — BFC, margin collapsing
│       ├── InlineLayoutEngine.cs  — IFC, line boxes, baseline
│       ├── TableLayoutEngine.cs   — column sizing, span
│       ├── PaginationEngine.cs    — breaks, header/footer, counters
│       ├── PositionedElement.cs
│       ├── PositionedPage.cs
│       └── PositionedPageList.cs  — implements IPositionedPageList
└── Extensions/
    └── (empty in Phase 3 — DI registration in Phase 6)

src/Muonroi.Pdf.Abstractions/Engine/   — additions in Phase 3a
├── IStyledNode.cs      (new)
├── IComputedStyle.cs   (new)
├── IPageRule.cs        (new)
└── IStyledDocument.cs  (extended: + Root, + PageRule)

src/Muonroi.Pdf.Governance/Cascade/   — additions in Phase 3b
├── AngleSharpComputedStyle.cs  (new)
├── AngleSharpStyledNode.cs     (new)
├── AngleSharpPageRule.cs       (new)
└── AngleSharpStyledDocument.cs (extended: + Root, + PageRule props)

tests/Muonroi.Pdf.Tests/   — new project (Wave 0)
├── Muonroi.Pdf.Tests.csproj
├── GlobalUsings.cs
├── Layout/
│   ├── BoxTreeBuilderTests.cs
│   ├── BlockLayoutTests.cs      — SC1: margin collapsing
│   ├── InlineLayoutTests.cs     — SC2: baseline, vertical-align
│   ├── TableLayoutTests.cs      — SC3: colspan/rowspan
│   └── PaginationTests.cs       — SC4/SC5: page-break, counter(pages)
└── Helpers/
    └── FakeStyledNode.cs        — IStyledNode stub for tests
```

### Pattern 1: IStyledNode Traversal Interface (Abstractions seam)

**What:** Layout engine walks the DOM via interfaces only — AngleSharp types never cross into `Muonroi.Pdf`.
**When to use:** Every place layout code needs to inspect an element's tag, style, children, or attributes.

```csharp
// Source: CONTEXT.md Decision 2 (locked)
public interface IStyledNode
{
    string LocalName { get; }
    string? TextContent { get; }
    IComputedStyle Style { get; }
    IReadOnlyList<IStyledNode> Children { get; }
    string? GetAttribute(string name);
    bool IsElement { get; }
    bool IsText { get; }
}

public interface IComputedStyle
{
    string? GetValue(string property);
    bool HasProperty(string property);
}

public interface IPageRule
{
    PdfMargins Margins { get; }
    string? TopMarginBoxHtml { get; }
    string? BottomMarginBoxHtml { get; }
    string? Size { get; }
}
```

### Pattern 2: Two-Pass Layout Entry Point

**What:** Run layout twice to resolve `counter(pages)`.
**When to use:** Only in `LayoutEngine.Layout()` — the single entry point.

```csharp
// Source: CONTEXT.md Decision 4 (locked)
internal sealed class LayoutEngine
{
    public IPositionedPageList Layout(IStyledDocument doc, PdfRenderOptions options,
        PdfConfigs.Limits limits)
    {
        var pass1 = RunLayout(doc, options, limits, totalPages: 0);
        int pageCount = pass1.PageCount;
        if (pageCount > limits.MaxPages)
            throw new PdfInputLimitException(
                $"Page count {pageCount} exceeds MaxPages {limits.MaxPages}.");
        return RunLayout(doc, options, limits, totalPages: pageCount);
    }

    private PositionedPageList RunLayout(IStyledDocument doc, PdfRenderOptions options,
        PdfConfigs.Limits limits, int totalPages) { ... }
}
```

### Pattern 3: BFC Margin Collapsing (CSS 2.1 §8.3.1)

**What:** Adjacent block margins collapse to the maximum; BFC roots prevent cross-boundary collapsing.
**When to use:** `BlockLayoutEngine` after positioning each block child.

```csharp
// Source: CSS 2.1 §8.3.1 [ASSUMED - standard CSS spec algorithm]
// Three collapsing cases:
// 1. Adjacent siblings: max(marginBottom_A, marginTop_B)
// 2. Parent-child: if no border/padding between parent top and first child top,
//    parent.MarginTop collapses with firstChild.MarginTop
// 3. Empty block: if block has no border/padding/height, its own top and bottom margins collapse

float CollapseMargins(float a, float b) => MathF.Max(a, b); // positives only
// Negative: max(positives) - abs(min(negatives))
float CollapseMarginsWithNegative(float a, float b)
{
    float positive = MathF.Max(MathF.Max(a, b), 0f);
    float negative = MathF.Min(MathF.Min(a, b), 0f);
    return positive + negative; // negative is already negative
}
```

### Pattern 4: Table Column Width (CSS 2.1 §17.5.2)

**What:** Auto-width table: preferred minimum widths per column, distributed to fill table width.
**When to use:** `TableLayoutEngine` before laying out rows.

```csharp
// Source: CSS 2.1 §17.5.2 [ASSUMED - standard CSS spec algorithm]
// Step 1: For each column, min-width = max(all cells in column, min-content width)
// Step 2: preferred-width = max(all cells in column, preferred-content width)
// Step 3: Distribute available table width proportionally
// Spanning cells: contribute after non-spanning columns are sized
float[] ComputeAutoColumnWidths(TableBox table, float availableWidth,
    ITextMetrics metrics) { ... }
```

### Pattern 5: ITextMetrics Seam

**What:** Injectable text measurement — `EstimatedTextMetrics` in Phase 3, `SixLaborsTextMetrics` in Phase 4.
**When to use:** Every place line-breaking or baseline computation needs character/font metrics.

```csharp
// Source: CONTEXT.md Decision 11 (locked)
internal interface ITextMetrics
{
    float GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic);
    float GetLineHeight(string fontFamily, float fontSize);
    float GetAscender(string fontFamily, float fontSize);
    float GetDescender(string fontFamily, float fontSize);
}

// Phase 3 implementation
internal sealed class EstimatedTextMetrics : ITextMetrics
{
    public float GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic)
        => fontSize * 0.6f;
    public float GetLineHeight(string fontFamily, float fontSize)
        => fontSize * 1.2f;
    public float GetAscender(string fontFamily, float fontSize)
        => fontSize * 0.8f;
    public float GetDescender(string fontFamily, float fontSize)
        => fontSize * 0.2f;
}
```

### Pattern 6: Unit Conversion

**What:** All internal layout coordinates use points (pt). CSS input uses px, mm, cm, em, %.
**When to use:** `Units.cs` — called at box-tree construction time to resolve all CSS lengths.

```csharp
// 1 inch = 72 pt; 1 mm = 72/25.4 pt ≈ 2.834646 pt
// CSS px = 1/96 inch = 72/96 pt = 0.75 pt
internal static class Units
{
    public const float MmToPt = 2.834646f;
    public const float PxToPt = 0.75f;
    public const float CmToPt = 28.34646f;
    public const float InToPt = 72f;
}
```

### Anti-Patterns to Avoid

- **Casting `IStyledNode` to `AngleSharpStyledNode` in `Muonroi.Pdf`:** This would break the seam. Layout code must only use `IStyledNode` interface members.
- **Reading AngleSharp `IDocument` directly in layout code:** Never. All DOM access through `IStyledNode`.
- **Implementing margin collapsing as a simple sum:** It is a max (for positives) or max-positive + min-negative rule, not a sum.
- **Forgetting anonymous box generation (CSS 2.1 §9.2.1):** When a block-level box has both block-level and inline-level children, anonymous block boxes must wrap the inline children. Missing this causes incorrect BFC/IFC switching.
- **Running only one layout pass:** `counter(pages)` requires two passes. The Phase 5 writer must receive the pass-2 result.
- **Using `double` for layout arithmetic:** Use `float` throughout. PDF coordinates are 32-bit floats. Mixing `double` adds unnecessary allocation and conversion noise.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CSS length parsing (`12px`, `1.5em`, `10mm`) | Custom string parser | `IComputedStyle.GetValue(property)` returns pre-resolved string from AngleSharp cascade | AngleSharp.Css has already computed and resolved values; parse is done |
| Unicode line-break opportunities | Custom Unicode TR#14 impl | `System.Globalization.StringInfo` + `char.GetUnicodeCategory()` for basic cases; AngleSharp cascade resolves `word-break`/`overflow-wrap` | Full UAX#14 is 50+ pages; basic word/character boundaries sufficient for Phase 3 scope |
| CSS property name normalization | String manipulation | Always use lowercase property names with `IComputedStyle.GetValue("margin-top")` | AngleSharp normalizes to lowercase at cascade time |
| Page size dimensions | Custom lookup table | `PdfPageSizeDimensions` helper class in `Geometry/` — verified from ISO 216 | A4 = 210×297 mm, A5 = 148×210 mm, Letter = 216×279 mm, Legal = 216×356 mm |

**Key insight:** AngleSharp.Css has already parsed, resolved, and cascaded all CSS values. `IComputedStyle.GetValue("display")` returns `"block"`, `"table"`, etc. — not raw CSS text. The layout engine's job is geometry, not CSS parsing.

---

## Common Pitfalls

### Pitfall 1: Parent-Child Margin Collapsing Missed
**What goes wrong:** Adjacent sibling collapsing is implemented but parent-child collapsing is forgotten. A `<div>` with no padding/border and a first `<p>` child: the div's `margin-top` collapses with the `<p>`'s `margin-top`. Results in double spacing above the first paragraph.
**Why it happens:** CSS 2.1 §8.3.1 describes three collapsing cases; most implementations only handle case 1 (adjacent siblings).
**How to avoid:** `BlockLayoutEngine` must check whether the parent's top margin collapses with first child's top margin (no border, no padding, no clearance between them). Implement all three cases in a single `MarginsCollapse()` helper.
**Warning signs:** Unit test: `<div style="margin-top:20px"><p style="margin-top:30px">` should produce 30 px of top spacing (not 50 px).

### Pitfall 2: Anonymous Box Generation Omitted
**What goes wrong:** A block-level element contains a mix of block children and text nodes. Text nodes produce inline boxes that sit directly inside the block box — no IFC is established — and text renders at incorrect positions.
**Why it happens:** CSS 2.1 §9.2.1 ("Anonymous block boxes") is easy to overlook when reading the spec linearly.
**How to avoid:** In `BoxTreeBuilder`, when building children of a block box: if any child is block-level, wrap all adjacent inline-level siblings in `AnonymousBox`. Run this normalization pass before returning from `BuildChildren()`.
**Warning signs:** A `<div>` containing `text <p>block</p> more text` renders incorrectly.

### Pitfall 3: Table `colspan` Spanning Algorithm Off-by-One
**What goes wrong:** A `colspan=2` cell contributes min-width to only the first column, leaving the second column undersized. Output table has collapsed second column.
**Why it happens:** CSS 2.1 §17.5.2.2 requires a two-step spanning distribution: first size non-spanning columns, then distribute spanning cell widths across spanned columns with proportional distribution.
**How to avoid:** Process non-spanning cells first, then spanning cells in ascending `colspan` order. For each spanning cell: if its required width exceeds the sum of spanned column widths, distribute the excess proportionally.
**Warning signs:** SC3 test: `<table><tr><td colspan="2">A</td><td>B</td></tr><tr><td>X</td><td>Y</td><td>Z</td></tr>` — column widths must be consistent.

### Pitfall 4: `page-break-inside: avoid` Infinite Loop
**What goes wrong:** An element's `page-break-inside: avoid` is implemented as "keep retrying placement". If the element is taller than the page's available body height, the engine loops forever.
**Why it happens:** The avoid semantics are "best-effort, not mandatory" (CSS 2.1 §13.3.1).
**How to avoid:** In `PaginationEngine`, if an element with `page-break-inside: avoid` does not fit on the current page, force a page break before it and place it at the top of the next page. If it still does not fit (height > available page height), break it anyway. Document this edge case in `KNOWN-DEVIATIONS.md`.
**Warning signs:** Test: element 2000 pt tall on an A4 page (body height ~770 pt) must not cause infinite loop.

### Pitfall 5: Two-Pass Counter Boundary Shift
**What goes wrong:** Pass 1 produces 9 pages (counter shows "1 of 0"). Pass 2 substitutes totalPages=9, and the footer "Page 1 of 9" is wider than "Page 1 of 0", shifting a paragraph across a page boundary, making pass 2 produce 10 pages instead of 9. The result shows "Page N of 9" but has 10 pages.
**Why it happens:** Expected. The two-pass approach is not guaranteed convergent when counter rendering affects pagination.
**How to avoid:** This is documented as a known deviation per CONTEXT.md Decision 4. The pass-2 result is always authoritative. A third pass is NOT implemented. Document in `KNOWN-DEVIATIONS.md`.
**Warning signs:** Only occurs when a multi-digit total page count (10+) changes the rendered footer width enough to shift a paragraph. Rare in practice.

### Pitfall 6: `display:none` Nodes Generating Boxes
**What goes wrong:** `BoxTreeBuilder` generates a box for an element with `display:none`, producing ghost boxes that consume layout space.
**Why it happens:** `display:none` means the element is not rendered and generates no box.
**How to avoid:** In `BoxTreeBuilder.BuildNode()`, check `node.Style.GetValue("display") == "none"` early and return null. Same for `visibility:hidden` (element generates a box but is invisible — do generate the box, just mark it invisible for Phase 5).
**Warning signs:** A `<div style="display:none">text</div>` contributes zero height to parent.

### Pitfall 7: Percent Width Resolution Without Containing Block
**What goes wrong:** A child element with `width: 50%` resolves to 0 or throws because the containing block width is not known at box-tree construction time.
**Why it happens:** CSS percent widths resolve against the containing block, which is only known during layout, not during box-tree building.
**How to avoid:** Percent widths must be resolved in `BlockLayoutEngine` during the layout pass, not in `BoxTreeBuilder`. `BoxNode` stores the raw CSS value string; the layout engine resolves it against `LayoutContext.AvailableWidth` at layout time.
**Warning signs:** `<div style="width:50%">` in a 400 pt container should yield a 200 pt box.

---

## Code Examples

### AngleSharpStyledNode wrapping IElement (Governance implementation)

```csharp
// Source: CONTEXT.md Decision 2 (locked design)
// In Muonroi.Pdf.Governance/Cascade/AngleSharpStyledNode.cs
internal sealed class AngleSharpStyledNode : IStyledNode
{
    private readonly INode _node;
    private readonly IWindow? _window;

    internal AngleSharpStyledNode(INode node, IWindow? window)
    {
        _node = node;
        _window = window;
    }

    public string LocalName => (_node as IElement)?.LocalName ?? "#text";
    public string? TextContent => _node.TextContent;
    public bool IsElement => _node is IElement;
    public bool IsText => _node.NodeType == NodeType.Text;
    public string? GetAttribute(string name) => (_node as IElement)?.GetAttribute(name);

    public IComputedStyle Style
    {
        get
        {
            if (_node is IElement element && _window != null)
            {
                var decl = _window.ComputedStyle(element);
                return new AngleSharpComputedStyle(decl);
            }
            return AngleSharpComputedStyle.Empty;
        }
    }

    public IReadOnlyList<IStyledNode> Children
    {
        get
        {
            var result = new List<IStyledNode>(_node.ChildNodes.Length);
            foreach (INode child in _node.ChildNodes)
            {
                if (child is IElement || child.NodeType == NodeType.Text)
                    result.Add(new AngleSharpStyledNode(child, _window));
            }
            return result;
        }
    }
}
```

### Page size dimensions helper

```csharp
// Source: ISO 216 standard [VERIFIED: well-known standard]
// In Muonroi.Pdf/Internal/Layout/Geometry/PdfPageSizeDimensions.cs
internal static class PdfPageSizeDimensions
{
    // Returns (widthPt, heightPt) for portrait; swap for landscape
    public static (float Width, float Height) Get(PdfPageSize size)
        => size switch
        {
            PdfPageSize.A3     => (841f * Units.MmToPt / 10f, 1189f * Units.MmToPt / 10f), // 297×420mm
            PdfPageSize.A4     => (595.28f, 841.89f),   // 210×297mm
            PdfPageSize.A5     => (419.53f, 595.28f),   // 148×210mm
            PdfPageSize.Letter => (612f, 792f),           // 8.5×11in
            PdfPageSize.Legal  => (612f, 1008f),          // 8.5×14in
            _                  => (595.28f, 841.89f)
        };
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| HtmlRenderer.PdfSharp (GDI+, archived) | Hand-written pure-managed CSS 2.1 layout | Phase 3 decision (D1) | OS-neutral; no GDI+; no archived fork maintenance |
| wkhtmltopdf / DinkToPdf | Native-free pure managed | Phase 1 architectural decision | Runs on Alpine/AOT; no CVE treadmill |
| Runtime reflection for box dispatch | Virtual method dispatch on sealed BoxNode hierarchy | Phase 3 design | AOT-safe; devirtualizable by JIT/AOT |

**Deprecated/outdated:**
- HtmlRenderer.PdfSharp: archived at 1.6.x, no maintenance, GDI+ dependency — explicitly excluded by PIPE-05
- GDI+ on Linux: `libgdiplus` required separately, broken on Alpine — excluded by OS-neutral constraint

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `IWindow.ComputedStyle(element)` returns resolved values for all CSS properties set via stylesheets (not just inline) | Code Examples — AngleSharpStyledNode | If computed style only returns inline styles, cascade output would be lost; mitigation: verify in Phase 3b with integration test |
| A2 | `EstimatedTextMetrics` `fontSize * 0.6f` per-char approximation is close enough that pagination tests pass (no off-by-one page breaks in unit tests) | Pattern 5 — ITextMetrics Seam | If estimation causes SC4/SC5 tests to flicker, adjust the multipliers; Phase 4 replaces entirely |
| A3 | CSS `display` property value returned by `IComputedStyle.GetValue("display")` is normalized to lowercase by AngleSharp | Pitfall 6 | If mixed-case values are returned, box type discrimination breaks; mitigation: use `StringComparison.OrdinalIgnoreCase` throughout |
| A4 | A4 standard point dimensions (595.28 × 841.89 pt) match PdfSharpCore's internal A4 page size | Code Examples — PdfPageSizeDimensions | If Phase 5 uses different A4 dimensions, positioned elements will be misaligned on the page; mitigation: verify against PdfSharpCore source in Phase 5 |

**If this table is empty:** N/A — four assumptions identified above.

---

## Open Questions

1. **Does `IWindow.ComputedStyle` work correctly after `AngleSharpCascadeEngine.CascadeAsync`?**
   - What we know: `AngleSharpCascadeEngine` creates a `BrowsingContext` with CSS enabled and parses/cascades the document. `IWindow` should be available via `document.DefaultView`.
   - What's unclear: Whether the `IWindow` reference on `AngleSharpStyledDocument` is valid after the async cascade completes, or if the context is disposed.
   - Recommendation: Phase 3b must add an integration test in `Muonroi.Pdf.Governance.Tests` that parses a styled document and reads a computed style value via `IWindow.ComputedStyle` to confirm the context stays alive.

2. **What value does `IComputedStyle.GetValue("border-spacing")` return — pixels or raw CSS string?**
   - What we know: AngleSharp.Css resolves most values to canonical form (e.g., `"10px"` not `"10"`).
   - What's unclear: Whether `border-spacing` returns the value already converted to a canonical unit, or as `"10px 5px"` (two-value shorthand).
   - Recommendation: `AngleSharpComputedStyle` tests in Phase 3b should verify the format returned for `border-spacing`, `margin`, and `padding` shorthand properties.

3. **Does `AngleSharp 1.3.0` expose `IDocument.StyleSheets` as `IList<IStyleSheet>` with access to `@page` rules?**
   - What we know: `AngleSharp.Css` adds CSS parsing support. `@page` rules are at-rules in CSS.
   - What's unclear: Whether `@page` rules are accessible as typed objects via the AngleSharp.Css API, or require raw CSS text parsing.
   - Recommendation: `AngleSharpPageRule` implementation in Phase 3b must explicitly verify `@page` rule access pattern. If typed access is unavailable, fall back to regex on `sheet.OwnerNode.TextContent`.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | `dotnet build`, `dotnet test` | ✓ | Verified from `net8.0` target in existing projects | — |
| `dotnet test` runner | Validation Architecture | ✓ | Built into .NET SDK | — |
| NuGet CPM packages | All packages | ✓ | All pre-pinned in Directory.Packages.props | — |

**Missing dependencies with no fallback:** none
**Missing dependencies with fallback:** none

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 |
| Config file | none (standard xunit auto-discovery) |
| Quick run command | `dotnet test tests/Muonroi.Pdf.Tests/ --no-restore -v m` |
| Full suite command | `dotnet test Muonroi.BuildingBlock.sln` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PIPE-05 | BoxTreeBuilder produces non-null root from IStyledNode stub | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~BoxTreeBuilder" -v m` | ❌ Wave 0 |
| PIPE-06 | LayoutEngine.Layout returns IPositionedPageList with PageCount > 0 | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~LayoutEngine" -v m` | ❌ Wave 0 |
| LAYOUT-01 | Adjacent block margins collapse to max; BFC root prevents cross-boundary collapse | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~BlockLayout" -v m` | ❌ Wave 0 |
| LAYOUT-02 | Vertical-align top/middle/bottom places inline boxes at correct y-offset | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~InlineLayout" -v m` | ❌ Wave 0 |
| LAYOUT-03 | Mixed inline box baseline = max ascender in line | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~InlineLayout" -v m` | ❌ Wave 0 |
| LAYOUT-04 | Table with 3 columns produces correct column widths summing to table width | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~TableLayout" -v m` | ❌ Wave 0 |
| LAYOUT-05 | colspan=2 cell spans two column widths; rowspan=2 cell spans two row heights | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~TableLayout" -v m` | ❌ Wave 0 |
| LAYOUT-06 | border-spacing applied between table cells | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~TableLayout" -v m` | ❌ Wave 0 |
| PAGE-01 | @page margins reduce body rect by margin values | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~Pagination" -v m` | ❌ Wave 0 |
| PAGE-02 | A4 portrait page = 595.28 × 841.89 pt; landscape swapped | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~PdfPageSize" -v m` | ❌ Wave 0 |
| PAGE-03 | page-break-before:always forces new page before element | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~Pagination" -v m` | ❌ Wave 0 |
| PAGE-04 | Header appears on every page at top margin box position | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~Pagination" -v m` | ❌ Wave 0 |
| PAGE-05 | Footer appears on every page at bottom margin box position | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~Pagination" -v m` | ❌ Wave 0 |
| PAGE-06 | counter(page) in content resolves to page index (1-based) | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~Counter" -v m` | ❌ Wave 0 |
| PAGE-07 | counter(pages) resolves to total page count after two-pass | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~Counter" -v m` | ❌ Wave 0 |
| PAGE-08 | MaxPages exceeded → PdfInputLimitException thrown after pass 1 | unit | `dotnet test tests/Muonroi.Pdf.Tests/ --filter "FullyQualifiedName~Pagination" -v m` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `dotnet test tests/Muonroi.Pdf.Tests/ --no-restore -v m`
- **Per wave merge:** `dotnet test Muonroi.BuildingBlock.sln`

### Wave 0 Gaps
- [ ] `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` — new test project
- [ ] `tests/Muonroi.Pdf.Tests/GlobalUsings.cs` — standard usings
- [ ] `tests/Muonroi.Pdf.Tests/Helpers/FakeStyledNode.cs` — IStyledNode test stub
- [ ] Add `Muonroi.Pdf.Tests` to `Muonroi.BuildingBlock.sln`

---

## Security Domain

> `security_enforcement` not set to false — section required.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | Layout engine has no auth surface |
| V3 Session Management | no | Stateless layout computation |
| V4 Access Control | no | No access control in layout engine |
| V5 Input Validation | yes | `IStyledNode` values from trusted cascade output; CSS values pre-validated by Phase 2 policy gate |
| V6 Cryptography | no | No crypto in layout |

### Known Threat Patterns for Layout Engine

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Counter(pages) second-pass DoS via unbounded page generation | DoS | `MaxPages` check after pass 1 throws `PdfInputLimitException` before pass 2 — D9 |
| `page-break-inside: avoid` infinite loop | DoS | Break anyway if element > page height — D9 pattern |
| Deeply nested DOM causing stack overflow in `BoxTreeBuilder` | DoS | `MaxDomDepth` = 256 enforced in Phase 2 before reaching layout |
| CSS `width: 100000mm` causing integer overflow in geometry | Tampering | Use `float` throughout (max finite float ~3.4e38); clamp to page width in layout |

---

## Sources

### Primary (HIGH confidence)
- `CONTEXT.md` (Phase 3 context) — all 11 locked decisions; canonical authority for this phase
- `Directory.Packages.props` — CPM package versions verified directly
- Existing source files in `src/Muonroi.Pdf.Abstractions/`, `src/Muonroi.Pdf.Governance/` — verified current state

### Secondary (MEDIUM confidence)
- CSS 2.1 specification §8.3.1 (margin collapsing), §9.2 (box generation), §17.5.2 (table) [ASSUMED — standard knowledge, not re-fetched during this session]
- ISO 216 page dimensions (A4, A5) [ASSUMED — standard knowledge]
- Unicode TR#14 / UAX#14 (line-break algorithm) [ASSUMED — standard knowledge]

### Tertiary (LOW confidence)
- AngleSharp `IWindow.ComputedStyle` behavior post-cascade — Open Question #1 above
- `@page` rule typed access via AngleSharp.Css — Open Question #3 above

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new packages; all CPM-pinned versions verified in Directory.Packages.props
- Architecture: HIGH — all 11 decisions locked in CONTEXT.md; complete file creation plan provided
- CSS algorithm pitfalls: MEDIUM-HIGH — CSS 2.1 spec is the authority; implementation complexity is real and pitfalls are documented from common CSS layout engine experience
- AngleSharp API for @page/computed style: MEDIUM — used successfully in Phase 2; specific @page typed access not confirmed (Open Question #3)

**Research date:** 2026-05-27
**Valid until:** 2026-06-27 (stable domain; only invalidated if AngleSharp.Css API changes)
