# Legacy Print-HTML Profile v1 — Public Specification

> **Profile name:** Legacy Print-HTML Profile v1
> **Policy ID:** `legacy-print-v1`
> **Version:** 1.0
> **Issued:** 2026-05-29 (post-Phase-8.16)
> **Audience:** template authors, integrators, Phase 9 Designer implementers
> **Status:** **STABLE — production-ready.** 17/17 production templates render at Chrome parity.

This document is the public contract for the v1 rendering profile of `Muonroi.Pdf`. It declares
what HTML/CSS subset the engine guarantees to render correctly, what it rejects loud at the
policy gate, and what it silently ignores. Anything outside the declared subset is unsupported —
do not rely on incidental behaviour.

For the historical phase-internal version used during 8.7 implementation see
`.planning/phases/08.7-legacy-print-html/CAPABILITY-CONTRACT.md`. This document supersedes it.

---

## 1. Identity

| Field | Value |
|---|---|
| Profile name | Legacy Print-HTML Profile v1 |
| Policy ID | `legacy-print-v1` |
| Policy class | `Muonroi.Pdf.Governance.Policies.LegacyPrintPolicy` |
| Engine version | `Muonroi.Pdf` v1.0 (post-Phase-8.16) |
| Corpus | 17 production shipping/logistics templates |
| Reference renderer | Chromium (visual parity bar) |

**Purpose.** Gate for production shipping/logistics PDF templates. Extends the safe CSS 2.1 print
subset with the layout primitives the corpus requires (float, `position:absolute`,
`border-collapse`, fixed table layout, synthetic bold/italic, descendant class selectors). All
other non-print-safe CSS features remain blocked.

**Caller registration:**
```csharp
services.TryAddSingleton<IPdfCssPolicy, LegacyPrintPolicy>();
services.AddPdf(configuration);
```

The policy registration must come BEFORE `AddPdf(...)` so the `TryAddSingleton` respects the
override over the default permissive policy.

---

## 2. Policy Limits (PdfPolicyLimits.Strict)

| Limit | Value | Policy ID |
|---|---|---|
| Max HTML bytes | 512 KB | `limit.max-html-bytes` |
| Max element count | 5 000 | `limit.max-element-count` |
| Max DOM depth | 50 | `limit.max-dom-depth` |
| Max image pixels | `PdfLimits.Defaults.MaxImagePixels` | rejected with `IMG-MAX-PIXELS` |

Source: `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs`

---

## 3. Supported Layout Primitives

The engine **guarantees** the following CSS features render correctly. Each guarantee is backed
by at least one test in `tests/Muonroi.Pdf.Tests/` and exercised across the production corpus.

### 3.1 Table Layout

| Feature | Notes |
|---|---|
| `display: table / table-row / table-row-group / table-cell` | HTML5 UA stylesheet defaults applied (G14 — Phase 8.12) |
| `border-collapse: collapse` | Uniform shared-border model with descendant-selector borders (G17 — 8.12, G23c — 8.14) |
| `border-collapse: separate` | Separated cell borders (default) |
| `table-layout: fixed` | Fixed column widths from declared `<col>` / `<th>` / `<td>` widths; class-rule fallback (G23a — 8.14) |
| `table-layout: auto` | Width inferred from content |
| Proportional fixed-layout scaling | When declared widths sum < table width, columns scale proportionally per CSS 2.1 §17.5.2.1 (G23e — 8.14) |
| `colspan` / `rowspan` | Multi-cell span handling |
| `width` on `<td>` / `<th>` | Fixed px, fixed pt, and percentage column widths (G23b — 8.14 fixed % double-application) |
| Inline-style `width` on cells | `<th style="width:16%">` honoured even when `GetComputedStyle` throws (G23a — 8.14) |
| `<th>` UA bold | UA default `font-weight: bold` applied (G23d — 8.14) |
| `<th>` UA centered | UA default `text-align: center` applied (G23g — 8.15) |
| Cell `text-align` propagation | `<td class="text-center">` content centers correctly (G23h — 8.15) |
| `background-color` on rows/cells | Header and data row fills |
| `vertical-align: top / middle / bottom` in cells | Cell content vertical alignment |

**Constraints:**
- Max nesting depth: 1 (no `<table>` inside `<td>`; render works but layout accuracy is not guaranteed).
- Nested `colspan` + `rowspan` combinations exercised by the corpus are guaranteed; deeply unusual combinations may produce unexpected layout.

### 3.2 Float Layout

| Feature | Notes |
|---|---|
| `float: left` / `float: right` | Side-by-side block placement |
| Multiple sibling floats | Horizontal flow (e.g. `w-20` + `w-50` + `w-30`) supported (G15 / G15b — 8.12) |
| `clear: both` / `clear: left` / `clear: right` | Float clearance |
| `float` inside `border-collapse:collapse` table cells | Renders correctly (G2 — 8.8) |
| Float establishes its own BFC | Correct CSS 2.1 §9.4.1 semantics (TD3 — 8.11) |
| ExcludedShapes float placement | WeasyPrint-derived solver — `AvoidCollisions`, `AvailableWidthAtY`, `ClearY` (Phase 8.10) |
| Floated child layout (`% width`) | Inner width resolves against float width, not parent (G19 — 8.13) |

**Constraint:** Inline text wrapping around floats is NOT implemented. Floats establish
block-formatting contexts. Text following a float clears it.

### 3.3 Positioned Layout

| Feature | Notes |
|---|---|
| `position: relative` containers | Establishes containing block |
| `position: absolute` inside relative container | px and % offsets supported |
| `position: absolute` inside `overflow: hidden` table cell | Renders at cell position, not page origin (G9 — 8.11) |
| `top` / `left` / `right` / `bottom` (px, %) | Offset properties |
| `width` / `height` on absolute elements | Explicit size on overlays |

**Constraint:** `position: fixed` and `position: sticky` are rejected by the policy.

### 3.4 Inline / Text Features

| Feature | Notes |
|---|---|
| `text-transform: uppercase` | Captured at glyph-collection time so subsets include uppercase codepoints (G22 — 8.14) |
| Vietnamese diacritics under uppercase | Ế / Ă / Ý / À etc. render correctly (G22 — 8.14) |
| `white-space: pre-line` / `pre-wrap` / `nowrap` | Whitespace handling |
| `<nobr>` element | No-break inline wrapper |
| `<br>` line break | Hard newline |
| Inline anchors `<a href>` | `http` / `https` / `mailto` / relative URLs; other schemes silently dropped |
| URI link annotations in PDF | Emitted (SEC-02 allows URI; never `/JavaScript` / `/Launch`) |
| `<u>`, `<s>` / `<strike>` / `<del>` | Implicit text-decoration (underline / line-through) via HTML UA semantics |
| `<i>` / `<em>` | Italic — synthetic via `Tm` matrix skew when font lacks italic variant (G23f — 8.14) |
| `<b>` / `<strong>` | Bold — synthetic via PDF `2 Tr` (text-render-mode fill+stroke) when font lacks bold variant (G23f — 8.14) |
| `rem` units | Resolved relative to root `font-size` (default 16px) |
| `font-size` (px, pt, rem) | Absolute and relative sizes; CSS spec `px → pt × 0.75` |
| `font-weight: bold` / numeric (700+) | Selects bold font variant or falls back to synthetic bold |
| `font-style: italic` / `oblique` | Selects italic font variant or falls back to synthetic italic |
| `font-family` cascade | `font-family: "Times New Roman", serif` cascade resolved through bundled-font fallback |
| `text-align: left / center / right / justify` | Paragraph alignment |
| `text-decoration: underline / line-through` | Per-run text decoration |
| Word/letter break around tokens | Best-effort UAX-14 line breaking |

**Class-rule cascade:**
- `<style>` blocks parse class rules into a lookup table.
- Both direct class selectors (`.cls { ... }`) and descendant selectors (`.parent th { ... }`, `.parent > td { ... }`) are honoured (G23c — 8.14).
- Inline `style="..."` attribute parsed as last-resort fallback when `GetComputedStyle` throws for properties AngleSharp cannot evaluate without a render device (G23a — 8.14).
- HTML5 §15.3 UA stylesheet display mapping applied for `table` / `tbody` / `tr` / `td` / `th` / `caption` / etc. so missing computed-display does not silently downgrade structural elements to `block` (G14 — 8.12).

### 3.5 Block / Decorative Features

| Feature | Notes |
|---|---|
| `background-color` | Any CSS named/hex/rgb/rgba color on any block |
| `background-image: url("data:...")` | Base64 data-URI images (RGB PNG or JPEG) |
| `border` shorthand + per-side `border-{top,right,bottom,left}-{width,style,color}` | Full border model |
| `padding` / `margin` (px, %, rem) | Block spacing |
| `width` / `height` (px, %, auto) | Explicit and auto sizing |
| `max-width` / `min-width` | Clamps applied after intrinsic / explicit width resolution |
| `<hr>` | Filled rectangle at `border-top-width` × content-width |
| Page-level page numbers, breaks | Automatic pagination at content overflow (G8 — 8.9) |

### 3.6 Font Mapping (Bundled Liberation Fonts)

| CSS Family Name(s) | Maps To | Variants |
|---|---|---|
| `serif`, `Times New Roman`, `Times`, `Georgia` | Liberation Serif | Regular / Bold / Italic / BoldItalic |
| `sans-serif`, `Arial`, `Helvetica`, `Verdana` | Liberation Sans | Regular / Bold / Italic / BoldItalic |
| `monospace`, `Courier New`, `Courier` | Liberation Mono | Regular / Bold |

Source: `src/Muonroi.Pdf/Internal/Font/BundledFonts.cs`

**Activation:** Bundled fonts load automatically when the template references the listed family
names. No `@font-face` declaration required. The CID Type0 / Identity-H subset is generated per
render. Synthetic bold/italic kicks in when the requested variant is unavailable; the writer
emits `2 Tr` stroked-fill (bold) or `Tm` skew matrix (italic) so visual weight/slant is preserved
even on incomplete font families.

### 3.7 Image Support

| Feature | Notes |
|---|---|
| `<img src="data:image/png;base64,...">` | 8-bit RGB PNG (color_type=2) |
| `<img src="data:image/jpeg;base64,...">` | JPEG |
| `<img>` with explicit `width` / `height` | Honoured (G16 — 8.12) |
| `<img>` intrinsic size when no CSS width/height | `DecodedImage.Width × 0.75pt/px`, no container stretch (G24 — 8.16) |
| `<img style="max-width:N%">` | Proportional clamp |
| `background-image: url("data:...")` | Same formats as `<img>`; rendered as image XObject behind content |
| Image inside `float` | Renders at correct position with float dimensions (G2 / G9 — 8.8 / 8.11) |
| Image inside `position:absolute` cell | Rendered at cell position (G9 — 8.11) |

**Supported PNG color types** (8-bit samples): RGB (`color_type=2`), palette (`color_type=3`),
RGBA (`color_type=6`, alpha composited onto white), grayscale (`color_type=0`), and
grayscale+alpha (`color_type=4`, alpha composited onto white). Grayscale samples expand to R=G=B.

**Rejected image formats** (raised at decode):
- 16-bit PNG (`bit_depth=16`, any color type) — convert to 8-bit
- Sub-8-bit grayscale PNG (`bit_depth` 1/2/4) — convert to 8-bit
- GIF / WebP / SVG / BMP / TIFF — no decoder in v1
- External URLs (`http(s)://`, `file://`, relative paths) — engine never fetches at render time

### 3.8 Page Media

| Feature | Notes |
|---|---|
| `@page { size: A4 portrait/landscape }` | Page size from template `@page` rule |
| `@page { margin: ... }` | Page margins from `@page` rule |
| `PdfRenderOptions.PageSize` + `Orientation` | Caller override |
| Supported size keywords | `A4`, `A5`, `A6`, `Letter`, `Legal` |
| Multi-page output | Automatic page breaks at content overflow |

---

## 4. Fail-Loud Reject List

The policy rejects the document **before** any rendering when these features are present.
Rejection raises `PdfPolicyViolationException` with the listed violation ID.

| Violation ID | Feature | Alternative |
|---|---|---|
| `forbidden.display.flex` | `display: flex` / `inline-flex` | `display: block` or table layout |
| `forbidden.display.grid` | `display: grid` / `inline-grid` | `display: table` |
| `forbidden.position.fixed` | `position: fixed` | `position: absolute` in `position: relative` container |
| `forbidden.position.sticky` | `position: sticky` | `position: static` |
| `forbidden.transform.geometric` | `transform: ...` (any) | Remove |
| `forbidden.background.gradient` | `background: linear-gradient(...)` and variants | `background-color: <solid>` |
| `forbidden.css-animation` | `@keyframes` / CSS animations | Remove |
| `forbidden.css-transition` | `transition: ...` | Remove |
| `forbidden.import.external` | External `@import` URL | Inline the stylesheet |
| `forbidden.script-element` | `<script>` elements | Remove |
| `forbidden.link.scheme` | `<a href="javascript:..">` / `file:` URLs | `http` / `https` / `mailto` / relative only |

Source: `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs`

---

## 5. Silently Ignored Features

These features are NOT rejected (no violation ID) but produce no visual effect. They will not
break a render but will not match a CSS-conformant browser visually:

| Feature | Behaviour |
|---|---|
| `box-shadow` | Ignored |
| `border-radius` | Ignored (sharp corners always rendered) |
| `outline` / `outline-*` | Ignored |
| `text-shadow` | Ignored |
| `text-decoration: overline` | Ignored (underline and line-through are honoured) |
| `font-variant` | Ignored |
| `letter-spacing` / `word-spacing` | Ignored |
| `calc()` expressions | Not evaluated; property falls back to `0` or inherited value |
| CSS custom properties (`--var`) | Not resolved |
| CSS counters | Not implemented |
| `:hover` / `:focus` / `:active` rules | Ignored (no interactive context in PDF) |
| `@media` other than `print` / `all` | Other media-query branches discarded |
| `@supports` | Not honoured; only the unconditional rules apply |

If a future template requires any of these and the corpus expands to use it, file a gap against
this profile and a new minor profile version will be issued.

---

## 6. Security Constraints

The renderer enforces these unconditionally — they are not overridable by callers:

- **SEC-02 (writer):** Output PDF contains no `/JavaScript`, `/Launch`, `/OpenAction`, or `/EmbeddedFile` actions. URI link annotations from `<a href>` ARE permitted.
- **External resource fetching:** Engine never makes outbound network calls. All images and fonts must be inlined as `data:` URIs.
- **External `@import` blocked:** `@import "https://..."` and `@import "file:..."` raise `forbidden.import.external`.
- **Script execution:** `<script>` elements raise `forbidden.script-element`.
- **Link scheme allowlist:** `<a href>` accepts `http`, `https`, `mailto`, and relative URLs only. Other schemes (`javascript:`, `file:`, etc.) are stripped, the anchor renders as plain text.
- **Pure-managed implementation:** No native deps (no Chromium, no wkhtmltopdf, no QuestPDF native). All decoding (PNG, JPEG, fonts) is pure C#.

---

## 7. Template Format Contract (Phase 9 Designer Seam)

This section defines the interface between any external template-authoring tool (notably the
Phase 9 Designer) and the v1 rendering engine.

### 7.1 Input Format

- **Type:** UTF-8 HTML string
- **State:** Fully filled — no `{{key}}` placeholder tokens remain when passed to `IMPdfService.RenderAsync(...)`. Placeholder expansion is the caller's responsibility.
- **Loop / conditional constructs:** Not interpreted. Callers expand loops to flat HTML before rendering.

### 7.2 Page Size Declaration

Templates SHOULD declare page size via CSS:
```css
@page { size: A4 landscape; margin: 5mm 5mm 5mm 5mm; }
```

Supported size keywords: `A4`, `A5`, `A6`, `Letter`, `Legal`. Orientation: `portrait` / `landscape`.

If `@page { size }` is absent, the caller supplies `PdfRenderOptions.PageSize` + `Orientation`.

### 7.3 Image Format

- **Inline only:** Base64 data URIs in `<img src>` or `background-image: url("data:...")`.
- **Allowed:** `data:image/png;base64,...` (RGB PNG), `data:image/jpeg;base64,...`.
- **Prohibited:** External URLs (`http(s)://`, `file://`, relative paths). The engine never fetches.
- **PNG type:** 8-bit RGB (`color_type=2`) only. RGBA / palette / grayscale rejected.

### 7.4 Font Declaration

- **Preferred:** Rely on bundled Liberation fallback — no `@font-face` needed. Reference any standard family name (`serif`, `"Times New Roman"`, `Arial`, etc.) and the engine maps to the bundled font.
- **Custom fonts:** Templates may include `@font-face { src: url("data:font/truetype;base64,...") }` declarations. External font URLs are rejected (SEC).
- **Synthetic styles:** Bold and italic fall back to synthetic stroke/skew when the variant is missing — templates do not need to bundle bold/italic font files unless exact typographic fidelity is required.

### 7.5 Table Structure Constraints

- Max nesting depth: 1 (no `<table>` inside `<td>`).
- Border model: `border-collapse: collapse` recommended for shared-border print layouts.
- `colspan` / `rowspan` supported.
- `<col>` and `<colgroup>` recognised for column width hints in `table-layout: fixed`.

### 7.6 Template Placeholder Format (caller convention)

The engine itself does NOT process placeholders. By convention, callers using the test harness
and the Phase 9 Designer pre-process tokens before render:

- **Syntax:** `{{key}}` — double curly braces. Spaces inside braces (`{{ key }}`) should be normalised by the caller.
- **Image placeholders:** Tokens for embedded images (`{{logo}}`, `{{barcode}}`) substitute raw base64 strings into pre-wrapped `<img src="data:image/png;base64,{{logo}}">` markup.
- **Text placeholders:** Substitute UTF-8 strings; HTML-escape if untrusted.

---

## 8. Layout IR Seam (Phase 9 Note)

The engine produces a `PositionedPageList` — the canonical intermediate representation between
CSS parsing and PDF emission. This is the seam for any future visual renderer, designer
preview, or alternate output target.

| Type | Location | Role |
|---|---|---|
| `PositionedPageList` | `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` | Root IR: list of pages + embedded font metadata |
| `PositionedPage` | `src/Muonroi.Pdf/Internal/Layout/PositionedPage.cs` | One page: `Elements` + `LinkAnnotations` |
| `PositionedElement` | `src/Muonroi.Pdf/Internal/Layout/PositionedElement.cs` | Leaf: `Source` (BoxNode) + `Position` (Rect) |
| `BoxNode` | `src/Muonroi.Pdf/Internal/Layout/Boxes/BoxNode.cs` | Abstract box tree node |
| `InlineBox` | `src/Muonroi.Pdf/Internal/Layout/Boxes/InlineBox.cs` | Text run + font/size/color/decoration |
| `BlockBox` | `src/Muonroi.Pdf/Internal/Layout/Boxes/BlockBox.cs` | Block container |
| `ReplacedBox` | `src/Muonroi.Pdf/Internal/Layout/Boxes/ReplacedBox.cs` | Image / replaced element with `NaturalWidth/Height` |
| `TableBox` / `TableCellBox` | `src/Muonroi.Pdf/Internal/Layout/Boxes/` | Table model |

**Seam contract for Phase 9:**

1. The Designer emits HTML conforming to Profile v1 (§3) — no flex / grid / animation / `position:fixed`.
2. The engine consumes the HTML, produces `PositionedPageList`, and the writer emits the PDF.
3. The Designer does NOT need to understand the IR — it only needs to conform to the CSS profile.
4. **Future opt-in:** If flex / grid layout is later needed, a `FlexLayoutMapper` or `GridLayoutMapper` can target the same `PositionedPageList` IR. The IR shape is stable.

---

## 9. Validation Status

**Run date:** 2026-05-29 (Phase 8.16 Wave C audit)
**Harness:** `tests/Muonroi.Pdf.Tests/Diagnostic/TemplateImageAudit.cs`
**Template directory:** `D:\Data\Template\Htmls\PreviewRegistion`
**Audit report:** `.planning/phases/08.16-image-polish/AUDIT.md`

| Template | Pages | Render | Visual |
|---|---|---|---|
| BNTT | 1 | OK | MATCH |
| CAPR_E | 1 | OK | MATCH |
| CHNG_E | 1 | OK | MATCH |
| CHNG_F | 1 | OK | MATCH (modulo audit stub size) |
| CRCD_E | 1 | OK | MATCH |
| CSLA_E | 1 | OK | MATCH |
| CSLA_F | 1 | OK | MATCH (modulo audit stub size) |
| GTHA_F | 1 | OK | MATCH (modulo audit stub size) |
| GTND_F | 1 | OK | MATCH |
| HANG_E | 1 | OK | MATCH |
| HANG_F | 1 | OK | MATCH |
| HBCX_F | 1 | OK | MATCH (modulo audit stub size) |
| HBL | 1 | OK | MATCH |
| HBND_F | 1 | OK | MATCH |
| HSLA_E | 1 | OK | MATCH (modulo audit stub size) |
| HSLA_F | 1 | OK | MATCH (modulo audit stub size) |
| NHAR_E | 1 | OK | MATCH |

**Result: 17 / 17 templates render to valid PDF and visually match Chrome reference within the
declared profile scope.** Test suite: 447/447 green.

The Phase 8.7 systemic FONT-GID-MAP bug recorded in the historical capability contract was
resolved in subsequent phases; the bundled-font fallback now produces a complete cp→newGid map
without requiring a caller-supplied `@font-face` declaration.

---

## 10. Known Gaps Out of Scope

| Gap | Description | Out-of-scope reason |
|---|---|---|
| Full CSS 2.1 §17.6.2 border-conflict resolution | Complex precedence for adjacent cell borders | Corpus uses uniform `border-collapse: collapse`; conflict resolution not needed |
| Inline text wrapping around floats | Text reflow around float boxes | Corpus only uses floats for side-by-side blocks |
| Nested tables (depth > 1) | Table inside `<td>` | Corpus depth = 1; layout accuracy not guaranteed for deeper nesting |
| RGBA / palette / grayscale PNG | Non-RGB PNG channel layouts | Corpus uses RGB only; rejected by `PureImageDecoder` |
| GIF / WebP / SVG / BMP / TIFF | Other image formats | Out of v1 decoder scope |
| `calc()` | Math expressions in property values | Not parsed |
| CSS custom properties (`--var`) | CSS variable resolution | Not in corpus |
| `box-shadow` / `border-radius` / `text-shadow` | Decorative effects | Silently ignored |
| `font-variant` / `letter-spacing` / `word-spacing` | Advanced typography | Silently ignored |
| External font URLs | `@font-face src: url(http(s)://)` | SEC: no outbound fetch |
| SVG content | Inline `<svg>` or SVG data URI | Out of v1 scope |
| JavaScript | `<script>` elements | Rejected by policy |
| Form input rendering (`<input>`, `<select>`, etc.) | Form controls | Deferred — zero corpus exposure (G4 / G5) |
| `vertical-align` edge cases in multi-line cells | Rare baseline anomalies | LOW priority (G6) |

---

## 11. Versioning

This profile is `v1.0`. Any change to §3 (Supported), §4 (Reject List), or §7 (Template Format)
that could break existing templates produces a new major profile version (`v2.0`). Additions
that strictly expand the supported subset without rejecting previously-valid templates produce
a minor revision (`v1.x`).

Changes are tracked in `CHANGELOG.md` under a `## Profile v1.x` heading.

---

## 12. References

- `.planning/ROADMAP.md` — overall project roadmap
- `.planning/GAPS-AND-DEBT.md` — gap inventory across phases
- `.planning/phases/08.7-legacy-print-html/CAPABILITY-CONTRACT.md` — historical (Phase 8.7) draft, superseded by this document
- `.planning/phases/08.16-image-polish/AUDIT.md` — validation evidence
- `KNOWN-DEVIATIONS.md` — intentional CSS 2.1 deviations enumerated
- `SECURITY.md` — security posture
- `OSS-BOUNDARY.md` — open-core line
- `src/Muonroi.Pdf.Governance/Policies/LegacyPrintPolicy.cs` — policy implementation
- `src/Muonroi.Pdf/Internal/Font/BundledFonts.cs` — bundled-font registry
- CSS 2.1 specification — https://www.w3.org/TR/CSS21/
- HTML Living Standard §15 — Rendering / UA stylesheet
