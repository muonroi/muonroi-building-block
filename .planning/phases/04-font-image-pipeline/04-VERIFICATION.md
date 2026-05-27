---
phase: 04-font-image-pipeline
verified: 2026-05-27T14:00:00Z
status: human_needed
score: 4/5 must-haves verified
overrides_applied: 0
deferred:
  - truth: "Embedded TTF/OTF in the output PDF contains only glyphs used in the document (subsetting verified by embedded glyph table size)"
    addressed_in: "Phase 5"
    evidence: "Phase 5 goal: PdfSharpCore writer adapter writes positioned boxes to Stream; PIPE-07; EmbeddedFonts on PositionedPageList is the Phase 4 handoff — Phase 5 writes them into the PDF stream"
  - truth: "Any direct file-path or HTTP resolution throws PdfSecurityException"
    addressed_in: "Phase 5"
    evidence: "Phase 5 requirements SEC-06: 'file:// URI scheme rejected by IResourceResolver default implementation'; PdfSecurityException class and ThrowingResourceResolver are Phase 5 deliverables (confirmed: grep of src/ finds zero instances of PdfSecurityException)"
human_verification:
  - test: "Vietnamese diacritic rendering — visual check"
    expected: "A rendered PDF containing 'Tiếng Việt' shows correctly stacked diacritics (e.g. circumflex + acute above 'e' in 'ế') with no replacement glyphs (no boxes, no fallback characters)"
    why_human: "SixLabors.Fonts glyph measurement returns positive widths for precomposed Vietnamese characters (verified programmatically), but correct combining-mark positioning above base glyphs can only be confirmed by inspecting actual PDF output — which requires Phase 5's PDF writer to produce a file"
documentation_gaps:
  - artifact: ".planning/REQUIREMENTS.md"
    issue: "FONT-01 through FONT-06 and IMG-01 through IMG-05 are still unchecked [ ] in the working tree; Phase 4 implementation is complete but the requirements file was not updated to mark them [x]"
    severity: "minor — tracking artifact only, no code impact"
---

# Phase 4: Font + Image Pipeline Verification Report

**Phase Goal:** Fonts are resolved, shaped, and subsetted; images are decoded; Vietnamese diacritics render correctly; all resource limits are enforced
**Verified:** 2026-05-27T14:00:00Z
**Status:** human_needed (re-verification of existing report — findings confirmed)
**Re-verification:** Yes — independent code-level re-verification of all claims in the prior report

---

## Verification Method

Each claim in the existing VERIFICATION.md was independently re-checked against actual file content (not SUMMARY.md). Evidence is quoted from source files with line numbers.

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| 1 | `@font-face` declarations resolve font bytes via `IFontResolver`; subsetting produces a smaller TTF with only used glyphs | VERIFIED | `FontPipeline.cs:34` calls `resolver.ResolveAsync(request, ct)`; `TrueTypeFontSubsetter.cs:12` is a 695-line binary TTF subsetter with cmap→GID mapping, composite glyph closure, and rebuilt table directory; `EmbeddedFontInfo` stored in `PositionedPageList.EmbeddedFonts`; `TtfSubset_SmallerThanOriginal` and `MaxpNumGlyphs_UpdatedInSubset` tests pass (47/47) |
| 2 | Vietnamese text "Tiếng Việt" has correctly stacked diacritics — no replacement glyphs | UNCERTAIN | `SixLaborsTextMetrics.cs:30` uses `TextMeasurer.MeasureAdvance` from SixLabors.Fonts; `VietnamesePrecomposed_CharWidth_Positive` verifies U+1EBF and U+1EB9 return positive widths with real Noto Sans; visual diacritic stacking confirmation requires Phase 5 PDF output |
| 3 | PNG `data:image/png;base64,...` URI decoded inline with no outbound network call | VERIFIED | `ImagePipeline.cs:27-30` routes `data:` URIs through `DataUriDecoder.Decode`; no `HttpClient`/`WebRequest`/`File.Read` anywhere in `src/Muonroi.Pdf` (confirmed by grep); `DataUri_PngBase64_DecodesBytes` passes |
| 4 | External `src` URIs resolved exclusively via `IResourceResolver.ResolveAsync` | VERIFIED (routing; security enforcement deferred) | `ImagePipeline.cs:45` calls `resolver.ResolveAsync(uri, null, ct)` for all non-`data:` URIs; no direct IO in engine; `ExternalSrc_RoutedThroughResolver_NeverDirectNetwork` passes; `PdfSecurityException` class does not exist in Phase 4 (confirmed: grep across all `src/` returns zero matches) — file:// rejection deferred to Phase 5 SEC-06 |
| 5 | Image pixel count exceeding `MaxImagePixels` (25 MP) rejected before layout | VERIFIED | `ImagePipeline.cs:56-61`: `if ((long)decoded.Width * decoded.Height > PdfConfigs.PdfLimits.MaxImagePixels)` throws `PdfInputLimitException("IMG-MAX-PIXELS", ...)` before image enters resolved dict; `MaxImagePixels_Exceeded_ThrowsLimitException` (5001×5000=25,005,000) and `MaxImagePixels_AtBoundary_NoException` (5000×5000=25,000,000) both pass |

**Score:** 4/5 truths verified (SC2 uncertain; SC4 routing verified, security enforcement deferred)

---

## Deferred Items

Items not yet met but explicitly addressed in later milestone phases.

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | "Embedded TTF/OTF in the output PDF contains only glyphs used" | Phase 5 | `PositionedPageList.cs:9` declares `EmbeddedFonts` as the handoff; Phase 5 PIPE-07 writes EmbeddedFonts into PDF stream via PdfSharpCore |
| 2 | "Any direct file-path or HTTP resolution throws PdfSecurityException" | Phase 5 | SEC-06: "file:// URI scheme rejected by IResourceResolver default implementation"; `PdfSecurityException` class absent from codebase (grep confirmed); Phase 5 plan `05-01-PLAN.md` lists it as a deliverable |

---

## Required Artifacts

| Artifact | Status | Key Evidence |
|----------|--------|-------------|
| `src/Muonroi.Pdf.Abstractions/Engine/FontFaceDeclaration.cs` | VERIFIED | `sealed record FontFaceDeclaration(string Family, FontWeight Weight, FontStyle Style)` |
| `src/Muonroi.Pdf.Abstractions/Engine/IStyledDocument.cs` | VERIFIED | `IReadOnlyList<FontFaceDeclaration> FontFaces { get; }` at line 8 |
| `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs` | VERIFIED | `ExtractFontFaces(document)` iterates `ICssStyleSheet.Rules`, casts to `ICssFontFaceRule`, deduplicates; `_fontFaces` populated in constructor line 30 |
| `src/Muonroi.Pdf/Internal/Font/SixLaborsTextMetrics.cs` | VERIFIED | 68-line non-stub; uses `TextMeasurer.MeasureAdvance` and `FontMetrics.HorizontalMetrics` |
| `src/Muonroi.Pdf/Internal/Font/FontPipeline.cs` | VERIFIED | 48-line orchestrator; calls `IFontResolver.ResolveAsync`, enforces `MaxFontFiles` at line 19, builds `FontCollection` |
| `src/Muonroi.Pdf/Internal/Font/TrueTypeFontSubsetter.cs` | VERIFIED | 695-line binary TTF subsetter; CFF passthrough; cmap Format 4 parsing; composite glyph closure; full table directory rebuild with checksums |
| `src/Muonroi.Pdf/Internal/Font/GlyphCollector.cs` | VERIFIED | Traverses `PositionedPageList.Pages`, calls `font.TryGetGlyphs(new CodePoint(ch))` per char, accumulates codepoints per family |
| `src/Muonroi.Pdf/Internal/Font/EmbeddedFontInfo.cs` | VERIFIED | `internal sealed record EmbeddedFontInfo(string Family, FontWeight Weight, FontStyle Style, ReadOnlyMemory<byte> SubsetBytes, IReadOnlySet<int> UsedCodepoints)` |
| `src/Muonroi.Pdf/Internal/Image/DataUriDecoder.cs` | VERIFIED | RFC 2397 parser; extracts MIME type; strips whitespace; `Convert.FromBase64String`; throws `PdfFormatException` on non-base64 image URIs |
| `src/Muonroi.Pdf/Internal/Image/PureImageDecoder.cs` | VERIFIED | Magic-byte detection: PNG IHDR width/height at offsets 16/20; JPEG SOF0/C1/C2/C3 scan; throws `PdfFormatException` on unknown format |
| `src/Muonroi.Pdf/Internal/Image/ImagePipeline.cs` | VERIFIED | Routes `data:` through `DataUriDecoder`, external URIs through `resolver.ResolveAsync`; pixel limit enforced at line 56 |
| `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` | VERIFIED | `EmbeddedFonts` and `Images` with `internal set` at lines 9-10 |
| `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` | VERIFIED | `LayoutAsync` wires `FontPipeline` (line 63), `ImagePipeline` (line 70), `GlyphCollector` (line 98), `TrueTypeFontSubsetter` (line 106); sets `pass2.EmbeddedFonts` (line 116) and `pass2.Images` (line 117) |

---

## Key Link Verification (Wiring)

| From | To | Via | Status |
|------|-----|-----|--------|
| `AngleSharpStyledDocument.FontFaces` | `FontFaceDeclaration[]` | `ICssFontFaceRule` AST iteration | WIRED |
| `LayoutEngine.LayoutAsync` | `FontPipeline.ResolveAsync` | line 63-64 | WIRED |
| `LayoutEngine.LayoutAsync` | `ImagePipeline.ResolveAsync` | line 70-71 | WIRED |
| `LayoutEngine.LayoutAsync` | `TrueTypeFontSubsetter.Subset` | line 106 per font family | WIRED |
| `PositionedPageList.EmbeddedFonts` | Phase 5 PDF writer | `pass2.EmbeddedFonts = embeddedFonts` line 116 | WIRED (handoff) |
| `ImagePipeline` | `IResourceResolver.ResolveAsync` | `resolver.ResolveAsync(uri, null, ct)` line 45 | WIRED |
| `ImagePipeline` | `PdfInputLimitException` | `(long)w * h > MaxImagePixels` → throw line 56 | WIRED |

---

## Requirements Coverage

| Requirement | Status | Evidence |
|-------------|--------|---------|
| FONT-01 | SATISFIED | `FontPipeline.cs:34` calls `resolver.ResolveAsync` per `FontFaceDeclaration`; bytes-only, no URI dereferencing |
| FONT-02 | SATISFIED (partial — PDF embedding Phase 5) | TTF subset in `EmbeddedFontInfo.SubsetBytes`; CFF pass-through; PDF embedding is Phase 5 PIPE-07 |
| FONT-03 | SATISFIED | `TrueTypeFontSubsetter` + `GlyphCollector`; `TtfSubset_SmallerThanOriginal` passes; `MaxpNumGlyphs_UpdatedInSubset` confirms numGlyphs < 100 for 3-char subset |
| FONT-04 | SATISFIED (programmatic) | `SixLaborsTextMetrics` uses SixLabors.Fonts shaping; `VietnamesePrecomposed_CharWidth_Positive` passes with real Noto Sans; visual check deferred to Phase 5 |
| FONT-05 | SATISFIED | SixLabors.Fonts Unicode line-break via `TextMeasurer.MeasureAdvance`; `MixedLatinVietnamese_LineHeight_Positive` passes |
| FONT-06 | SATISFIED | `FontPipeline.cs:19`: `if (fontFaces.Count > PdfConfigs.PdfLimits.MaxFontFiles)` throws `PdfInputLimitException("FONT-MAX-FILES")`; `MaxFontFiles_ExceededBeforeResolve_ThrowsPdfInputLimitException` passes |
| IMG-01 | SATISFIED (routing; PDF embedding Phase 5) | `PureImageDecoder.DecodePng` reads IHDR at offsets 16/20; `Png_ValidIhdr_ReturnsCorrectDimensions` passes |
| IMG-02 | SATISFIED (routing; PDF embedding Phase 5) | `PureImageDecoder.DecodeJpeg` scans SOF0/SOF2/SOF3; `Jpeg_ValidSof0_ReturnsCorrectDimensions` passes |
| IMG-03 | SATISFIED | `DataUriDecoder.Decode` handles RFC 2397; no network call; `DataUri_PngBase64_DecodesBytes` passes |
| IMG-04 | SATISFIED (routing; file:// enforcement Phase 5) | `ImagePipeline` routes all non-`data:` URIs through `resolver.ResolveAsync`; no direct IO; `ExternalSrc_RoutedThroughResolver_NeverDirectNetwork` passes; `PdfSecurityException` for file:// rejection is Phase 5 SEC-06 |
| IMG-05 | SATISFIED | `ImagePipeline.cs:56-61` checks pixel count before inserting into dictionary; `MaxImagePixels_Exceeded_ThrowsLimitException` passes |

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Assessment |
|------|------|---------|----------|-----------|
| `TrueTypeFontSubsetter.cs` | 367, 451, 500, 631, 641, 655 | `return []` | Info | Defensive guard returns when specific optional font tables absent — correct fallback paths in TTF binary parser; not placeholder stubs |

No TBD/FIXME/XXX/HACK/PLACEHOLDER/`throw new NotImplementedException` markers found in any Phase 4 source files (confirmed by grep).

No `HttpClient`, `WebRequest`, `File.Read`, `File.Open`, or `WebClient` calls in `src/Muonroi.Pdf` (confirmed by grep).

No empty `catch {}` blocks (confirmed by grep for `catch \{` pattern).

---

## Behavioral Verification (Test Suite)

```
dotnet test tests/Muonroi.Pdf.Tests/ --no-build
Passed!  - Failed: 0, Passed: 47, Skipped: 0, Total: 47, Duration: 126 ms
```

| Test | Requirement | Result |
|------|------------|--------|
| `TtfSubset_SmallerThanOriginal` | FONT-03 | PASS — subset.Length < original.Length |
| `MaxpNumGlyphs_UpdatedInSubset` | FONT-03 | PASS — numGlyphs < 100 for 3-char subset |
| `CffOtf_PassthroughUnchanged` | FONT-02 | PASS — CFF bytes returned unchanged |
| `TtfSubset_ValidTableDirectory` | FONT-03 | PASS — sfntVersion = 0x00010000, numTables ≤ 50 |
| `UnrecognizedFormat_ThrowsPdfException` | FONT-02 | PASS — throws `PdfFormatException("FONT-FORMAT")` |
| `MaxFontFiles_ExceededBeforeResolve_ThrowsPdfInputLimitException` | FONT-06 | PASS — throws at 33 fonts, resolver never called |
| `MaxFontFiles_AtLimit_NoException` | FONT-06 | PASS — 32 fonts at limit is allowed |
| `NullResolverResult_FontSkipped_NoException` | FONT-01 | PASS — null resolver result skipped gracefully |
| `ValidTtfBytes_FontCollectionBuilt_MetricsNotEstimated` | FONT-01 | PASS — real TTF loaded, GetCharWidth > 0 |
| `VietnamesePrecomposed_CharWidth_Positive` | FONT-04 | PASS — U+1EBF and U+1EB9 widths > 0 with Noto Sans |
| `MixedLatinVietnamese_LineHeight_Positive` | FONT-05 | PASS — lineHeight > 0, ascender > 0 |
| `SurrogateChar_GlyphCollector_Skipped` | FONT-04 | PASS — surrogate chars excluded from codepoint set |
| `Png_ValidIhdr_ReturnsCorrectDimensions` | IMG-01 | PASS — width=100, height=200 from IHDR |
| `Jpeg_ValidSof0_ReturnsCorrectDimensions` | IMG-02 | PASS — width=320, height=240 from SOF0 |
| `Jpeg_ProgressiveSof2_FindsMarker` | IMG-02 | PASS — width=640, height=480 from SOF2 |
| `Png_InvalidMagic_ThrowsPdfException` | IMG-01 | PASS — throws PdfFormatException |
| `Png_TooShort_ThrowsPdfException` | IMG-01 | PASS — throws PdfFormatException |
| `DataUri_PngBase64_DecodesBytes` | IMG-03 | PASS — bytes match original, contentType="image/png" |
| `DataUri_WithWhitespace_StripAndDecode` | IMG-03 | PASS — newlines stripped before decode |
| `DataUri_MissingBase64Flag_ImageType_Throws` | IMG-03 | PASS — throws PdfFormatException("IMG-FORMAT") |
| `ExternalSrc_RoutedThroughResolver_NeverDirectNetwork` | IMG-04 | PASS — resolver.ResolveAsync called exactly once |
| `NullResolverResult_ImageSkipped_EmptyDictionary` | IMG-04 | PASS — null result skipped, dict empty |
| `MaxImagePixels_Exceeded_ThrowsLimitException` | IMG-05 | PASS — throws PdfInputLimitException("IMG-MAX-PIXELS") |
| `MaxImagePixels_AtBoundary_NoException` | IMG-05 | PASS — exactly 25 MP is not over limit |

---

## Documentation Gaps (Non-Blocking)

| Gap | Severity | Description |
|-----|----------|-------------|
| `REQUIREMENTS.md` checkboxes | Minor | FONT-01 through FONT-06 and IMG-01 through IMG-05 remain `[ ]` (unchecked) in the working-tree file. Phase 4 implementation is complete; checkboxes have not been updated to `[x]`. Confirmed via `git diff .planning/REQUIREMENTS.md` — the diff only covers ABST, PIPE, LAYOUT sections; FONT/IMG rows are unchanged at `[ ]`. |

---

## Human Verification Required

### 1. Vietnamese Diacritic Rendering — Visual Check

**Test:** After Phase 5 produces a PDF: render an HTML document containing `<p style="font-family:'Noto Sans'">Tiếng Việt ế ẹ ổ ừ</p>` using a `@font-face` declaration pointing to a Noto Sans TTF.

**Expected:** The rendered PDF shows correct stacked diacritics — circumflex + acute above the base 'e' in 'ế', dot below in 'ẹ', etc. No replacement glyphs (no □ boxes), no missing combining marks.

**Why human:** `SixLaborsTextMetrics.GetCharWidth` returns positive widths for precomposed Vietnamese characters (verified by `VietnamesePrecomposed_CharWidth_Positive`), confirming the font is loaded and glyphs recognized. However, "correctly stacked diacritics — correct combining-mark positions above base glyphs" is a visual assertion about glyph rendering that requires inspecting actual PDF output, which does not exist until Phase 5's PDF writer is complete.

---

## Gaps Summary

No implementation gaps. All Phase 4 deliverables are substantively implemented and wired.

Two success-criterion elements are deferred to Phase 5 by design:

1. **SC1 "embedded in the output PDF"** — `PositionedPageList.EmbeddedFonts` is the Phase 4 handoff; Phase 5 (PIPE-07) embeds them into the PDF stream via PdfSharpCore.
2. **SC4 "throws PdfSecurityException" / file:// rejection** — `PdfSecurityException` class is absent from the entire codebase (grep confirmed); the Phase 4 engine correctly routes all external URIs through `IResourceResolver.ResolveAsync` and never performs direct IO; the file:// enforcement mechanism is a Phase 5 deliverable (SEC-06, `05-01-PLAN.md`).

One human verification item remains (SC2 visual diacritic rendering) blocked on Phase 5 PDF output.

One non-blocking documentation gap: REQUIREMENTS.md FONT/IMG checkboxes not yet updated to `[x]`.

---

_Verified: 2026-05-27T14:00:00Z_
_Verifier: Claude (gsd-verifier) — independent re-verification_
