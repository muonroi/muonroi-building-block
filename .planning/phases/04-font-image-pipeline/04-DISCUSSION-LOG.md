# Phase 4 Discussion Log: Font + Image Pipeline

**Date**: 2026-05-27
**Mode**: Headless autonomous — all decisions made without interactive discussion
**Phase**: 4 of 9

---

## Gray Areas Identified

### 1. Who extracts `@font-face` declarations?

**Options Presented**:
- A: Scan `IStyledNode` tree at layout time for computed `font-family` values
- B: Add `FontFaces` to `IStyledDocument`; extract in Governance (AngleSharp stylesheet AST access)
- C: `FontPipeline` parses raw CSS text for `@font-face` rules independently

**Selected**: B — `IStyledDocument.FontFaces`; Governance extracts; `FontFaceDeclaration` record in Abstractions

**Notes**: Option A misses explicit `@font-face` declarations (only resolves fonts that appear in computed styles, not declared but possibly unused ones). Option C duplicates CSS parsing already in Governance. Option B is consistent with Phase 3's `IPageRule` / `IStyledDocument.PageRule` pattern — Governance extracts stylesheet metadata, layout engine consumes it through Abstractions.

---

### 2. How is `FontCollection` built and `SixLaborsTextMetrics` constructed?

**Options Presented**:
- A: Lazy — resolve fonts on first `GetCharWidth` call during layout
- B: Pre-layout async `FontPipeline` pass — resolve all, build collection, construct `SixLaborsTextMetrics`
- C: DI singleton `FontCollection` shared across renders

**Selected**: B — `FontPipeline` pre-layout async pass

**Notes**: Option A makes the sync layout engine implicitly async (wrong). Option C creates cross-render state contamination (font bytes from tenant A visible in tenant B render — violates D16 multi-tenant isolation). Option B is the clean pattern: async I/O before sync layout, consistent with `ImagePipeline`. MaxFontFiles validation happens in `FontPipeline` before any resolver calls.

---

### 3. Font subsetting — how subset binary is produced

**Options Presented**:
- A: No subsetting — embed full font bytes (violates FONT-03)
- B: SixLabors.Fonts glyph ID tracking + hand-written `TrueTypeFontSubsetter` (TTF only; OTF CFF full embedding)
- C: Add a managed subsetting library (no MIT pure-managed option exists)

**Selected**: B — `GlyphCollector` + `TrueTypeFontSubsetter` internal classes

**Notes**: Option A is a hard FONT-03 violation. Option C has no viable candidate (HarfBuzz is native; no pure-managed MIT TTF subsetter exists on NuGet). Option B delivers the requirement with known scope: TTF glyph table subsetting is well-specified (glyf/loca/cmap/hmtx). OTF CFF limitation documented in `KNOWN-DEVIATIONS.md`. SixLabors.Fonts satisfies "applied via SixLabors.Fonts" by providing the glyph ID mapping from Unicode codepoints.

---

### 4. Image decoding library

**Options Presented**:
- A: `SixLabors.ImageSharp` — full pixel decode; license risk (STATE.md blocker)
- B: `StbImageSharp` — MIT, pure managed; full pixel decode; new NuGet dependency
- C: `PureImageDecoder` — PNG IHDR + JPEG SOF header-only parse; no pixels needed; no new library

**Selected**: C — `PureImageDecoder`

**Notes**: Option A has an explicit license audit blocker (STATE.md). Option B would work but introduces an unnecessary dependency — Phase 5 uses `PdfSharpCore.Drawing.XImage.FromStream(rawBytes)` which accepts compressed PNG/JPEG natively; no pixel decode is ever needed. Option C is the minimal correct implementation: parse width/height from headers, pass raw bytes through. Pure managed, AOT-safe, zero new dep.

---

### 5. How do font/image data reach Phase 5?

**Options Presented**:
- A: New interface `IEnrichedPageList : IPositionedPageList` with `EmbeddedFonts` and `Images`
- B: Extend internal `PositionedPageList` with `EmbeddedFonts` and `Images` properties
- C: Separate carrier objects passed alongside `IPositionedPageList` to the PDF writer

**Selected**: B — extend `PositionedPageList`

**Notes**: Option A adds an Abstractions interface that external consumers would see (leaks Phase 4 internals). Option C requires changing `IPdfWriter` signature (Phase 5 contract in Abstractions — premature). Option B uses the established pattern from Phase 2 (same-assembly internal cast from `IPositionedPageList` → `PositionedPageList` in `PdfSharpCoreWriter`). No Abstractions or public API change needed.

---

## Deferred Ideas

- Background image `data:` URI support — deferred to post-v0.1; no policy gate needed (data: scheme allowed by `IResourceResolver` pattern), but no consumer requirement in v0.1
- OTF CFF subsetting — complex separate algorithm; full bytes embedded in Phase 4; deferred to post-v0.1 if file size concern arises
- Font fallback chain resolution (e.g., `font-family: 'Custom', Arial`) — Phase 4 resolves first `@font-face` family only; generic fallback uses estimated proportions; documented deviation

---

## Claude Discretion Items

- Chose `PureImageDecoder` over `StbImageSharp` to avoid any new dependency risk, based on the insight that PdfSharpCore accepts raw compressed bytes — no pixel decode path is ever needed in this pipeline
- Chose `TrueTypeFontSubsetter` (hand-written) because no viable managed MIT option exists; the TTF table format is stable enough for a focused internal implementation scoped to glyf-flavored fonts
- Decided to add `FontFaces` to `IStyledDocument` (Abstractions interface change) rather than scanning computed styles, because `@font-face` declarations are stylesheet-level constructs that may declare fonts never referenced in computed styles — the CSS model requires processing them explicitly
