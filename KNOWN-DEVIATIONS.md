# Known Deviations from CSS 2.1 / W3C Specifications

This document records intentional deviations from the CSS 2.1 specification. Each entry
explains the deviation, its scope, and the rationale. Deviations are accepted for v0.1 and
will be addressed in future phases as noted.

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
