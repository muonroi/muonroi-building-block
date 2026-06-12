# Known Deviations from CSS 2.1 / W3C Specifications

This document records intentional deviations from the CSS 2.1 specification. Each entry
explains the deviation, its scope, and the rationale. Deviations are accepted for v0.1 and
will be addressed in future phases as noted.

> **TEST-03 conformance framing.** v0.1 does not publish a numeric CSS-property coverage
> percentage. Instead, "conformance to the declared subset" means: (1) the golden corpus
> exercises every non-rejected property in the declared v0.1 subset (block/inline layout,
> tables with `border-collapse:separate`, paged media, fonts, images, Vietnamese diacritics),
> and (2) every intentional behavioural difference from CSS 2.1 / W3C specs is exhaustively
> enumerated below (TEST-04). Properties outside the subset are not "deviations" — they are
> hard-rejected by the strict policy gate (see KD-05-01) and never reach layout.

---

## Phase 3 Deviations (Box Tree + Layout Engine)

### KD-03-01: `@page { size: … }` descriptor ignored

**Specification**: CSS Paged Media §6.1 — `@page { size: A4 landscape }` overrides the
physical page dimensions.

**Actual behavior**: The `@page { size: … }` descriptor is parsed and stored in
`IPageRule.Size` but is **never applied** by the layout engine. Page dimensions are always
taken from `PdfRenderOptions.PageSize` and `PdfRenderOptions.Orientation`.

**Scope**: Phase 3 layout engine (`LayoutEngine.GetPageDimensions`).

**Rationale**: No v0.1 consumer requires CSS-driven page size override. The ROADMAP SC1–SC5
success criteria test margins and content positioning, not `@page { size }`. Adding size
parsing adds complexity without a testable requirement. The feature is deferred to a future
phase when a consumer demands it.

---

### KD-03-02: `orphans` and `widows` properties not implemented

**Specification**: CSS 2.1 §13.3.3 — `orphans` specifies the minimum number of lines in a
block that must be left at the bottom of a page; `widows` specifies the minimum at the top.

**Actual behavior**: The `orphans` and `widows` CSS properties are accepted by the policy gate
but have no effect on pagination. Lines are broken at the natural page boundary without any
minimum-line guarantee.

**Scope**: Phase 3 `PaginationEngine` and `BlockLayoutEngine`.

**Rationale**: Orphan/widow control requires tracking individual line boxes across page
boundaries, which adds significant complexity to the pagination loop. No Phase 3 success
criterion (SC1–SC5) requires this behavior. Deferred to a future phase.

---

### KD-03-03: Two-pass layout — page boundary may shift between passes

**Specification**: CSS Paged Media / counter(pages) — the total-page count reported by
`counter(pages)` should match the final paginated output exactly.

**Actual behavior**: Pass 1 runs with `totalPages = 0` to determine page count. Pass 2 runs
with the pass-1 count substituted into `counter(pages)`. If the text of the resolved count
(e.g., "10" vs "9") changes the rendered width of a header/footer line enough to shift a page
boundary, pass 2 may produce a different page count than pass 1. Pass 2 is authoritative; no
third pass is performed.

**Scope**: `LayoutEngine.Layout` two-pass orchestration.

**Rationale**: A third pass is not implemented because the edge case (multi-digit boundary
shift caused by rendering `counter(pages)` in body content) does not occur in practice when
headers/footers use a single text line. The cost of a third pass is not justified by the
marginal improvement. The deviation is accepted and documented per Decision 4 in
`03-CONTEXT.md`.

---

### KD-03-05: Unicode Line Breaking Algorithm (UAX#14) not implemented

**Specification**: Unicode TR#14 (Unicode Line Breaking Algorithm) defines break opportunities by
character class (BA, BB, ID, AL, SA, etc.) for all Unicode scripts, including CJK, Vietnamese,
and Latin.

**Actual behavior**: `InlineLayoutEngine` splits text using `String.Split` on
`{ ' ', '\t', '\n', '\r', U+200B }`. No character-class-based break opportunities are computed.

**Why this is acceptable for Phase 3**: Vietnamese text is space-delimited (one syllable per
orthographic word); the space-splitting approach produces correct break opportunities for
Latin+Vietnamese mixed text in practice. This is confirmed by the
`VietnamesePlusLatin_MixedText_ProducesOneElementPerSpaceSeparatedToken` test.

**Scope**: Phase 3 `InlineLayoutEngine` (`WordSeparators` field); full UAX#14 is deferred to
Phase 4 when SixLabors.Fonts shaping is integrated.

**Rationale**: UAX#14 requires a Unicode line-breaking table lookup per character. Phase 3 has
no font metrics dependency; adding a UAX#14 implementation without font integration introduces
complexity with no tested consumer. The space-splitting approach is confirmed correct for the
Phase 3 Latin+Vietnamese use case.

---

### KD-03-04: `counter(page)` / `counter(pages)` inside header/footer HTML not resolved recursively

**Specification**: CSS Paged Media — margin-box content (`@page` `top-center`, etc.) may
contain arbitrary CSS counters that are evaluated in the context of each page.

**Actual behavior**: The `PaginationEngine` resolves `counter(page)` and `counter(pages)` in
header/footer HTML via a simple string replacement (`StripTags` → `Replace`). CSS counter
expressions nested inside HTML attributes, CSS `content:` values, or sub-elements within the
header/footer HTML are **not** recursively parsed or resolved.

**Scope**: `PaginationEngine.ApplyHeaderFooter` and `CombineHeaderFooter`.

**Rationale**: Full CSS counter resolution inside margin-box content requires a mini-cascade
and inline layout pass per page, adding substantial complexity. The v0.1 use case (plain-text
"Page X of Y" footer) is fully covered by the string-replacement approach. Full recursive
resolution is deferred to a future phase.

---

## Phase 4 Deviations (Font + Image Pipeline)

### KD-04-01: Font subsetting limited to TrueType `glyf` outlines; CFF/OpenType passed through unsubsetted

**Specification**: A conforming PDF producer may subset any embedded font program, including
CFF/OpenType (`CFF ` table) outlines.

**Actual behavior**: `TrueTypeFontSubsetter` subsets only TrueType `glyf`-based fonts (cmap
Format 4 → GID mapping, composite-glyph closure, rebuilt table directory). Fonts whose outlines
live in a `CFF ` table are passed through **whole** (no glyph removal); only `glyf`-outline
fonts are reduced to used glyphs.

**Scope**: Phase 4 `TrueTypeFontSubsetter`, `EmbeddedFontInfo`.

**Rationale**: CFF subsetting requires a Type 2 charstring interpreter and a separate CFF
INDEX/DICT rewriter — a large surface with no v0.1 consumer (the embedded test face and the
common Vietnamese-covering faces are `glyf`-based). Whole-font pass-through is correct (renders
identically), only larger. CFF subsetting is deferred.

---

### KD-04-02: Image decoding limited to PNG and baseline/progressive JPEG; intrinsic dimensions only

**Specification**: HTML/CSS replaced elements may reference any image format the user agent
supports (GIF, WebP, AVIF, TIFF, BMP, SVG, …), with full color-management.

**Actual behavior**: `PureImageDecoder` recognises only PNG (via IHDR) and JPEG (SOF0/SOF1/SOF2/
SOF3 markers). It reads **dimensions** for layout; it does not perform colour-space conversion,
ICC-profile application, CMYK→RGB transformation, or re-encoding. Any other format throws
`PdfFormatException`.

**Scope**: Phase 4 `PureImageDecoder`, `ImagePipeline`.

**Rationale**: The pure-managed constraint (no `System.Drawing`, no SkiaSharp) makes additional
codecs costly. PNG + JPEG cover the v0.1 document use cases. Additional formats and colour
management are deferred.

---

## Phase 5 Deviations (PDF Writer, Determinism + Security)

### KD-05-01: CSS properties outside the v0.1 subset are policy-rejected, not silently ignored

**Specification**: CSS 2.1 §2.1 — a conforming UA must ignore (not error on) properties and
values it does not support.

**Actual behavior**: The strict policy gate (`DefaultStrictPolicy`) **hard-rejects** documents
that use `display:flex`/`inline-flex`, `display:grid`/`inline-grid`, `float:left|right`,
`position:absolute|fixed|sticky`, `border-collapse:collapse`, CSS animations/transitions,
external `@import`, and `<script>` elements — raising a policy violation rather than ignoring
the property and continuing.

**Scope**: Phase 5 `Muonroi.Pdf.Governance` strict policy.

**Rationale**: A PDF generator that silently dropped layout-defining properties would emit
misleading output (e.g. a flex layout rendered as stacked blocks). Failing loud at the trust
boundary is a deliberate security/correctness choice for v0.1; the rejected set is exactly the
complement of the declared subset.

---

### KD-05-02: No interactive / active PDF features emitted (`/JavaScript`, embedded files, launch actions)

**Specification**: PDF 32000-1 permits `/JavaScript` actions, `/EmbeddedFiles`, `/Launch`,
`/URI`, and other interactive action dictionaries.

**Actual behavior**: The writer emits a hardened static `%PDF-1.7` stream with **no**
`/JavaScript`, no embedded-file streams, no launch/URI actions. This is asserted in the corpus
(`security-hardened-no-js`) and locked by SEC-01/SEC-02.

**Scope**: Phase 5 `PdfSharpCoreWriter`, security golden.

**Rationale**: Active content is an attack surface with no v0.1 consumer. Excluding it is an
intentional security posture, not an oversight.

---

### KD-05-03: `file://` and direct file-path / network resource resolution rejected

**Specification**: A user agent may dereference `file://` URIs and arbitrary URL schemes for
external resources.

**Actual behavior**: The default `IResourceResolver` rejects `file://` URIs (SEC-06); the
rendering engine performs **no** direct file or network IO. All external resources must be
supplied through a caller-provided `IResourceResolver`, and unresolved/`file://` references
raise a security exception rather than reading from disk.

**Scope**: Phase 5 resource-resolution security (SEC-06).

**Rationale**: SSRF / local-file-disclosure prevention. The caller is the only authority that
can grant resource access, via an explicit resolver.

---

### KD-05-04: Producer/timestamp metadata normalized for byte-determinism

**Specification**: PDF documents normally carry `/CreationDate`, `/ModDate`, a producer string,
and a random `/ID` and font-subset prefix — values that legitimately vary per render.

**Actual behavior**: `NormalizeForDeterminism` rewrites the PDF version header to 1.7 and patches
the two known random tokens (font-subset prefix and trailer `/ID`) to fixed values so identical
input yields byte-identical output. Wall-clock creation/modification timestamps are not emitted
as live values.

**Scope**: Phase 5 `PdfSharpCoreWriter.NormalizeForDeterminism`.

**Rationale**: Byte-reproducible output (SC1) is a core v0.1 guarantee enabling the golden-snapshot
suite. Stripping nondeterministic metadata is required for that guarantee and is intentional.

---

## Phase 6 Deviations (DI, Telemetry, Integration)

### KD-06-01: No CSS-driven page-size/feature overrides introduced

**Specification**: n/a — integration phase.

**Actual behavior**: Phase 6 added DI registration, telemetry descriptors, and the
service-integration path only. It introduced **no** new CSS-property support and therefore no
new CSS 2.1 deviation; the subset and all deviations remain exactly as enumerated in Phases 3-5
above. (Recorded explicitly so a reader can confirm Phase 6 added nothing to the deviation set —
TEST-04 completeness.)

**Scope**: Phase 6 `AddPdf` DI wiring, `PdfTelemetryDescriptor`.

**Rationale**: Integration/observability work does not touch the layout or CSS-cascade surface.

---
