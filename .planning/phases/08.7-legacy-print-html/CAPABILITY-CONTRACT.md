# Legacy Print-HTML Profile v1 — Capability Contract

**Version:** v1.0
**Date:** 2026-05-28
**Status:** Issued (fidelity gate run complete — see §7 Validation Status)

---

## 1. Profile Name and Identity

**Profile name:** Legacy Print-HTML Profile v1
**Policy ID:** `legacy-print-v1`
**Policy class:** `Muonroi.Pdf.Governance.Policies.LegacyPrintPolicy`
  (file: `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs:19`)

**Purpose:** Gate for production shipping/logistics PDF templates. Extends the safe CSS 2.1
print subset with the layout primitives the corpus requires (float, position:absolute,
border-collapse). All other non-print-safe CSS features remain blocked.

**Caller registration (required):**
```csharp
services.TryAddSingleton<IPdfCssPolicy, LegacyPrintPolicy>();
// Must come BEFORE AddTestDoubles() and AddPdf() so TryAdd respects the override.
services.AddPdf(configuration);
```

---

## 2. Policy Limits (PdfPolicyLimits.Strict)

| Limit | Value | Policy ID |
|-------|-------|-----------|
| Max HTML bytes | 512 KB | `limit.max-html-bytes` |
| Max element count | 5 000 | `limit.max-element-count` |
| Max DOM depth | 50 | `limit.max-dom-depth` |

Source: `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs:40-55`

---

## 3. Supported Layout Primitives

The engine **guarantees** to render the following CSS 2.1 layout features correctly:

### 3.1 Table Layout
| Feature | Notes | Source |
|---------|-------|--------|
| `border-collapse: collapse` | Uniform shared-border model | `BoxTreeBuilder.cs:298`, `TableLayoutEngine.cs` |
| `vertical-align: top/middle/bottom` in `<td>/<th>` | Aligns cell content vertically | `BoxTreeBuilder.cs:246,313` |
| `colspan` / `rowspan` | Multi-cell span handling | `TableLayoutEngine.cs` |
| `width` on `<td>/<th>` | Fixed and percentage column widths | `TableLayoutEngine.cs` |
| `background-color` on rows/cells | Header and data row fills | `BoxTreeBuilder.cs:187` |

### 3.2 Float Layout
| Feature | Notes | Source |
|---------|-------|--------|
| `float: left` / `float: right` | Side-by-side block placement | `BoxTreeBuilder.cs:206-208`, `BlockLayoutEngine.cs` |
| `clear: both` / `clear: left` / `clear: right` | Float clearance | `BoxTreeBuilder.cs` |
| Two-column header pattern | logo-float-left + order-block-float-right | corpus HSLA_E, HANG_E, HANG_F |

**Constraint:** Inline text wrapping around floats is NOT implemented. Floats establish
block-formatting contexts. Text following a float clears it.

### 3.3 Positioned Layout
| Feature | Notes | Source |
|---------|-------|--------|
| `position: absolute` in `position: relative` container | px and % offsets supported | `BoxTreeBuilder.cs:216`, `BlockLayoutEngine.cs:76` |
| `top` / `left` / `right` / `bottom` (px, %) | Offset properties | `BlockLayoutEngine.cs` |
| `width` / `height` on absolute elements | Explicit size on overlays | `BlockLayoutEngine.cs` |

**Constraint:** `position: fixed` and `position: sticky` are blocked (violation IDs below).

### 3.4 Inline / Text Features
| Feature | Notes | Source |
|---------|-------|--------|
| `text-transform: uppercase` | Uppercase transformation at render time | `BoxTreeBuilder.cs:279` |
| `white-space: pre-line` / `pre-wrap` | Preserves `\n` line breaks | `BoxTreeBuilder.cs:283` |
| `<nobr>` element | No-break inline wrapper | `BoxTreeBuilder.cs` |
| `rem` units | Resolved relative to root `font-size` (default 16px) | `BoxTreeBuilder.cs` |
| `font-weight: bold` / `font-style: italic` | Selects correct Liberation variant | `BundledFonts.cs:104-113` |
| `font-size` (px, pt, rem) | Absolute and relative sizes | `BoxTreeBuilder.cs:147` |
| `text-align: left/center/right/justify` | Paragraph alignment | `InlineBox.cs` |

### 3.5 Block / Decorative Features
| Feature | Notes | Source |
|---------|-------|--------|
| `background-color` | Any CSS color on any block element | `BoxTreeBuilder.cs:187-190` |
| `background-image: url("data:...")` | Base64 data-URI images (RGB PNG or JPEG) | `BoxTreeBuilder.cs:191-203` |
| `border` shorthand and per-side borders | `border-top/right/bottom/left-width/style/color` | `BoxTreeBuilder.cs:95-145` |
| `padding` / `margin` (px, %, rem) | Block spacing | `BoxTreeBuilder.cs` |
| `width` / `height` (px, %, auto) | Explicit and auto sizing | `BlockLayoutEngine.cs:363` |

### 3.6 Font Mapping (Bundled Liberation Fonts)
| CSS Family Name(s) | Maps To | Variants |
|--------------------|---------|----------|
| `serif`, `Times New Roman`, `Times`, `Georgia` | Liberation Serif (Regular/Bold/Italic/BoldItalic) | 4 |
| `sans-serif`, `Arial`, `Helvetica`, `Verdana` | Liberation Sans (Regular/Bold/Italic/BoldItalic) | 4 |
| `monospace`, `Courier New`, `Courier` | Liberation Mono (Regular/Bold) | 2 |

Source: `src/Muonroi.Pdf/Internal/Font/BundledFonts.cs:23-31`

**Activation requirement:** Fonts are loaded as fallbacks when the template contains no
`@font-face` declarations for those families. The CSS family name used in the template
(e.g. `font-family: serif` or `font-family: "Times New Roman"`) is mapped by `BundledFonts.TryGetFallback()`.

**Known engine bug (see §7):** As of the Wave 5 fidelity run, the bundled-font fallback path
does not produce a cp→newGid mapping for templates without `@font-face` declarations. This
causes `PdfFormatException: Font GID map missing or empty for family '...'` at write time.
Workaround until fixed: add `@font-face { font-family: "Times New Roman"; src: url(data:...); }`
or equivalent in the template, or fix `LayoutEngine.cs:108-110` to produce `EmbeddedFontInfo`
for bundled-font entries (no `FontFaceDeclaration` guard required for bundled fonts).

### 3.7 Image Support
| Feature | Notes |
|---------|-------|
| `<img src="data:image/png;base64,...">` | 8-bit RGB PNG (color_type=2, bit_depth=8) |
| `<img src="data:image/jpeg;base64,...">` | JPEG |
| Background image via `background-image: url("data:...")` | Same formats |

**Rejected image formats:** RGBA PNG (color_type=6 — 4-channel), palette PNG (color_type=3),
grayscale PNG (color_type=0). Rejection is implemented in `PureImageDecoder`.

### 3.8 Page Media
| Feature | Notes | Source |
|---------|-------|--------|
| `@page { size: A4 portrait/landscape }` | Page size from template `@page` rule | `LayoutEngine.cs` |
| `@page { margin: ... }` | Page margins from `@page` rule | `LayoutEngine.cs` |
| Explicit `PdfRenderOptions.PageSize` | Caller can override page size | `PdfRenderOptions` |
| Multi-page output | Automatic page breaks at content overflow | `LayoutEngine.cs` |

---

## 4. Fail-Loud Reject List

The following CSS features cause the policy to reject the document before rendering:

| Violation ID | Feature | Alternative |
|---|---|---|
| `forbidden.display.flex` | `display: flex` / `display: inline-flex` | `display: block` or table layout |
| `forbidden.display.grid` | `display: grid` / `display: inline-grid` | `display: table` |
| `forbidden.position.fixed` | `position: fixed` | `position: absolute` in relative container |
| `forbidden.position.sticky` | `position: sticky` | `position: static` |
| `forbidden.transform.geometric` | `transform: ...` (any value) | Remove transform |
| `forbidden.background.gradient` | `background: linear-gradient(...)` etc. | `background-color: solid` |
| `forbidden.css-animation` | `@keyframes` / CSS animations | Remove |
| `forbidden.css-transition` | `transition: ...` | Remove |
| `forbidden.import.external` | External `@import` URL | Inline the stylesheet |
| `forbidden.script-element` | `<script>` elements | Remove all scripts |
| `forbidden.link.scheme` | `<a href="javascript:...">` / `file:` schemes | `http` / `https` / `mailto` only |

Source: `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs:58-216`

**Not checked but also unsupported (engine limitation, no policy ID):**
- `box-shadow` — silently ignored
- `border-radius` — silently ignored
- `calc()` — not parsed; falls back to 0 or parent value
- CSS custom properties (`--var`) — not resolved
- Nested tables (table inside `<td>`) — renders but layout accuracy not guaranteed

---

## 5. Template Format Contract (Phase 9 Designer Seam)

This section defines the interface between the Phase 9 Designer (template authoring tool)
and the Legacy Print-HTML rendering engine.

### 5.1 Input Format
- **Type:** UTF-8 HTML string
- **State:** Fully filled — no `{{...}}` placeholder tokens remaining when passed to the engine
- **Placeholder fill:** Caller's responsibility; `{{key}}` tokens replaced with string values before `RenderToBytesAsync` is called

### 5.2 Page Size Declaration
The template SHOULD declare page size via CSS `@page`:
```css
@page { size: A4 landscape; margin: 5mm 5mm 5mm 5mm; }
```
Supported size keywords: `A4`, `A5`, `A6`, `Letter`, `Legal` + orientation `portrait`/`landscape`.
If absent, the caller supplies `PdfRenderOptions.PageSize` + `PdfOrientation`.

Corpus page sizes (observed):
| Template | Size | Orientation |
|----------|------|-------------|
| HSLA_E | A5 | Landscape |
| CHNG_F, CSLA_F, GTHA_F, GTND_F, HANG_F, HBCX_F, HBND_F, HSLA_F | A4 | Landscape |
| All others (BNTT, CAPR_E, CHNG_E, CRCD_E, CSLA_E, HANG_E, HBL, NHAR_E) | A4 | Portrait |

### 5.3 Image Format
- **Required:** Base64 data URIs embedded directly in `src` attributes or `background-image`
- **Format:** `data:image/png;base64,<base64>` (8-bit RGB PNG) or `data:image/jpeg;base64,<base64>`
- **Prohibited:** External URLs (file://, http://, relative paths) — engine cannot fetch at render time
- **RGBA PNG:** Rejected by `PureImageDecoder` (use RGB-only PNG)

### 5.4 Font Declaration
- **Preferred:** Rely on bundled Liberation font fallback — no `@font-face` needed in the template
  (when the engine bug at §3.6 is fixed)
- **Current workaround:** Until the bundled-font GID-map bug is fixed, templates must include
  a `@font-face` declaration for each font family they use:
  ```css
  @font-face { font-family: "Times New Roman"; src: url("data:font/truetype;base64,..."); }
  @font-face { font-family: serif; src: url("data:font/truetype;base64,..."); }
  ```
- **Standard family names** (`serif`, `"Times New Roman"`, `Arial`, etc.) map to bundled
  Liberation fonts — the Designer does not need to bundle TTF files into the template HTML

### 5.5 Table Structure Constraints
- **Max nesting depth:** 1 (no table inside `<td>`)
- **Border model:** `border-collapse: collapse` (shared-border model)
- `colspan` and `rowspan` are supported; deeply-nested spans may produce unexpected layout

### 5.6 Template Placeholder Format
- **Syntax:** `{{key}}` — double curly braces, no spaces required (but `{{ key }}` with spaces also supported via explicit dictionary entry)
- **Loop constructs** (`{{for item in items}}...{{end}}`) are NOT interpreted by the engine — the caller must expand loops into flat HTML before rendering
- **Image placeholders:** `{{logo}}` and `{{barcode}}` must be replaced with raw base64 strings (without the `data:image/...` prefix); the template wraps them as `<img src="data:image/png;base64,{{logo}}">`

---

## 6. Layout IR Seam (Phase 9 Note)

The engine produces a `PositionedPageList` — an intermediate representation between CSS parsing
and PDF emission. This is the canonical seam for any future visual renderer or designer.

**Key types:**
| Type | Location | Role |
|------|----------|------|
| `PositionedPageList` | `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` | Root IR: list of pages + embedded font metadata |
| `PositionedPage` | `src/Muonroi.Pdf/Internal/Layout/PositionedPage.cs` | Single page: `Elements` + `LinkAnnotations` |
| `PositionedElement` | `src/Muonroi.Pdf/Internal/Layout/PositionedElement.cs` | Leaf: `Source` (BoxNode) + `Position` (Rect) |
| `BoxNode` | `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` | Abstract box tree node |
| `InlineBox` | `src/Muonroi.Pdf/Internal/Layout/Boxes/InlineBox.cs` | Text run with font/size/color/decoration |
| `BlockBox` | `src/Muonroi.Pdf/Internal/Layout/Boxes/BlockBox.cs` | Block container |
| `ReplacedBox` | `src/Muonroi.Pdf/Internal/Layout/Boxes/` | Image/replaced element |

**Seam contract for Phase 9:**
1. The Designer emits HTML conforming to Profile v1 (§3) — no flex/grid/animation.
2. The engine consumes the HTML, produces `PositionedPageList`, and the writer emits PDF.
3. The Designer does NOT need to understand the IR — it only needs to conform to the CSS profile.
4. **Future opt-in:** If flex/grid layout is later needed, a `FlexLayoutMapper` or `GridLayoutMapper`
   can target the same `PositionedPageList` IR as output. The IR structure is stable.

---

## 7. Validation Status (Wave 5 Fidelity Gate Run)

**Run date:** 2026-05-28
**Harness:** `tests/Muonroi.Pdf.Tests/Golden/RealTemplateBaselineTests.cs`
**Policy:** `LegacyPrintPolicy` (Id: `legacy-print-v1`)
**Template dir:** `D:\Data\Template\Htmls\PreviewRegistion\`

### Render Summary

| Template | Renders to PDF | Renders to PNG | Exception Type | Exception Message |
|----------|:--------------:|:--------------:|----------------|-------------------|
| BNTT | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| CAPR_E | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| CHNG_E | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| CHNG_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| CRCD_E | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| CSLA_E | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| CSLA_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'"Times New Roman"'` |
| GTHA_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| GTND_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'"Times New Roman"'` |
| HANG_E | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| HANG_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| HBCX_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'"Times New Roman"'` |
| HBL | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'"Times New Roman"'` |
| HBND_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'"Times New Roman"'` |
| HSLA_E | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'serif'` |
| HSLA_F | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'"Times New Roman"'` |
| NHAR_E | NO | NO | `PdfFormatException` | Font GID map missing or empty for family `'"Times New Roman"'` |

**Result: 0 / 17 templates rendered to PDF. 0 PNG rasterizations produced.**

### Systemic Bug Report

**Bug ID:** FONT-GID-MAP-MISSING (engine throw code)
**Location:** `src/Muonroi.Pdf/Internal/Writer/OwnedPdfWriter.cs:778`
**Root cause:** `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs:108-110`

The font embedding loop in `LayoutEngine.RunAsync()` iterates `fontBytesMap` (which includes
bundled Liberation fonts registered for CSS aliases `serif`, `"Times New Roman"`, etc.) but
then guards: `if (decl == null) continue` where `decl = doc.FontFaces.FirstOrDefault(f => f.Family == family)`.

Since the 17 real corpus templates contain **no `@font-face` declarations**, `doc.FontFaces` is
empty. Every bundled font entry is skipped. The `embeddedFonts` list remains empty.

At write time, `BuildContentStream()` attempts to encode text using CID/GID encoding.
It looks up `cpToNewGidMap[inline.FontFamily]` — but the map is empty (no fonts were embedded).
The fail-loud guard at `OwnedPdfWriter.cs:778` throws `PdfFormatException` with the message above.

**Why the 312 existing tests pass:** Every existing golden test injects
`@font-face{font-family:serif;src:url(test.ttf);}` into the HTML, so `doc.FontFaces` is non-empty
and the guard at `LayoutEngine.cs:109` passes.

**Fix required (out of scope for Plan 08 — for Phase 09 or Wave 6):**
Option A (minimal): In `LayoutEngine.cs`, produce `EmbeddedFontInfo` for bundled-font entries
regardless of whether a `FontFaceDeclaration` exists. The `decl.Family` / `decl.Weight` /
`decl.Style` can be inferred from the bundled font's canonical family + default weight/style.

Option B (safer): Synthesize synthetic `FontFaceDeclaration` entries for all bundled font
variants during `FontPipeline.ResolveAsync()` when no caller-supplied declaration exists,
so the downstream `LayoutEngine` code path requires no change.

---

## 8. Known Gaps / Out of Scope (Profile v1)

| Gap | Description | Out of scope reason |
|-----|-------------|---------------------|
| Full CSS 2.1 §17.6.2 border-conflict resolution | Complex precedence rules for adjacent cell borders | Corpus uses border-collapse:collapse uniformly; conflict resolution not needed |
| Inline text wrapping around floats | Text reflow around float boxes | Corpus only uses floats for side-by-side blocks; wrapping not used |
| Nested tables | Table inside `<td>` | Corpus max depth = 1; nested tables rarely needed for print layout |
| RGBA PNG images | 4-channel PNG decoding | Corpus uses RGB PNG only; RGBA PNG rejected by PureImageDecoder |
| CSS `calc()` | Math expressions in property values | Not parsed; property falls back to 0 or inherited value |
| CSS custom properties (`--var`) | CSS variable resolution | Not in corpus; not needed for Profile v1 |
| `box-shadow` / `border-radius` | Decorative shadows and rounded corners | Not in corpus; silently ignored |
| External font URLs | `@font-face src: url(http://...)` | SEC-02: no external resource fetching permitted |
| SVG content | Inline `<svg>` or `<img src="data:image/svg+xml,..">` | Not in corpus; not in engine scope |
| JavaScript | `<script>` elements | Rejected by policy (`forbidden.script-element`) |
