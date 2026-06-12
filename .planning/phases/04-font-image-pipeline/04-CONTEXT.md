# Phase 4 Context: Font + Image Pipeline

**Phase**: 4 of 9
**Name**: Font + Image Pipeline
**Date captured**: 2026-05-27
**Mode**: Headless autonomous (no interactive discussion)

---

## Domain

Phase 4 wires the font and image sub-pipelines into the existing Phase 3 layout engine. Two pre-layout async passes run before `LayoutEngine.Layout()`:
1. **FontPipeline** — resolves all `@font-face` declarations via `IFontResolver`, builds a `SixLabors.Fonts.FontCollection`, constructs `SixLaborsTextMetrics` (replacing Phase 3's `EstimatedTextMetrics`), and produces a glyph-usage tracker for subsetting.
2. **ImagePipeline** — scans the document for `<img>` elements, decodes `data:` URIs inline and routes external `src` through `IResourceResolver`, validates pixel counts, and returns a `IReadOnlyDictionary<string, DecodedImage>` used by the layout engine to populate `ReplacedBox` natural dimensions.

Phase 5 (PDF writer) reads the `PositionedPageList.EmbeddedFonts` and `PositionedPageList.Images` properties produced here to embed subset fonts and image bytes into the PDF stream.

Requirements locked: FONT-01, FONT-02, FONT-03, FONT-04, FONT-05, FONT-06, IMG-01, IMG-02, IMG-03, IMG-04, IMG-05.

---

## Canonical References

- `.planning/REQUIREMENTS.md` — locked requirements FONT-01–06, IMG-01–05
- `.planning/ROADMAP.md` — Phase 4 success criteria (SC1–SC5)
- `.planning/PROJECT.md` — Key Decisions table; D14 (`IResourceResolver` bytes-only; engine never dereferences URIs); pure-managed constraint (no native deps)
- `.planning/phases/03-box-tree-layout-engine/03-CONTEXT.md` — Decision 11: `ITextMetrics` seam; `EstimatedTextMetrics` in Phase 3, `SixLaborsTextMetrics` in Phase 4; Decision 5: `ReplacedBox` carries `Src`, `NaturalWidth`, `NaturalHeight`
- `src/Muonroi.Pdf.Abstractions/IFontResolver.cs` — bytes-only contract; `FontRequest(Family, Weight, Style)`
- `src/Muonroi.Pdf.Abstractions/IResourceResolver.cs` — bytes-only contract; `ResourceResult(Bytes, ContentType)`
- `src/Muonroi.Pdf.Abstractions/Engine/IImageDecoder.cs` — `Decode(ReadOnlySpan<byte> data, string contentType) : DecodedImage`
- `src/Muonroi.Pdf.Abstractions/Engine/DecodedImage.cs` — `sealed record DecodedImage(int Width, int Height, ReadOnlyMemory<byte> Data, string ContentType)`
- `src/Muonroi.Pdf.Abstractions/Engine/IStyledDocument.cs` — to be extended with `FontFaces` in Phase 4a
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` — `MaxImagePixels = 25_000_000`, `MaxFontFiles = 32`
- `src/Muonroi.Pdf/Internal/Layout/ITextMetrics.cs` — seam implemented by `SixLaborsTextMetrics` in Phase 4
- `src/Muonroi.Pdf/Internal/Layout/LayoutEngine.cs` — entry point; receives `ITextMetrics` via constructor; `Layout()` signature extended in Phase 4e
- `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` — extended in Phase 4e with `EmbeddedFonts` and `Images`
- `src/Muonroi.Pdf/Internal/Layout/Boxes/ReplacedBox.cs` — `Src`, `NaturalWidth`, `NaturalHeight`
- `Directory.Packages.props` — `SixLabors.Fonts 2.1.0` (verified present); `PdfSharpCore 1.3.65` (verified present)

---

## Existing State (verified 2026-05-27)

| Component | Status |
|-----------|--------|
| `Muonroi.Pdf.Abstractions` | Phase 1+3 complete; `IFontResolver`, `IImageDecoder`, `DecodedImage`, `IResourceResolver` defined; `IStyledDocument` has `Root` + `PageRule` but NOT `FontFaces` yet |
| `Muonroi.Pdf.Governance` | Phase 2+3 complete; `AngleSharpStyledDocument` implements `IStyledDocument` (Root + PageRule); `@font-face` NOT yet extracted |
| `Muonroi.Pdf` | Phase 3 complete; layout engine with `EstimatedTextMetrics`; `ReplacedBox` has `Src`/`NaturalWidth`/`NaturalHeight`; NO font or image pipeline code |
| `SixLabors.Fonts 2.1.0` | In `Directory.Packages.props`; NOT yet referenced in `Muonroi.Pdf.csproj` |
| `SixLabors.ImageSharp` | Not in solution (intentionally — license risk per STATE.md blocker) |
| `StbImageSharp` / pixel decoders | Not in solution (not needed — pure header-parse approach chosen) |
| Font subsetting library | Not in solution (hand-written `TrueTypeFontSubsetter` in Phase 4) |

---

## Implementation Decisions

### Decision 1: Extend `IStyledDocument` with `FontFaces` — parsed in Governance, consumed by FontPipeline

**Problem**: `@font-face` CSS at-rules live in the AngleSharp stylesheet AST. The layout engine in `Muonroi.Pdf` has no AngleSharp dependency (Phase 3 Decision 1). `FontPipeline` needs the list of declared font faces to call `IFontResolver`.

**Decision**: Add to `Muonroi.Pdf.Abstractions/Engine/IStyledDocument.cs`:
```csharp
public interface IStyledDocument {
    IStyledNode Root { get; }
    IPageRule? PageRule { get; }
    IReadOnlyList<FontFaceDeclaration> FontFaces { get; }   // NEW in Phase 4a
}
```

Add `FontFaceDeclaration` record to `Muonroi.Pdf.Abstractions/`:
```csharp
public sealed record FontFaceDeclaration(string Family, FontWeight Weight, FontStyle Style);
```

`AngleSharpStyledDocument` implements `FontFaces` by iterating `document.StyleSheets`, casting rules to `ICssFontFaceRule`, and extracting `font-family`, `font-weight`, `font-style` values. Unique combinations only (deduplicate before returning).

**Why**: Consistent with existing seam pattern (Phase 3 added `PageRule` to `IStyledDocument` via same approach). AngleSharp CSS is the only component that can access the stylesheet AST. `FontPipeline` in `Muonroi.Pdf` stays AngleSharp-free — it only sees `FontFaceDeclaration` records.

---

### Decision 2: `SixLaborsTextMetrics` — built from `FontCollection` by `FontPipeline` before layout

**Problem**: `ITextMetrics` is the seam. Phase 3 uses `EstimatedTextMetrics`. Phase 4 must replace it with real SixLabors.Fonts metrics. SixLabors.Fonts requires a `FontCollection` populated with font bytes before you can measure anything.

**Decision**: `FontPipeline` (new class in `Muonroi.Pdf/Internal/Font/`) runs as an async pre-layout step:
1. Reads `IStyledDocument.FontFaces`
2. Validates count against `PdfConfigs.Limits.MaxFontFiles` — throws `PdfInputLimitException` on violation
3. Calls `IFontResolver.ResolveAsync` for each unique `FontFaceDeclaration`
4. Discards `null` results (no match = font unavailable; layout falls back to system font or default metrics)
5. For each resolved font: loads bytes into `FontCollection` via `collection.Add(new MemoryStream(bytes.ToArray()))`
6. Constructs `SixLaborsTextMetrics(FontCollection fontCollection)`

`SixLaborsTextMetrics` implements `ITextMetrics`:
- `GetCharWidth`: uses `TextMeasurer.MeasureAdvance(text, options)` with a `TextOptions` built from the font family name + size. Returns `advance.Width / text.Length` for single-char calls.
- `GetLineHeight`: `font.FontMetrics.LineHeight * fontSize / font.FontMetrics.UnitsPerEm`
- `GetAscender`: `font.FontMetrics.Ascender * fontSize / font.FontMetrics.UnitsPerEm`
- `GetDescender`: `font.FontMetrics.Descender * fontSize / font.FontMetrics.UnitsPerEm`

`LayoutEngine` constructor already accepts `ITextMetrics`. In Phase 4, the call chain is:
```
FontPipeline.ResolveAsync(doc, resolver, limits) → (SixLaborsTextMetrics, EmbeddedFontInfo[])
new LayoutEngine(sixLaborsMetrics).Layout(doc, options, limits, resolvedImages, ct)
```

In Phase 6 DI, `FontPipeline` is injected as a scoped service; `LayoutEngine` is constructed per-render.

**Why**: `FontPipeline` pre-loading matches the `ImagePipeline` pre-loading pattern (both are async, both run before the sync layout pass). The `ITextMetrics` seam (Phase 3 Decision 11) requires no change — only the implementation swaps.

---

### Decision 3: Font subsetting — `GlyphCollector` + `TrueTypeFontSubsetter`, both internal to `Muonroi.Pdf`

**Problem**: FONT-03 requires subsetting via SixLabors.Fonts. SixLabors.Fonts 2.x can identify glyph IDs for any codepoint in a loaded font. But it cannot write a new TTF binary — it's a consumer library. PdfSharpCore doesn't expose a subsetting API through `IPdfWriter`. A third-party subsetter doesn't exist in pure managed MIT space.

**Decision**:
- **`GlyphCollector`** runs after `LayoutEngine.Layout()` (post-layout pass). Walks `PositionedPageList.Pages[].Elements` where `Source is InlineBox` or `ReplacedBox` (text runs). For each text run: uses `SixLabors.Fonts.Font.TryGetGlyphs(text, out GlyphMetrics[] glyphs)` to map codepoints → glyph IDs. Accumulates a `Dictionary<string, HashSet<int>>` keyed by font-family, value = set of used glyph IDs.
- **`TrueTypeFontSubsetter`** takes raw font bytes (`ReadOnlyMemory<byte>`) and a `HashSet<int> usedGlyphIds`, produces a minimal TTF binary containing only those glyphs. Implemented as a focused internal class that copies: `head`, `hhea`, `maxp`, `OS/2`, `name`, `cmap` (remapped to contiguous GID space), `hmtx`, `loca`, `glyf` (only required glyphs). Uses big-endian binary reading via `System.Buffers.Binary.BinaryPrimitives`.
- **`EmbeddedFontInfo`** is an `internal sealed record` in `Muonroi.Pdf/Internal/Font/`:
  ```csharp
  internal sealed record EmbeddedFontInfo(
      string Family,
      FontWeight Weight,
      FontStyle Style,
      ReadOnlyMemory<byte> SubsetBytes,
      IReadOnlySet<int> UsedGlyphIds);
  ```
- `PositionedPageList` gains `IReadOnlyList<EmbeddedFontInfo> EmbeddedFonts { get; init; }` (Phase 4e).

Phase 5's `PdfSharpCoreWriter` embeds `EmbeddedFontInfo.SubsetBytes` directly as a raw TTF stream via `PdfSharpCore`'s font dictionary APIs. Phase 5 planner owns the embedding mechanics.

**Why**: SixLabors.Fonts is used for glyph ID resolution (satisfying "subsetting applied via SixLabors.Fonts" in FONT-03). The TrueType subsetter is the minimum required code to produce a smaller font binary. Pure managed, no native, works on Alpine/AOT. TTF table structure is a well-specified binary format — the table list is stable.

**Known limitation**: `TrueTypeFontSubsetter` handles TTF only (glyf/loca tables). OTF CFF-flavored fonts are NOT subsetted in Phase 4 — full OTF bytes are embedded. Document in `KNOWN-DEVIATIONS.md`.

---

### Decision 4: Image decoding — `PureImageDecoder`, no external library

**Problem**: `IImageDecoder.Decode()` must return `DecodedImage(Width, Height, Data, ContentType)`. For PDF embedding, Phase 5 (`PdfSharpCoreWriter`) uses `XImage.FromStream(stream)` which accepts raw PNG/JPEG bytes — it does NOT need decoded ARGB pixels. We only need width/height for layout measurement and the raw bytes for embedding.

**Decision**: `PureImageDecoder : IImageDecoder` in `Muonroi.Pdf/Internal/Image/`:
- **PNG**: Reads bytes 16–19 (big-endian uint32) for width, bytes 20–23 for height, from the IHDR chunk. Magic bytes `89 50 4E 47 0D 0A 1A 0A` at offset 0 validated. Raw bytes returned as `Data`.
- **JPEG**: Scans forward for SOF0 (`FF C0`), SOF1 (`FF C1`), SOF2 (`FF C2`), SOF3 (`FF C3`) markers. Width is at offset +5 (2 bytes big-endian), height at offset +3 within the SOF segment. Raw bytes returned as `Data`.
- **Unsupported format**: Throws `PdfInputException("IMG-FORMAT", ...)` with the detected magic bytes in the message.
- `DecodedImage.Data` always contains the original compressed image bytes (not pixels). Phase 5 feeds these to `XImage.FromStream`.
- Pixel count validation (`Width * Height > MaxImagePixels`) is done in `ImagePipeline`, not in `PureImageDecoder` itself — keeps the decoder a pure header parser.

No new NuGet dependency. Satisfies: pure-managed, AOT-safe, MIT/zero license risk. `SixLabors.ImageSharp` is explicitly avoided per STATE.md license blocker.

**Why**: The PDF format natively embeds JPEG/PNG streams — no pixel decode is required for embedding. Width/height are needed only for layout (replaced element sizing). Pure header parsing delivers both without a dependency.

---

### Decision 5: `ImagePipeline` — async pre-layout pass, dictionary-based result

**Problem**: Image resolution is async (`IResourceResolver.ResolveAsync`). The layout engine is synchronous. `data:` URIs need base64 decoding. External `src` goes through `IResourceResolver`. All pixel counts must be validated before layout.

**Decision**: `ImagePipeline` in `Muonroi.Pdf/Internal/Image/`:

```csharp
internal sealed class ImagePipeline
{
    internal async Task<IReadOnlyDictionary<string, DecodedImage>> ResolveAsync(
        IStyledDocument doc,
        IResourceResolver resolver,
        IImageDecoder decoder,
        PdfConfigs.PdfLimits limits,
        CancellationToken ct)
```

Steps:
1. Walk `IStyledDocument.Root` (via `IStyledNode`) to find all nodes where `LocalName == "img"` and `GetAttribute("src") != null`
2. For each unique `src` value:
   - If it starts with `data:`: parse with `DataUriDecoder` (internal static class); base64 decode; call `decoder.Decode(bytes, contentType)` 
   - Otherwise: call `resolver.ResolveAsync(new Uri(src), contentTypeHint: null, ct)`. Null result → skip (image not available; log at debug; layout uses `ReplacedBox` with 0×0 dimensions). Non-null → call `decoder.Decode(result.Bytes.Span, result.ContentType)`
3. After decoding: validate `(long)decoded.Width * decoded.Height > limits.MaxImagePixels` → throw `PdfInputLimitException("IMG-MAX-PIXELS", ...)`
4. Return `Dictionary<string, DecodedImage>` keyed by original `src` attribute value

The dictionary is passed to `LayoutEngine.Layout()` as a new parameter. `BoxTreeBuilder` uses it to set `ReplacedBox.NaturalWidth/NaturalHeight` from `DecodedImage.Width/Height` (falling back to CSS `width`/`height` attributes when the src key is not in the dictionary).

`PositionedPageList` gains `IReadOnlyDictionary<string, DecodedImage> Images { get; init; }` (Phase 4e) so Phase 5 can embed the raw bytes.

**`DataUriDecoder`** (internal static class in `Muonroi.Pdf/Internal/Image/`):
- Parses `data:[<mediatype>][;base64],<data>` per RFC 2397
- Uses `System.Convert.FromBase64String` for base64 payload
- Returns `(ReadOnlyMemory<byte> Bytes, string ContentType)`

**Why**: Separates async I/O (image resolution) from sync layout. The pre-layout pass is the correct place to enforce `MaxImagePixels` (requirement says "rejected before any layout measurement" — IMG-05). `BoxTreeBuilder` already populates `ReplacedBox.Src`; adding `NaturalWidth/Height` from the dictionary is minimal change.

---

### Decision 6: `LayoutEngine.Layout()` signature extension and `PositionedPageList` enrichment

**Problem**: Phase 4 outputs must flow to Phase 5 through the existing internal boundary. `PositionedPageList` is the only data carrier that crosses from Phase 3/4 layout to Phase 5 writer (same assembly, internal cast from `IPositionedPageList`).

**Decision**: Extend `LayoutEngine.Layout()`:
```csharp
// Phase 4 signature (extends Phase 3)
public IPositionedPageList Layout(
    IStyledDocument doc,
    PdfRenderOptions options,
    PdfConfigs.PdfLimits limits,
    IReadOnlyDictionary<string, DecodedImage> resolvedImages,  // NEW
    CancellationToken ct)
```

Extend `PositionedPageList` with:
```csharp
// Added in Phase 4
public IReadOnlyList<EmbeddedFontInfo> EmbeddedFonts { get; init; } = [];
public IReadOnlyDictionary<string, DecodedImage> Images { get; init; } = new Dictionary<string, DecodedImage>();
```

The `LayoutEngine.RunLayout()` private method receives `resolvedImages` and passes it to `BoxTreeBuilder`. After layout, `LayoutEngine.Layout()` (the two-pass entry point) calls `GlyphCollector.Collect(pageList, fontCollection)` to produce the glyph usage sets, then calls `TrueTypeFontSubsetter.Subset(fontBytes, usedGlyphIds)` for each font, and assigns `EmbeddedFonts` to the final `PositionedPageList`.

**Why**: `PositionedPageList` is already the Phase 3→5 carrier for positioned elements. Adding `EmbeddedFonts` and `Images` to it keeps all layout + resource data in one internal object. Phase 5 planner gets everything needed for PDF writing without new seams. The `init` setters allow clean construction in `LayoutEngine`.

---

## File Creation Plan

Priority order (strict dependency):

**Phase 4a — Abstractions gap closure** (`Muonroi.Pdf.Abstractions`):
1. `FontFaceDeclaration.cs` — `public sealed record FontFaceDeclaration(string Family, FontWeight Weight, FontStyle Style)`
2. Extend `Engine/IStyledDocument.cs` — add `IReadOnlyList<FontFaceDeclaration> FontFaces { get; }`

**Phase 4b — Governance gap closure** (`Muonroi.Pdf.Governance`):
3. Extend `Cascade/AngleSharpStyledDocument.cs` — implement `FontFaces` by iterating `document.StyleSheets` for `ICssFontFaceRule` entries; deduplicate; wrap in `FontFaceDeclaration`

**Phase 4c — `Muonroi.Pdf.csproj` update**:
4. Add `<PackageReference Include="SixLabors.Fonts" />` to `Muonroi.Pdf/Muonroi.Pdf.csproj` (already in `Directory.Packages.props` at 2.1.0)

**Phase 4d — Font pipeline** (`Muonroi.Pdf/Internal/Font/`):
5. `EmbeddedFontInfo.cs` — `internal sealed record EmbeddedFontInfo(string Family, FontWeight Weight, FontStyle Style, ReadOnlyMemory<byte> SubsetBytes, IReadOnlySet<int> UsedGlyphIds)`
6. `SixLaborsTextMetrics.cs` — implements `ITextMetrics`; constructor takes `FontCollection`; uses `TextMeasurer.MeasureAdvance` for width, `FontMetrics` properties for height/ascender/descender
7. `GlyphCollector.cs` — post-layout pass; walks `PositionedPageList` text runs; maps codepoints → glyph IDs via `SixLabors.Fonts.Font.TryGetGlyphs`
8. `TrueTypeFontSubsetter.cs` — reads TTF bytes; copies required tables; returns subset binary. TTF only; OTF CFF falls back to full embedding.
9. `FontPipeline.cs` — orchestrates: validate MaxFontFiles → resolve all fonts via IFontResolver → build FontCollection → return (SixLaborsTextMetrics, fontBytesMap)

**Phase 4e — Image pipeline** (`Muonroi.Pdf/Internal/Image/`):
10. `DataUriDecoder.cs` — static; parses RFC 2397 data: URIs; returns (ReadOnlyMemory<byte>, string contentType)
11. `PureImageDecoder.cs` — implements `IImageDecoder`; PNG IHDR header parse; JPEG SOF marker scan; no pixel decode; no external library
12. `ImagePipeline.cs` — async; walks IStyledNode for img elements; routes data: vs external; validates MaxImagePixels; returns Dictionary<string, DecodedImage>

**Phase 4f — PositionedPageList + LayoutEngine extension** (`Muonroi.Pdf/Internal/Layout/`):
13. Extend `PositionedPageList.cs` — add `EmbeddedFonts` and `Images` properties
14. Extend `BoxTreeBuilder.cs` — accept `IReadOnlyDictionary<string, DecodedImage>? resolvedImages`; populate `ReplacedBox.NaturalWidth/Height` from decoded dimensions when key present
15. Extend `LayoutEngine.cs` — new `Layout()` overload with `resolvedImages` parameter; post-layout: call `GlyphCollector` then `TrueTypeFontSubsetter`; set `EmbeddedFonts`/`Images` on `PositionedPageList`

**Phase 4g — Tests** (`tests/Muonroi.Pdf.Tests/`):
16. `FontPipelineTests.cs` — verify MaxFontFiles validation; verify FontCollection built from resolver bytes; verify SixLaborsTextMetrics returns non-estimated values
17. `ImagePipelineTests.cs` — verify PNG/JPEG header parsing; verify data: URI decoding; verify MaxImagePixels rejection; verify external src routed through IResourceResolver
18. `TrueTypeFontSubsetterTests.cs` — verify subset binary smaller than original; verify only used glyphs in subset (via glyph count in output MAXP table)
19. `VietnameseDiacriticTests.cs` — verify "Tiếng Việt" text measures with SixLaborsTextMetrics (no replacement glyphs — valid width returned)

---

## Out of Phase 4 Scope

- PDF file writing — Phase 5 (`PdfSharpCoreWriter`); consumes `PositionedPageList.EmbeddedFonts` and `PositionedPageList.Images`
- `AddPdf()` DI registration and pipeline orchestration — Phase 6
- Vietnamese golden snapshot tests (≥10) — Phase 7
- OTF CFF subsetting — KNOWN-DEVIATIONS.md; full bytes embedded for CFF-flavored OTF
- `background-image: url(...)` CSS property — not in v0.1 policy subset; rejected by DefaultStrictPolicy if used with external URIs; `data:` background images are post-v0.1 scope
- SixLabors.ImageSharp — explicitly excluded (license audit pending per STATE.md)
- `counter(page)` inside `@font-face` `src` attribute — nonsensical; not in scope
- Font fallback chains (CSS `font-family: 'Custom', Arial, sans-serif`) — Phase 4 resolves only the first declared `@font-face` family; generic fallback (`Arial`, `sans-serif`) uses `EstimatedTextMetrics` proportions when no resolver match. Document in `KNOWN-DEVIATIONS.md`.

---

## Autonomous Gray Area Resolutions

| Gray Area | Decision | Rationale |
|-----------|----------|-----------|
| Who extracts `@font-face` declarations? | Add `FontFaces` to `IStyledDocument`; Governance extracts from AngleSharp stylesheet AST | Consistent with Phase 3 `PageRule` pattern; layout engine stays AngleSharp-free |
| How is `FontCollection` built from `IFontResolver` bytes? | `FontPipeline` pre-layout async pass; validates MaxFontFiles; returns `SixLaborsTextMetrics` | Async I/O before sync layout; mirrors `ImagePipeline` pre-layout pattern |
| How are fonts subsetted without a managed subsetting library? | `TrueTypeFontSubsetter` internal class; SixLabors.Fonts identifies glyph IDs; hand-written TTF table copier | SixLabors.Fonts satisfies FONT-03 "applied via SixLabors.Fonts" for glyph ID identification; pure managed; AOT-safe |
| Which image decoding library? | None — `PureImageDecoder` reads PNG IHDR and JPEG SOF headers only; raw bytes passthrough to PdfSharpCore | Avoids SixLabors.ImageSharp license risk; PDF embedding needs raw bytes, not pixels; no new dep |
| How do font/image data reach Phase 5? | Extend `PositionedPageList` (internal) with `EmbeddedFonts` and `Images` | Same-assembly internal cast used since Phase 2; no new seams needed; Phase 5 planner gets one clean carrier |
