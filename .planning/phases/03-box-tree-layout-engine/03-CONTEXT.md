# Phase 3 Context: Box Tree + Layout Engine

**Phase**: 3 of 9
**Name**: Box Tree + Layout Engine
**Date captured**: 2026-05-27
**Mode**: Headless autonomous (no interactive discussion)

---

## Domain

A styled DOM (produced by Phase 2 cascade in `Muonroi.Pdf.Governance`) converts to an internal box tree, which the layout engine walks to produce a `IPositionedPageList` output. The layout engine handles block/inline/table formatting contexts, margin collapsing, pagination, page counters, and `@page` header/footer repetition. No font shaping, no image decoding, no PDF writing — those are Phases 4 and 5.

Requirements locked: PIPE-05, PIPE-06, LAYOUT-01, LAYOUT-02, LAYOUT-03, LAYOUT-04, LAYOUT-05, LAYOUT-06, LAYOUT-07, PAGE-01, PAGE-02, PAGE-03, PAGE-04, PAGE-05, PAGE-06, PAGE-07, PAGE-08.

---

## Canonical References

- `.planning/REQUIREMENTS.md` — locked requirements PIPE-05, PIPE-06, LAYOUT-01–07, PAGE-01–08
- `.planning/ROADMAP.md` — Phase 3 success criteria (SC1–SC5)
- `.planning/PROJECT.md` — Key Decisions table (hand-written layout engine, no HtmlRenderer.PdfSharp fork, D1)
- `.planning/phases/01-abstractions-contracts/01-CONTEXT.md` — adapter seam shapes; `IPositionedPageList`, `IStyledDocument` defined as marker interfaces
- `.planning/phases/02-parse-cascade-policy-gate/02-CONTEXT.md` — `AngleSharpStyledDocument` design; cascade phase output contract
- `src/Muonroi.Pdf.Abstractions/Engine/IStyledDocument.cs` — current marker interface (to be extended in Phase 3a)
- `src/Muonroi.Pdf.Abstractions/Engine/IPositionedPageList.cs` — marker interface; remains marker
- `src/Muonroi.Pdf.Abstractions/Engine/IPdfWriter.cs` — consumer of `IPositionedPageList` (Phase 5)
- `src/Muonroi.Pdf.Abstractions/PdfRenderOptions.cs` — page size, orientation, margins, header, footer, policy
- `src/Muonroi.Pdf.Abstractions/PdfPageSize.cs` — A4/A5/A3/Letter/Legal
- `src/Muonroi.Pdf.Abstractions/PdfMargins.cs` — margin values
- `src/Muonroi.Pdf.Abstractions/PdfHeaderFooter.cs` — API-level header/footer override
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs` — holds `internal IDocument AngleSharpDocument { get; }`; to be extended with `IStyledNode` implementation

---

## Existing State (verified 2026-05-27)

| Component | Status |
|-----------|--------|
| `Muonroi.Pdf.Abstractions` | Complete; `IStyledDocument` and `IPositionedPageList` are marker-only |
| `Muonroi.Pdf.Governance` | Complete Phase 2 implementation; `AngleSharpStyledDocument` holds DOM internally |
| `Muonroi.Pdf/Internal/`, `Muonroi.Pdf/Extensions/` | Directories exist, empty — no csproj yet |
| `Muonroi.Pdf.csproj` | Missing — must create in Phase 3 |
| `IStyledNode`, `IComputedStyle`, `IPageRule` interfaces | Missing — must add to Abstractions in Phase 3a |
| Box tree types | Missing — Phase 3d |
| Layout engine | Missing — Phase 3e |

---

## Implementation Decisions

### Decision 1: Layout engine package — `Muonroi.Pdf` only, no Governance reference

**Problem**: The layout engine must walk the styled DOM to build boxes, but `AngleSharpStyledDocument.AngleSharpDocument` is `internal` to `Muonroi.Pdf.Governance`. The layout code in `Muonroi.Pdf` cannot access it directly.

**Decision**: Extend `IStyledDocument` in Abstractions with a **traversal interface** — `Root : IStyledNode` and `PageRule : IPageRule?`. The layout engine in `Muonroi.Pdf` walks the tree via these interfaces. The `AngleSharpStyledDocument` in Governance implements `IStyledNode` by wrapping AngleSharp DOM nodes. No `InternalsVisibleTo`, no cross-package internal cast.

**Why**: Keeps the layout engine free of any AngleSharp dependency. The `IStyledNode` traversal interface becomes a stable seam — the v0.2 source generator and AOT path can implement `IStyledNode` directly without AngleSharp. Testability: box tree and layout tests can use `FakeStyledNode` stubs without a real parsed document.

**`Muonroi.Pdf.csproj` only references Abstractions** (not Governance). Governance reference is added in Phase 6 when DI wiring is implemented.

```xml
<ProjectReference Include="..\Muonroi.Pdf.Abstractions\Muonroi.Pdf.Abstractions.csproj" />
```

---

### Decision 2: `IStyledDocument` traversal interface — add to Abstractions in Phase 3a

**Problem**: `IStyledDocument` is currently a marker interface. Layout engine has no way to walk computed styles.

**Decision**: Add to `Muonroi.Pdf.Abstractions/Engine/`:

```csharp
// IStyledNode.cs
public interface IStyledNode {
    string LocalName { get; }
    string? TextContent { get; }
    IComputedStyle Style { get; }
    IReadOnlyList<IStyledNode> Children { get; }
    string? GetAttribute(string name);
    bool IsElement { get; }
    bool IsText { get; }
}

// IComputedStyle.cs
public interface IComputedStyle {
    string? GetValue(string property);
    bool HasProperty(string property);
}

// IPageRule.cs
public interface IPageRule {
    PdfMargins Margins { get; }
    string? TopMarginBoxHtml { get; }
    string? BottomMarginBoxHtml { get; }
    string? Size { get; }
}
```

Extend `IStyledDocument`:
```csharp
public interface IStyledDocument {
    IStyledNode Root { get; }
    IPageRule? PageRule { get; }
}
```

**Governance gap closure (Phase 3b)**: `AngleSharpStyledDocument` implements the extended `IStyledDocument`. Adds:
- `AngleSharpStyledNode : IStyledNode` — wraps `IElement`/`IText` + `IStyleDeclaration` from `IWindow.ComputedStyle(element)`
- `AngleSharpComputedStyle : IComputedStyle` — wraps `IStyleDeclaration`
- `AngleSharpPageRule : IPageRule` — extracted from `document.StyleSheets` during `AngleSharpStyledDocument` construction
- `AngleSharpStyledDocument.Root` — wraps `document.DocumentElement` as `AngleSharpStyledNode`
- `AngleSharpStyledDocument.PageRule` — extracted `@page` rule or null

**Why**: The Abstractions traversal interface is the correct architectural seam. All layout logic operates on `IStyledNode` — no AngleSharp in the layout hot path.

---

### Decision 3: `@page` rule handling — parsed in Governance, consumed by layout engine

**Problem**: `@page` specifies margins, size, and margin-box HTML (top/bottom for header/footer). Where does parsing live?

**Decision**: `AngleSharpStyledDocument` extracts `@page` metadata at construction time during the cascade phase (in Governance). The layout engine reads `IStyledDocument.PageRule` — it does not touch AngleSharp stylesheet AST.

**Precedence rules** (layout engine applies in this order):
1. `PdfRenderOptions.Margins` (if non-default) → overrides CSS `@page` margins
2. CSS `@page { margin: ... }` → overrides `PdfRenderOptions.Margins.Default10mm` baseline
3. `PdfRenderOptions.Header/Footer` (if set) → overrides `IPageRule.TopMarginBoxHtml/BottomMarginBoxHtml`
4. `IPageRule.TopMarginBoxHtml/BottomMarginBoxHtml` (if `@page` provides content) → used as header/footer when no API override
5. CSS `@page { size: ... }` → **IGNORED in Phase 3** (always use `PdfRenderOptions.PageSize`); document in `KNOWN-DEVIATIONS.md`

**Why**: `PdfRenderOptions` is the caller-controlled API surface. CSS `@page` size override adds complexity with no v0.1 consumer demand; the ROADMAP success criteria only test margins and margin-box content, not `@page { size }`. The deviation is explicitly scoped to v0.1.

---

### Decision 4: Two-pass layout for `counter(pages)` — full implementation in Phase 3

**Problem**: STATE.md blocker: "`counter(pages)` two-pass design: Architecture decision required in Phase 3 design before implementation; cannot be retrofitted into the layout engine later." SC5 requires `counter(pages)` resolves correctly.

**Decision**: Full two-pass implementation. The layout engine entry point is:

```csharp
// Internal to Muonroi.Pdf
internal sealed class LayoutEngine {
    public IPositionedPageList Layout(IStyledDocument doc, PdfRenderOptions options) {
        // Pass 1: layout with pages=0 placeholder
        var pass1 = RunLayout(doc, options, totalPages: 0);
        int pageCount = pass1.PageCount;
        // Pass 2: layout with resolved total
        return RunLayout(doc, options, totalPages: pageCount);
    }
    private PositionedPageList RunLayout(IStyledDocument doc, PdfRenderOptions options, int totalPages) { ... }
}
```

`counter(page)` is resolved during layout (current page number is known as layout runs page by page). `counter(pages)` substitutes `totalPages`.

**Edge case accepted**: If the text rendering of `totalPages` (e.g., "10" vs "9") shifts a page boundary, pass 2 result differs from pass 1. The pass 2 result is authoritative. A third pass is NOT implemented. This edge case (multi-digit boundary shift) is documented in `KNOWN-DEVIATIONS.md`. In practice, footer/header content like "Page 1 of N" is one line and does not affect body pagination.

**Why**: Two-pass is the standard approach for paginated documents. The two-pass runs the full layout twice — acceptable cost at ≤300 ms cold for a 50 KB template (PERF-01 target is at Phase 7 after full pipeline integration). Cannot defer: the `IPositionedPageList` contract must be settled before Phase 5 (PDF writer) can consume it.

---

### Decision 5: Box tree internal types — all in `Muonroi.Pdf/Internal/Layout/Boxes/`

**Problem**: What types make up the box tree?

**Decision**: All box types are `internal sealed` classes in `Muonroi.Pdf/Internal/Layout/Boxes/`:

```
BoxNode (abstract) — common: display type, containing-block width, margin/padding/border (resolved px values)
  ├── BlockBox — block formatting context participant; has list of children
  ├── InlineBox — text run or inline element; has text content or inline-block children
  ├── AnonymousBox — anonymous block/inline wrapper per CSS 2.1 §9.2.1
  ├── TableBox — wraps TableRowGroupBox children
  │   ├── TableRowGroupBox (thead/tbody/tfoot)
  │   │   └── TableRowBox
  │   │       └── TableCellBox — independent BFC; has colspan/rowspan
  └── ReplacedBox — placeholder for images (Phase 4 fills in actual decoded image); carries width/height from style
```

`IPositionedPageList` stays as a marker interface. The concrete `PositionedPageList` is `internal sealed` in `Muonroi.Pdf/Internal/Layout/`. Phase 5's `PdfSharpCoreWriter` (in the same `Muonroi.Pdf` assembly) casts `IPositionedPageList` to `PositionedPageList` directly — same-assembly internal cast, consistent with Phase 2 pattern.

**Why**: Box tree types are pure implementation detail of the layout engine. They don't belong in Abstractions (no external consumer needs them). The `IPositionedPageList` marker seam isolates Phase 5 from Phase 3 box tree internals — Phase 5 planner can make different internal representation decisions as long as the cast works.

---

### Decision 6: Block formatting context and margin collapsing — per CSS 2.1 §8.3.1

**Problem**: Adjacent block margins must collapse; BFC roots must NOT collapse across the boundary.

**Decision**:
- BFC roots in Phase 3 scope: `overflow: hidden`, table cells, `display: inline-block`, root element
- Margin collapsing: adjacent sibling collapsing (max of two margins), parent-child collapsing (if no border/padding/clearance between them), empty block collapsing
- `page-break` does NOT collapse across a page boundary — margin at the bottom of a page and top of the next page both apply in full
- Negative margins: supported per CSS 2.1 (max of positives minus absolute of negatives)
- Float/clearance interaction: NOT in scope (float rejected by policy gate in Phase 2)

**Why**: CSS 2.1 §8.3.1 is the governing spec. Only the block-layout-relevant cases apply here; float clearance is excluded because float is rejected by `DefaultStrictPolicy`. This bounds the implementation to the cases tested in SC1.

---

### Decision 7: Inline layout — simplified two-level line boxes

**Problem**: Inline layout requires baseline computation for mixed content. SC2 requires correct `vertical-align` offsets for mixed Latin+Vietnamese in the same line.

**Decision**: Simplified two-level approach:
- **Line box**: fixed-width container derived from containing block width
- **Inline box**: leaf (text run) or inline-block (treated as replaced)
- Baseline: maximum ascender from any inline box in the line. All inline boxes aligned to this baseline
- `vertical-align: top` → shift inline box up so its top aligns with line box top
- `vertical-align: middle` → shift inline box so its midpoint aligns with line box midpoint
- `vertical-align: bottom` → shift inline box down so its bottom aligns with line box bottom
- Unicode line-break opportunities: determined by `line-break: normal` (Unicode TR#14 default algorithm). Vietnamese: UAX#14 treats Vietnamese as ideographic at word boundaries — correct for Phase 3 (full shaping is Phase 4's responsibility)

**Measurement without font metrics**: Phase 3 does not have font metrics (Phase 4). For Phase 3 layout, use estimated metrics: `font-size` as line height, `font-size * 0.8` as ascender. Phase 4 replaces these estimates with real SixLabors.Fonts metrics. The `LayoutEngine` interface accepts an injectable `ITextMetrics` provider — Phase 3 uses `EstimatedTextMetrics`, Phase 4 replaces with `SixLaborsTextMetrics`.

**Why**: Full CSS inline formatting model (strut, CSS baseline table) is complex. The simplified two-level approach handles the tested cases (SC2). The `ITextMetrics` seam is the key design: Phase 3 layout and Phase 4 font integration are decoupled without changing the layout engine's interface.

---

### Decision 8: Table layout — fixed and auto width, colspan/rowspan

**Problem**: LAYOUT-04, LAYOUT-05, LAYOUT-06, SC3. Tables require column sizing before content layout.

**Decision**:
- **Column width algorithm**: Phase 3 implements fixed-width (`table-layout: fixed`) and auto-width (`table-layout: auto`) per CSS 2.1 §17.5.2. Auto-width: preferred widths computed from content min-width, distributed proportionally.
- **colspan**: A cell spanning N columns contributes its min-width spread equally across spanned columns (initial pass), then the spanning algorithm (CSS 2.1 §17.5.2.2) redistributes.
- **rowspan**: Two-pass: first pass lays out all rows assuming rowspan=1; second pass distributes excess height to spanned rows.
- `border-collapse: separate` enforced; `border-spacing` applied between cells. `border-collapse: collapse` is NOT in Phase 3 scope — the policy gate already rejects it (LAYOUT-07, SC3: PolicyViolation is emitted by Phase 2, not Phase 3).
- Each table cell is an independent BFC — margin collapsing does NOT cross cell boundaries.

**Why**: CSS 2.1 table layout is deterministic and well-specified. The `border-collapse: collapse` PolicyViolation is already handled in Phase 2 (DefaultStrictPolicy). Phase 3 only needs to correctly handle `border-collapse: separate` — the only value the policy permits.

---

### Decision 9: Pagination — break-before/after, header/footer repetition, page counter

**Problem**: PAGE-01–PAGE-08. Multiple interacting features: forced breaks, optional avoidance, page counter, header/footer.

**Decision**:
- `page-break-before: always` → force page break immediately before the element's box
- `page-break-after: always` → force page break immediately after the element's box
- `page-break-inside: avoid` → attempt to keep element on one page; if element height > available page height, break anyway (no infinite loop)
- Orphans/widows: NOT implemented in Phase 3; document in `KNOWN-DEVIATIONS.md`
- Page counters: `counter(page)` = current 1-based page number (resolved during layout pass). `counter(pages)` = `totalPages` parameter (two-pass — see Decision 4).
- Header/footer repetition: After layout of each page's body content, prepend/append the header/footer layout (from `IPageRule.TopMarginBoxHtml/BottomMarginBoxHtml` or `PdfRenderOptions.Header/Footer`). Header/footer is laid out as a static mini-document at fixed height — does NOT participate in body pagination.
- `MaxPages` enforcement: If page count from pass 1 exceeds `PdfConfigs.Limits.MaxPages`, throw `PdfInputLimitException` before running pass 2.

**Why**: This exactly satisfies PAGE-01–PAGE-08 and SC4/SC5 without over-engineering. Orphans/widows is explicitly out of Phase 3 scope (no success criterion requires them).

---

### Decision 10: `Muonroi.Pdf.csproj` — create targeting net8.0, Abstractions reference only

**Problem**: `src/Muonroi.Pdf/` has `Internal/` and `Extensions/` directories but no csproj. The layout engine goes here.

**Decision**: Create `src/Muonroi.Pdf/Muonroi.Pdf.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Description>HTML/CSS to PDF rendering engine for Muonroi — box tree, layout, and DI registration.</Description>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Muonroi.Pdf.Abstractions\Muonroi.Pdf.Abstractions.csproj" />
  </ItemGroup>
</Project>
```

Governance reference is NOT added now — it is added in Phase 6 when `AddPdf()` DI wiring is implemented. The layout engine operates only on Abstractions interfaces (`IStyledDocument`, `IStyledNode`, `IComputedStyle`, `IPageRule`). Add project to `Muonroi.BuildingBlock.sln`.

---

### Decision 11: `ITextMetrics` seam — injectable, Phase 4 replaces the estimate

**Problem**: Phase 3 needs character widths and font metrics for line-breaking and baseline computation. Phase 4 has not shipped yet.

**Decision**: Define `internal interface ITextMetrics` in `Muonroi.Pdf/Internal/Layout/`:
```csharp
internal interface ITextMetrics {
    float GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic);
    float GetLineHeight(string fontFamily, float fontSize);
    float GetAscender(string fontFamily, float fontSize);
    float GetDescender(string fontFamily, float fontSize);
}
```

Phase 3 ships with `EstimatedTextMetrics : ITextMetrics` — uses `fontSize * 0.6` per char (monospace approximation), `fontSize * 1.2` line height. Phase 4 ships `SixLaborsTextMetrics : ITextMetrics` and replaces it. The `LayoutEngine` constructor receives `ITextMetrics` — Phase 6 DI wires the real implementation.

**Why**: Keeps Phase 3 shippable without blocking on Phase 4. The estimated metrics are correct enough for pagination logic testing. The seam is internal — no Abstractions change required in Phase 4.

---

## File Creation Plan

Priority order (strict dependency):

**Phase 3a — Abstractions gap closure** (`Muonroi.Pdf.Abstractions`):
1. `Engine/IStyledNode.cs` — traversal interface
2. `Engine/IComputedStyle.cs` — computed CSS property accessor
3. `Engine/IPageRule.cs` — @page metadata
4. Extend `Engine/IStyledDocument.cs` — add `Root : IStyledNode` and `PageRule : IPageRule?`

**Phase 3b — Governance gap closure** (`Muonroi.Pdf.Governance`):
5. `Cascade/AngleSharpComputedStyle.cs` — wraps `IStyleDeclaration`
6. `Cascade/AngleSharpStyledNode.cs` — wraps `IElement`/`IText` + computed style
7. `Cascade/AngleSharpPageRule.cs` — extracts `@page` rule from stylesheet
8. Extend `Cascade/AngleSharpStyledDocument.cs` — implement `Root` and `PageRule` properties

**Phase 3c — Package setup**:
9. Create `src/Muonroi.Pdf/Muonroi.Pdf.csproj` (net8.0, Abstractions-only reference)
10. Add to `Muonroi.BuildingBlock.sln`
11. `src/Muonroi.Pdf/Internal/GlobalUsings.cs` — standard usings

**Phase 3d — Box tree** (`Muonroi.Pdf/Internal/Layout/`):
12. `Geometry/Units.cs` — CSS unit to points conversion (mm→pt: multiply by 2.834646)
13. `Geometry/Rect.cs` — `readonly struct Rect(float X, float Y, float Width, float Height)`
14. `Boxes/BoxNode.cs` — abstract; `DisplayType`, `MarginTop/Right/Bottom/Left`, `PaddingTop...`, `BorderTop...`, `Width`, `Height` (all resolved px values)
15. `Boxes/BlockBox.cs`
16. `Boxes/InlineBox.cs`
17. `Boxes/AnonymousBox.cs`
18. `Boxes/TableBox.cs` + `TableRowGroupBox.cs` + `TableRowBox.cs` + `TableCellBox.cs`
19. `Boxes/ReplacedBox.cs` — image placeholder with `float NaturalWidth`, `float NaturalHeight`
20. `BoxTreeBuilder.cs` — walks `IStyledNode` tree; produces `BlockBox` root

**Phase 3e — Layout engine** (`Muonroi.Pdf/Internal/Layout/`):
21. `ITextMetrics.cs` — internal interface
22. `EstimatedTextMetrics.cs` — approximation impl
23. `LayoutContext.cs` — carries available width, current y-cursor, current page index, `totalPages` parameter
24. `BlockLayoutEngine.cs` — BFC, margin collapsing per CSS 2.1 §8.3.1
25. `InlineLayoutEngine.cs` — line boxes, baseline, `vertical-align`
26. `TableLayoutEngine.cs` — column sizing, colspan/rowspan
27. `PaginationEngine.cs` — page breaks, header/footer repetition, counter(page/pages)
28. `PositionedElement.cs` — `sealed class`: `Rect Position`, `BoxNode Source`, `int PageIndex`
29. `PositionedPage.cs` — list of `PositionedElement` for one page
30. `PositionedPageList.cs : IPositionedPageList` — `IReadOnlyList<PositionedPage> Pages`, `int PageCount`
31. `LayoutEngine.cs` — entry point; two-pass orchestration

**Phase 3f — Verification**:
32. `dotnet build` on solution — must pass 0 errors
33. Success criteria SC1–SC5 manual verification via unit tests in `tests/Muonroi.Pdf.Tests/`

---

## Out of Phase 3 Scope

- Font metrics (real) — Phase 4 (`SixLaborsTextMetrics`)
- Image decoding — Phase 4
- `@font-face` resolution — Phase 4
- PDF file writing — Phase 5 (`PdfSharpCoreWriter`)
- `AddPdf()` DI registration — Phase 6
- `Muonroi.Pdf.csproj` Governance reference — Phase 6 (DI wiring only)
- `counter(page)` inside header/footer content (nested counter) — KNOWN-DEVIATIONS.md
- Orphans/widows — KNOWN-DEVIATIONS.md
- CSS `@page { size: ... }` override of `PdfRenderOptions.PageSize` — KNOWN-DEVIATIONS.md
- Two-pass layout third iteration (multi-digit boundary shift edge case) — KNOWN-DEVIATIONS.md

---

## Autonomous Gray Area Resolutions

| Gray Area | Decision | Rationale |
|-----------|----------|-----------|
| How does layout engine access styled DOM? | Extend `IStyledDocument` with `IStyledNode` traversal interface in Abstractions | Clean seam; no AngleSharp in layout hot path; enables v0.2 SG and AOT |
| Where does `@page` parsing live? | Governance (cascade phase); exposed via `IStyledDocument.PageRule` | Layout engine stays AngleSharp-free; one parsing location |
| Two-pass for `counter(pages)` — implement or defer? | Full two-pass in Phase 3 | STATE.md blocker: cannot retrofit; SC5 success criterion requires it |
| Font metrics without Phase 4? | `ITextMetrics` seam with `EstimatedTextMetrics` impl | Phase 3 ships; Phase 4 replaces impl; no layout engine change needed |
| `IPositionedPageList` — marker or richer? | Stay marker; `PositionedPageList` internal; Phase 5 casts internally | Consistent with Phase 2 pattern; Phase 5 planner retains flexibility |
| `Muonroi.Pdf.csproj` Governance reference? | NOT added in Phase 3 | Layout engine operates on Abstractions only; Governance ref deferred to Phase 6 DI |
