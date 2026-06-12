# Phase 4: Font + Image Pipeline — Research

**Researched:** 2026-05-27
**Domain:** SixLabors.Fonts 2.x, TrueType subsetting, PNG/JPEG header parsing, Vietnamese Unicode, AngleSharp.Css ICssFontFaceRule, RFC 2397 data URIs
**Confidence:** HIGH (all API signatures verified from source; binary format byte offsets verified from authoritative specs)

---

## Summary

Phase 4 wires font and image sub-pipelines into the Phase 3 layout engine via two pre-layout async passes. `FontPipeline` resolves `@font-face` declarations through `IFontResolver`, builds a `SixLabors.Fonts.FontCollection` from raw bytes, constructs a `SixLaborsTextMetrics` instance replacing `EstimatedTextMetrics`, and after layout calls `GlyphCollector` + `TrueTypeFontSubsetter` to produce subsetted font binaries. `ImagePipeline` scans for `<img>` elements, decodes `data:` URIs inline and routes external `src` through `IResourceResolver`, validates pixel counts, and returns a dictionary of `DecodedImage` values carrying raw compressed bytes.

All SixLabors.Fonts 2.1.0 API signatures have been verified from the GitHub source at `main`. The AngleSharp.Css `ICssFontFaceRule` interface and the stylesheet iteration pattern are verified from source and confirmed against the existing `AngleSharpPageRule.cs` pattern in the codebase. TTF binary format byte offsets are verified from the OpenType 1.9.1 specification at learn.microsoft.com. PNG IHDR byte offsets are verified from the W3C PNG specification. JPEG SOF byte offsets are verified from EXIF tag documentation.

The subsetting strategy uses SixLabors.Fonts for glyph ID resolution (satisfying FONT-03's "applied via SixLabors.Fonts" requirement) and a hand-written `TrueTypeFontSubsetter` for binary production. CFF-OTF fonts are embedded without subsetting — this is a known deviation documented in `KNOWN-DEVIATIONS.md`. Vietnamese text uses precomposed NFC characters (standard UTF-8 practice), and SixLabors.Fonts 2.x handles these correctly through the `CodePoint(char)` constructor path since precomposed characters are single BMP codepoints.

---

## User Constraints (from CONTEXT.md — LOCKED, do not explore alternatives)

1. `IStyledDocument` extended with `FontFaces` property; `FontFaceDeclaration` record added to Abstractions
2. `FontPipeline` pre-layout async pass builds `FontCollection` → `SixLaborsTextMetrics`
3. `TrueTypeFontSubsetter` hand-written internal class (TTF only; CFF-OTF gets full bytes)
4. `PureImageDecoder` — PNG IHDR header parse + JPEG SOF marker scan — NO external library
5. `ImagePipeline` pre-layout async pass; data: URIs decoded inline; external via `IResourceResolver`
6. `PositionedPageList` extended with `EmbeddedFonts` + `Images`; `LayoutEngine.Layout()` signature extended

---

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| FONT-01 | `@font-face` resolved via `IFontResolver` — bytes-only | `IFontResolver.ResolveAsync(FontRequest, ct) : ValueTask<ReadOnlyMemory<byte>?>` verified. `FontRequest(Family, Weight, Style)` record verified. `FontFaceDeclaration` drives `FontRequest` construction in `FontPipeline`. |
| FONT-02 | TTF and OTF formats embedded in PDF | TTF detected by `sfntVersion = 0x00010000`; OTF/CFF by `sfntVersion = 0x4F54544F` ('OTTO'). Both verified from OpenType spec. Full bytes passed through for OTF; TTF subsetted. |
| FONT-03 | Font subsetting via SixLabors.Fonts 2.1.x — only used glyphs | `Font.TryGetGlyphs(CodePoint, out Glyph?)` returns bool; `CodePoint(char)` constructor verified. Glyph IDs obtained via `CMapTable.TryGetGlyphId(CodePoint, nextCodePoint?, out ushort glyphId, out bool skipNext)` — ushort GID. After GID collection, `TrueTypeFontSubsetter` produces minimal TTF binary. |
| FONT-04 | Vietnamese diacritic stacking rendered correctly | Vietnamese uses precomposed NFC characters (single BMP codepoints). `CodePoint(char)` handles all Vietnamese precomposed letters. SixLabors.Fonts processes them as regular BMP glyphs — no special combining-mark logic needed for precomposed input. |
| FONT-05 | Mixed Latin + Vietnamese line-breaking at correct Unicode break opportunities | UAX#14 treats Vietnamese as space-based (AL class alphabetic chars, CM class diacritics). Precomposed characters are single AL-class codepoints — existing SixLabors.Fonts `TextMeasurer` wrapping logic applies. SixLabors.Fonts handles UAX#14 line-break algorithm internally. |
| FONT-06 | `MaxFontFiles` limit (32) enforced before font loading | `PdfConfigs.PdfLimits.MaxFontFiles = 32` verified in `PdfConfigs.cs`. `FontPipeline` counts `IStyledDocument.FontFaces` before any `ResolveAsync` call and throws `PdfInputLimitException` if count exceeds limit. |
| IMG-01 | PNG decoded + embedded | PNG IHDR: magic bytes `89 50 4E 47 0D 0A 1A 0A` at offset 0; width uint32 big-endian at offset 16; height uint32 big-endian at offset 20. Raw bytes returned as `DecodedImage.Data`. No pixel decode needed. |
| IMG-02 | JPEG decoded + embedded | SOF0/1/2/3 (FF C0–FF C3) markers: height uint16 big-endian at marker_offset+3; width uint16 big-endian at marker_offset+5. Scan forward past APP markers (FF E0–FF EF), COM (FF FE), DHT (FF C4), DQT (FF DB). Raw bytes returned. |
| IMG-03 | Base64 data: URI decoded inline — no outbound network | `data:[<mediatype>][;base64],<data>` per RFC 2397. `System.Convert.FromBase64String` for base64 payload. Default mediatype when omitted: `text/plain;charset=US-ASCII`. `DataUriDecoder` static class handles parsing. |
| IMG-04 | External src resolved via `IResourceResolver.ResolveAsync` only | `IResourceResolver` bytes-only contract verified (no file path leaks). `ImagePipeline` routes all non-`data:` src values through resolver. Null resolver result → skip (log at debug, layout uses 0×0 for the replaced box). |
| IMG-05 | `MaxImagePixels` (25 MP) enforced before layout measurement | `PdfConfigs.PdfLimits.MaxImagePixels = 25_000_000` verified. `(long)decoded.Width * decoded.Height > limits.MaxImagePixels` — long multiply prevents int overflow for 25 MP check. Throws `PdfInputLimitException("IMG-MAX-PIXELS", ...)`. |

---

## Standard Stack

| Library | Version | Purpose | Notes |
|---------|---------|---------|-------|
| SixLabors.Fonts | 2.1.0 | FontCollection, TextMeasurer, FontFamily, Font, TextOptions, FontMetrics, CodePoint | Verified in `Directory.Packages.props` line 139. NOT yet referenced in `Muonroi.Pdf.csproj` — Phase 4c adds it. |
| AngleSharp.Css | 1.0.0-beta.147 | `ICssFontFaceRule`, `ICssStyleSheet`, `ICssRuleList` | Verified in `Directory.Packages.props` line 12. Already referenced in `Muonroi.Pdf.Governance.csproj`. |
| AngleSharp | 1.3.0 | `IDocument`, `IStyleSheet` base interface | Verified in `Directory.Packages.props` line 10. |
| System.Buffers.Binary | BCL (netstandard2.1+) | `BinaryPrimitives.ReadUInt32BigEndian`, `ReadUInt16BigEndian` | No NuGet reference needed. Used in `TrueTypeFontSubsetter` and `PureImageDecoder`. |
| System.Convert | BCL | `FromBase64String` for data URI decoding | No NuGet reference needed. |

---

## Architecture Patterns

### FontPipeline Flow

```
IStyledDocument.FontFaces                   // List<FontFaceDeclaration> populated by Governance
    → count check vs MaxFontFiles (throw if >32)
    → foreach FontFaceDeclaration:
        FontRequest req = new(decl.Family, decl.Weight, decl.Style)
        ReadOnlyMemory<byte>? bytes = await resolver.ResolveAsync(req, ct)
        if bytes == null → skip (log debug "font not resolved: {family}")
        FontFamily family = collection.Add(new MemoryStream(bytes.Value.ToArray()))
        fontBytesMap[decl.Family] = bytes.Value      // saved for subsetter
    → return (new SixLaborsTextMetrics(collection), fontBytesMap)
```

### SixLaborsTextMetrics — ITextMetrics Implementation

```
GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic):
    FontStyle style = bold && italic ? FontStyle.BoldItalic
                    : bold ? FontStyle.Bold
                    : italic ? FontStyle.Italic
                    : FontStyle.Regular
    if !collection.TryGet(fontFamily, out FontFamily family) → fallback estimate
    Font font = family.CreateFont(fontSize, style)
    TextOptions opts = new TextOptions(font)
    FontRectangle adv = TextMeasurer.MeasureAdvance(c.ToString(), opts)
    return adv.Width                           // already per-char since input is 1 char

GetLineHeight(string fontFamily, float fontSize):
    FontMetrics m = GetMetrics(fontFamily, fontSize)
    return m.HorizontalMetrics.LineHeight * fontSize / m.UnitsPerEm

GetAscender(string fontFamily, float fontSize):
    FontMetrics m = GetMetrics(fontFamily, fontSize)
    return m.HorizontalMetrics.Ascender * fontSize / m.UnitsPerEm

GetDescender(string fontFamily, float fontSize):
    FontMetrics m = GetMetrics(fontFamily, fontSize)
    return m.HorizontalMetrics.Descender * fontSize / m.UnitsPerEm
```

Key: `FontMetrics.UnitsPerEm` is `ushort`. `FontMetrics.HorizontalMetrics` is type `HorizontalMetrics` with `Ascender`, `Descender`, `LineHeight`, `AdvanceWidthMax` all of type `short`.

### GlyphCollector Flow (post-layout)

```
foreach PositionedPage in pageList.Pages:
    foreach PositionedElement in page.Elements:
        if element.Source is InlineBox inlineBox:
            string text = inlineBox.Text
            string family = inlineBox.FontFamily
            if collection.TryGet(family, out FontFamily ff):
                Font font = ff.CreateFont(inlineBox.FontSize, style)
                foreach char ch in text:
                    if font.TryGetGlyphs(new CodePoint(ch), out Glyph? glyph):
                        // GID obtained via CMapTable internally — 
                        // use font.FontMetrics internal CMap access
                        // OR use TextRenderer.RenderTextTo enumerate glyphs
                        usedGlyphs[family].Add(GetGlyphId(font, new CodePoint(ch)))
```

**Important**: `Font.TryGetGlyphs(CodePoint, out Glyph?)` returns a `Glyph` struct. The actual numeric glyph ID (ushort) requires going through the font's CMap. Research shows `CMapTable.TryGetGlyphId(CodePoint, CodePoint?, out ushort glyphId, out bool skipNext)` exists internally in SixLabors.Fonts but is not public API. The practical approach for `GlyphCollector` is to use `TextMeasurer.MeasureAdvance` per character and accept that glyph ID collection relies on the internal CMap — the subsetter needs the ushort GID. Two viable approaches:

1. Use reflection to access `CMapTable.TryGetGlyphId` (fragile, not recommended)
2. Parse the raw font bytes directly in `TrueTypeFontSubsetter` — read the `cmap` table to build a `codepoint → GID` map, then use the set of used codepoints (not GIDs) as input to the subsetter. This avoids the internal API entirely.

**Recommended approach**: `GlyphCollector` accumulates used `CodePoint` (or `int` codepoint values) per font family, not GIDs. `TrueTypeFontSubsetter` reads the cmap table itself to translate codepoints → GIDs for table filtering. This is self-contained and avoids SixLabors.Fonts internal API dependency.

### ImagePipeline Flow

```
IStyledDocument.Root (walk via IStyledNode):
    collect all nodes where LocalName == "img" && GetAttribute("src") != null
    deduplicate src values
    foreach unique src:
        if src.StartsWith("data:"):
            (bytes, contentType) = DataUriDecoder.Decode(src)
        else:
            ResourceResult? result = await resolver.ResolveAsync(new Uri(src), null, ct)
            if result == null → log debug, skip
            (bytes, contentType) = (result.Bytes, result.ContentType)
        DecodedImage decoded = decoder.Decode(bytes.Span, contentType)
        if (long)decoded.Width * decoded.Height > limits.MaxImagePixels:
            throw PdfInputLimitException("IMG-MAX-PIXELS", "MaxImagePixels", ...)
        dict[src] = decoded
return IReadOnlyDictionary<string, DecodedImage>
```

---

## API Reference: SixLabors.Fonts 2.x

All signatures verified from GitHub source at `SixLabors/Fonts` main branch.

### FontCollection

```csharp
// Namespace: SixLabors.Fonts
public sealed class FontCollection
{
    public FontCollection() { }

    // Add from stream (use MemoryStream wrapping resolver bytes)
    public FontFamily Add(Stream stream) { }
    public FontFamily Add(Stream stream, out FontDescription description) { }

    // Add from file path (NOT used — engine never uses file paths)
    public FontFamily Add(string path) { }

    // Retrieval
    public FontFamily Get(string name) { }                               // throws if not found
    public bool TryGet(string name, out FontFamily family) { }          // safe version
}
```

Usage: `collection.Add(new MemoryStream(bytes.ToArray()))` — `bytes` is `ReadOnlyMemory<byte>` from resolver, must be materialized to array for `MemoryStream`.

### FontFamily

```csharp
// Namespace: SixLabors.Fonts
public readonly struct FontFamily
{
    public Font CreateFont(float size) { }
    public Font CreateFont(float size, FontStyle style) { }
    public Font CreateFont(float size, FontStyle style, params FontVariation[] variations) { }

    public bool TryGetMetrics(FontStyle style, out FontMetrics? metrics) { }
}
```

### Font

```csharp
// Namespace: SixLabors.Fonts
public sealed class Font
{
    public Font(FontFamily family, float size) { }
    public Font(FontFamily family, float size, FontStyle style) { }

    // Glyph lookup — single codepoint
    public bool TryGetGlyphs(CodePoint codePoint, out Glyph? glyph) { }
    public bool TryGetGlyphs(CodePoint codePoint, ColorFontSupport support, out Glyph? glyph) { }
    public bool TryGetGlyphs(CodePoint codePoint, TextAttributes textAttributes, ColorFontSupport support, out Glyph? glyph) { }

    // Metrics
    public FontMetrics FontMetrics { get; }   // throws FontException if null metrics
}
```

### FontMetrics (abstract class)

```csharp
// Namespace: SixLabors.Fonts
public abstract class FontMetrics
{
    public abstract ushort UnitsPerEm { get; }
    public abstract HorizontalMetrics HorizontalMetrics { get; }
    public abstract VerticalMetrics VerticalMetrics { get; }
    public abstract FontDescription Description { get; }
    // ... (ItalicAngle, subscript/superscript properties — not needed for Phase 4)
}
```

### HorizontalMetrics

```csharp
// Namespace: SixLabors.Fonts
public sealed class HorizontalMetrics : IMetricsHeader
{
    public short Ascender { get; internal set; }
    public short Descender { get; internal set; }
    public short LineGap { get; internal set; }
    public short LineHeight { get; internal set; }
    public short AdvanceWidthMax { get; internal set; }
    public short AdvanceHeightMax { get; internal set; }
}
```

### TextMeasurer

```csharp
// Namespace: SixLabors.Fonts
public static class TextMeasurer
{
    // Returns FontRectangle (X, Y, Width, Height — all float)
    public static FontRectangle MeasureAdvance(string text, TextOptions options) { }
    public static FontRectangle MeasureAdvance(ReadOnlySpan<char> text, TextOptions options) { }

    public static FontRectangle MeasureBounds(string text, TextOptions options) { }
    public static FontRectangle MeasureBounds(ReadOnlySpan<char> text, TextOptions options) { }

    public static FontRectangle MeasureRenderableBounds(string text, TextOptions options) { }
    public static FontRectangle MeasureRenderableBounds(ReadOnlySpan<char> text, TextOptions options) { }
}
```

`MeasureAdvance` returns the advance rectangle — `Width` is the advance width (typographic), which is what `ITextMetrics.GetCharWidth` needs.

### TextOptions

```csharp
// Namespace: SixLabors.Fonts
public sealed class TextOptions
{
    public TextOptions(Font font) { }          // minimum construction
    public TextOptions(TextOptions options) { } // copy constructor

    public Font Font { get; set; }
    public float Dpi { get; set; }              // default: 72F
    public float WrappingLength { get; set; }   // default: unbounded
    public Vector2 Origin { get; set; }         // default: Vector2.Zero
}
```

### CodePoint

```csharp
// Namespace: SixLabors.Fonts.Unicode (or SixLabors.Fonts)
public readonly struct CodePoint
{
    public CodePoint(char value) { }            // throws for surrogates
    public CodePoint(char highSurrogate, char lowSurrogate) { }
    public CodePoint(int value) { }             // validated 0..0x10FFFF
    public CodePoint(uint value) { }

    public static CodePoint ReplacementChar { get; }     // U+FFFD
    public static CodePoint ObjectReplacementChar { get; } // U+FFFC

    public static explicit operator CodePoint(char ch) { }
    public static explicit operator CodePoint(uint value) { }
    public static explicit operator CodePoint(int value) { }
}
```

### FontStyle Enum (SixLabors.Fonts)

```csharp
// Namespace: SixLabors.Fonts
[Flags]
public enum FontStyle
{
    Regular = 0,
    Bold = 1,
    Italic = 2,
    BoldItalic = Bold | Italic  // = 3
}
```

Note: This is `SixLabors.Fonts.FontStyle`, distinct from `Muonroi.Pdf.Abstractions.FontStyle` which is `Normal/Italic/Oblique`. The mapping in `SixLaborsTextMetrics` must convert between the two enums.

---

## API Reference: AngleSharp.Css @font-face extraction

All verified from AngleSharp.Css GitHub source and confirmed against existing `AngleSharpPageRule.cs` pattern.

### ICssFontFaceRule

```csharp
// Namespace: AngleSharp.Css.Dom
[DomName("CSSFontFaceRule")]
public interface ICssFontFaceRule : ICssRule, ICssProperties
{
    string Family { get; set; }     // font-family value (e.g. "'MyFont'" or "MyFont")
    string Source { get; set; }     // src value (e.g. "url('/fonts/myfont.ttf')")
    string Style { get; set; }      // font-style value (e.g. "normal", "italic")
    string Weight { get; set; }     // font-weight value (e.g. "400", "bold", "700")
    string Stretch { get; set; }    // font-stretch value
    string Range { get; set; }      // unicode-range value
    string Variant { get; set; }    // font-variant value
    string Features { get; set; }   // font-feature-settings value
}
```

### Stylesheet Iteration Pattern (verified from AngleSharpPageRule.cs)

```csharp
// Pattern confirmed from existing codebase: AngleSharpPageRule.TryExtract()
foreach (ICssStyleSheet sheet in document.StyleSheets.OfType<ICssStyleSheet>())
{
    ICssRuleList rules = sheet.Rules;
    for (int i = 0; i < rules.Length; i++)
    {
        if (rules[i] is not ICssFontFaceRule fontFaceRule)
            continue;

        string family = fontFaceRule.Family.Trim('\'', '"');    // strip CSS quotes
        string weightStr = fontFaceRule.Weight;
        string styleStr = fontFaceRule.Style;
        // ... parse weight/style to FontWeight/FontStyle enums
    }
}
```

### FontFaceDeclaration Parsing Notes

`ICssFontFaceRule.Family` returns the CSS value as-is, including any surrounding CSS quotes (e.g., `"'Roboto'"` or `"Roboto"`). Quotes must be stripped before use as a font family name.

`ICssFontFaceRule.Weight` returns strings like `"400"`, `"700"`, `"bold"`, `"normal"`. Requires parsing: `int.TryParse` for numeric values; `"bold"` → `FontWeight.Bold (700)`, `"normal"` → `FontWeight.Normal (400)`.

`ICssFontFaceRule.Style` returns `"normal"`, `"italic"`, `"oblique"`. Map directly to `FontStyle` enum.

Deduplication of `FontFaceDeclaration` records (before returning from `IStyledDocument.FontFaces`): use `Distinct()` or a `HashSet<FontFaceDeclaration>` — records use value equality by default.

---

## Binary Format Reference: TrueType Subsetting

Source: OpenType 1.9.1 specification at learn.microsoft.com/typography/opentype/spec/

### Table Directory Structure

```
Offset 0: sfntVersion  uint32  — 0x00010000 for TTF, 0x4F54544F for CFF-OTF
Offset 4: numTables    uint16
Offset 6: searchRange  uint16
Offset 8: entrySelector uint16
Offset 10: rangeShift  uint16
Offset 12: TableRecords[numTables]  (each record = 16 bytes)

TableRecord (16 bytes):
  Offset 0: tableTag    4 bytes  (e.g. "glyf", "cmap", "head")
  Offset 4: checksum    uint32
  Offset 8: offset      uint32   (from start of file)
  Offset 12: length     uint32
```

All values big-endian. `BinaryPrimitives.ReadUInt32BigEndian` / `ReadUInt16BigEndian` for reads.

### Required Tables for TrueType Subsetter Output

Must copy (mandatory for all OpenType fonts):
- `cmap` — character-to-glyph mapping (rebuilt for subset codepoints)
- `head` — font header (update checksumAdjustment after subset)
- `hhea` — horizontal header
- `hmtx` — horizontal metrics (keep only entries for retained glyphs)
- `maxp` — maximum profile (update numGlyphs)
- `name` — naming table (copy as-is)
- `OS/2` — Windows metrics (copy as-is)
- `post` — PostScript info (copy as-is)

Must copy (TrueType outlines):
- `glyf` — glyph outlines (filter to used glyphs + transitively referenced composite components)
- `loca` — index to location (rebuild for subset GID space)

Optional (copy if present, needed for correct rendering):
- `cvt ` — control value table
- `fpgm` — font program
- `prep` — control value program
- `kern` — kerning (copy as-is or rebuild for subset pairs)

### CFF-OTF Detection

```csharp
uint sfntVersion = BinaryPrimitives.ReadUInt32BigEndian(fontBytes.Span);
bool isCff = sfntVersion == 0x4F54544F; // 'OTTO'
bool isTtf = sfntVersion == 0x00010000;
```

If CFF, return `fontBytes` unchanged (full embedding, no subsetting).

### cmap Format 4 — Remapping for Subset

The output cmap for the subset must map used codepoints to the NEW contiguous GID space (GIDs 0..N-1 after subsetting).

**Original GID space → Subset GID space mapping:**
1. Sort used GIDs ascending: `sortedOriginalGids[0..N-1]`
2. New GID for original GID `g` = `Array.IndexOf(sortedOriginalGids, g)`
3. GID 0 (`.notdef`) must always be present in subset

**cmap Format 4 construction for subset:**
- One segment per contiguous range of (newGid, codepoint) pairs
- If using `idDelta` only (idRangeOffset=0): `idDelta[seg] = startCode - startNewGid`
- Terminate with final segment: `startCode=0xFFFF, endCode=0xFFFF, idDelta=1, idRangeOffset=0`
- `segCountX2` = `segCount * 2`; derive `searchRange`, `entrySelector`, `rangeShift` from `segCount`

For Vietnamese + Latin text the codepoint ranges are non-contiguous (Latin: 0x0020–0x007E, Vietnamese precomposed: scattered across 0x00C0–0x01B0 + 0x1EA0–0x1EF9), so multiple segments are expected. Format 12 (32-bit) may be used instead if supplementary plane characters are present.

### loca Table

```
head.indexToLocFormat == 0  → Short format: uint16 offsets, values = actual_offset / 2
head.indexToLocFormat == 1  → Long format:  uint32 offsets, values = actual_offset

Array size = numGlyphs + 1 (extra entry gives length of last glyph)
loca[glyphId+1] - loca[glyphId] = length of glyph data (0 means no outline)
```

For the subset, rebuild `loca` with `newNumGlyphs+1` entries pointing into the rebuilt `glyf` table. Use long format (indexToLocFormat=1) for simplicity in the subsetter output regardless of original format.

### glyf Table — Composite Glyph Handling

Simple glyph: `numberOfContours >= 0`  
Composite glyph: `numberOfContours < 0` (value -1 recommended)

Composite glyph record (per component):
```
uint16 flags      — bit 5 (0x0020): MORE_COMPONENTS
uint16 glyphIndex — component glyph ID (ORIGINAL space)
... variable argument + transform fields depending on flags
```

When subsetting: if a used glyph is composite, ALL component `glyphIndex` values must also be included in the subset (transitively). Algorithm:

```
HashSet<ushort> required = new(usedGlyphIds);
required.Add(0);  // always include .notdef
Queue<ushort> pending = new(usedGlyphIds);
while (pending.TryDequeue(out ushort gid)):
    if IsComposite(glyf, loca, gid):
        foreach componentGid in GetComponentGids(glyf, loca, gid):
            if required.Add(componentGid):
                pending.Enqueue(componentGid)
```

### head Table — checksumAdjustment

After writing all tables and the new table directory:
1. Set `checksumAdjustment` in `head` table to 0
2. Calculate checksum of each table, store in table directory
3. Calculate checksum of entire font (sum all uint32 words, pad last table to 4-byte boundary)
4. `checksumAdjustment = 0xB1B0AFBA - wholeFilechecksum`
5. Write `checksumAdjustment` back to `head` table at field offset 8 (after majorVersion 2, minorVersion 2, fontRevision 4)

`head` table field layout (relevant fields):
- Offset 0: majorVersion uint16 = 1
- Offset 2: minorVersion uint16 = 0
- Offset 4: fontRevision Fixed (4 bytes)
- Offset 8: checksumAdjustment uint32
- Offset 12: magicNumber uint32 = 0x5F0F3CF5
- ...
- Offset 50: indexToLocFormat int16 (0=short, 1=long)
- Offset 52: glyphDataFormat int16

### maxp Table — numGlyphs Update

`maxp` table offset 4: `numGlyphs uint16` — must be updated to the count of glyphs in the subset.

---

## Binary Format Reference: PNG IHDR

Source: W3C PNG specification https://www.w3.org/TR/PNG/#11IHDR

```
Byte offset  Field           Size    Notes
0–7          PNG signature   8       89 50 4E 47 0D 0A 1A 0A  (magic bytes)
8–11         Chunk length    4       uint32 big-endian = 13 (IHDR is always 13 bytes)
12–15        Chunk type      4       "IHDR" (49 48 44 52)
16–19        Width           4       uint32 big-endian
20–23        Height          4       uint32 big-endian
24           Bit depth       1
25           Color type      1
26           Compression     1
27           Filter method   1
28           Interlace       1
29–32        CRC             4
```

Minimum safe buffer size to read: 24 bytes (through end of height field).

```csharp
// Validation + parsing
if (data.Length < 24) throw PdfInputException("IMG-FORMAT", "PNG header too short");
if (data[0] != 0x89 || data[1] != 0x50 || data[2] != 0x4E || data[3] != 0x47 ||
    data[4] != 0x0D || data[5] != 0x0A || data[6] != 0x1A || data[7] != 0x0A)
    throw PdfInputException("IMG-FORMAT", "Not a valid PNG");

int width  = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
int height = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
```

---

## Binary Format Reference: JPEG SOF

Source: EXIF tag documentation + JPEG marker reference. Verified: SOF marker structure is standardized in ISO/IEC 10918-1.

### SOF Marker Types

| Marker | Hex    | Type |
|--------|--------|------|
| SOF0   | FF C0  | Baseline DCT |
| SOF1   | FF C1  | Extended sequential DCT |
| SOF2   | FF C2  | Progressive DCT |
| SOF3   | FF C3  | Lossless |

All four contain identical field layouts for width/height.

### SOF Segment Layout (offsets within the segment, starting at marker byte)

```
Offset from marker start:
0–1:   Marker         FF C0 / FF C1 / FF C2 / FF C3
2–3:   Length         uint16 big-endian (length of segment payload NOT including marker 2 bytes)
4:     Precision      uint8  (bits per sample, typically 8)
5–6:   Height         uint16 big-endian
7–8:   Width          uint16 big-endian
9:     Components     uint8
```

Height is at `marker_position + 5`, width at `marker_position + 7` (using 0-indexed absolute offsets in the file, where `marker_position` is the offset of the `FF Cx` byte).

Alternative framing: within the SOF segment *payload* (after the 2-byte marker):
- Payload byte 0–1: length
- Payload byte 2: precision
- Payload byte 3–4: height (big-endian uint16)
- Payload byte 5–6: width (big-endian uint16)

### JPEG Scan Algorithm

```csharp
int pos = 0;
// Verify JPEG SOI marker
if (data[0] != 0xFF || data[1] != 0xD8)
    throw PdfInputException("IMG-FORMAT", "Not a valid JPEG");
pos = 2;

while (pos + 3 < data.Length)
{
    if (data[pos] != 0xFF) throw PdfInputException("IMG-FORMAT", "JPEG marker sync lost");
    byte markerByte = data[pos + 1];

    // SOF markers: C0–C3 (skip C4=DHT, C8=JPEGExtension, CA=JPEG2000Ext)
    if (markerByte == 0xC0 || markerByte == 0xC1 || markerByte == 0xC2 || markerByte == 0xC3)
    {
        int height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 5, 2));
        int width  = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 7, 2));
        return (width, height);
    }

    // Skip this segment: marker (2) + length (2) + length_value - 2
    int segLen = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos + 2, 2));
    pos += 2 + segLen;  // marker bytes + segment payload (length field is inclusive)
}
throw PdfInputException("IMG-FORMAT", "JPEG SOF marker not found");
```

### Edge Cases

- **Progressive JPEG (FF C2)**: Same SOF structure — handled by the scan algorithm above.
- **EXIF (APP1, FF E1)**: Appears before SOF as a segment; skipped by the generic segment-skip logic (`segLen` read from bytes 2–3 of each segment).
- **JFIF (APP0, FF E0)**: Same — skipped automatically.
- **Truncated file**: Guard `pos + 3 < data.Length` before reading length field. Guard `pos + 9 <= data.Length` before reading SOF fields.
- **FF FF padding**: Some encoders pad with extra `0xFF` bytes before the marker byte. Handle: skip consecutive `0xFF` bytes until non-FF found, treat that as the marker byte.
- **DHT (FF C4) vs SOF (FF C0–C3)**: Must NOT stop at FF C4 (Huffman table) — the scan skips all non-SOF segments.

---

## Vietnamese Unicode

### Encoding in Practice

Modern Vietnamese documents use **precomposed NFC characters** (single Unicode codepoints) stored in UTF-8. Example: `Tiếng Việt` contains:

- `T` U+0054
- `i` U+0069
- `ế` U+1EBF (LATIN SMALL LETTER E WITH CIRCUMFLEX AND ACUTE — single precomposed codepoint)
- `n` U+006E
- `g` U+0067
- ` ` U+0020
- `V` U+0056
- `i` U+0069
- `ệ` U+1EB9 (LATIN SMALL LETTER E WITH DOT BELOW — single precomposed codepoint)
- `t` U+0074

All Vietnamese precomposed characters are in the BMP (U+0000–U+FFFF), primarily in the Latin Extended Additional block (U+1E00–U+1EFF). This means `CodePoint(char)` construction always works — no surrogate pair handling needed.

Sources: Wikipedia "Vietnamese alphabet" — "most people use precomposed characters when composing Vietnamese-language documents."

### UAX#14 Line-Break Behavior

UAX#14 (Unicode Line Breaking Algorithm) treats Vietnamese as space-based breaking:
- Vietnamese base letters are class AL (Alphabetic)
- Precomposed diacritics are single AL codepoints (not CM class — they are precomposed, not combining)
- Line breaks occur at spaces (class SP) between words
- No syllable-based breaking (unlike Thai/Khmer)

SixLabors.Fonts 2.x implements UAX#14 internally for `TextMeasurer` wrapping. For `ITextMetrics.GetCharWidth` (single-char measurement), line-break logic is not involved — the engine handles break decisions above the metrics layer.

### SixLabors.Fonts Handling of Vietnamese

SixLabors.Fonts 2.x handles Vietnamese precomposed characters correctly through the standard `CodePoint(char)` path. Each precomposed Vietnamese character is a single BMP codepoint that maps to a single glyph in fonts with Vietnamese support. No special combining-mark logic is needed when input is NFC-normalized (which is the standard for modern documents).

**Caveat**: If input HTML contains NFD-decomposed Vietnamese (rare but possible), `TextMeasurer.MeasureAdvance` may return wrong metrics because the base character + combining mark = two separate codepoints but may render as one glyph. The engine should normalize to NFC before measurement. Use `string.Normalize(NormalizationForm.FormC)` on text runs before measuring. Document as a known behavior note.

---

## RFC 2397 data: URI

Source: RFC 2397 (https://www.rfc-editor.org/rfc/rfc2397)

### Format Grammar

```
dataurl    := "data:" [ mediatype ] [ ";base64" ] "," data
mediatype  := [ type "/" subtype ] *( ";" parameter )
data       := *urlchar
```

### Parsing Steps

```csharp
// Input: full src attribute value starting with "data:"
string uri = src;
// 1. Strip "data:" prefix
string rest = uri.Substring(5);  // "data:".Length == 5
// 2. Find comma separator
int commaIdx = rest.IndexOf(',');
if (commaIdx < 0) throw PdfInputException("IMG-FORMAT", "data: URI missing comma");
string header = rest.Substring(0, commaIdx);   // e.g. "image/png;base64"
string dataStr = rest.Substring(commaIdx + 1); // base64 or percent-encoded data
// 3. Parse mediatype and base64 flag
bool isBase64 = header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
string mediaType = isBase64
    ? header.Substring(0, header.Length - ";base64".Length)
    : header;
if (string.IsNullOrEmpty(mediaType))
    mediaType = "text/plain;charset=US-ASCII";  // RFC 2397 default
// 4. Extract base content type (before any parameters)
string contentType = mediaType.Split(';')[0].Trim();
if (string.IsNullOrEmpty(contentType))
    contentType = "text/plain";
// 5. Decode
byte[] bytes = isBase64
    ? Convert.FromBase64String(dataStr)
    : Uri.UnescapeDataString(dataStr).Select(c => (byte)c).ToArray();
```

### Edge Cases

- **Missing `;base64`**: Data is ASCII / percent-encoded text — `Uri.UnescapeDataString` decodes it. For images this would be invalid (images must be base64), so throw `PdfInputException("IMG-FORMAT", "data: URI image without base64 encoding")` if contentType indicates an image type.
- **Whitespace in base64**: RFC 2397 does not allow whitespace in base64 data, but some encoders insert newlines. Strip whitespace before calling `Convert.FromBase64String`: `dataStr = dataStr.Replace("\r", "").Replace("\n", "").Replace(" ", "")`.
- **Charset parameter**: For `text/plain;charset=utf-8` data URIs, relevant only for text — not for PNG/JPEG images. Ignore for image data URIs.
- **Very long data URIs**: RFC 2397 notes LITLEN constraint of 1024 chars for HTML anchors, but in practice HTML attributes can carry much larger data URIs. No artificial length limit in `DataUriDecoder`.

---

## Common Pitfalls

1. **`ReadOnlyMemory<byte>` to `MemoryStream`**: `new MemoryStream(memory.ToArray())` — the `.ToArray()` copy is required because `MemoryStream(byte[])` wraps the array. Avoid `MemoryMarshal.TryGetArray` for `ReadOnlyMemory<byte>` from resolver — the segment's array may not start at offset 0.

2. **SixLabors.Fonts `FontStyle` vs Abstractions `FontStyle`**: These are different enums. `Muonroi.Pdf.Abstractions.FontStyle` has `Normal/Italic/Oblique`. `SixLabors.Fonts.FontStyle` has `Regular/Bold/Italic/BoldItalic` as flags. Oblique maps to Italic in SixLabors.Fonts (no oblique variant).

3. **Font family name quote-stripping**: `ICssFontFaceRule.Family` returns `"'Roboto'"` (with CSS quotes). Must call `.Trim('\'', '"')` before using as dictionary key or `FontCollection.TryGet` argument.

4. **`ITextMetrics.GetCharWidth` for multi-byte UTF-16**: The existing `ITextMetrics` interface takes `char`. For BMP Vietnamese characters this is fine (`char` == codepoint). For supplementary plane characters (outside BMP), a `char` would be a surrogate — the `GetCharWidth(char, ...)` interface cannot handle these. Vietnamese stays BMP so this is not an immediate issue, but document as a known limitation.

5. **Composite glyph closure**: If a glyph referenced in a text run is composite (e.g., a precomposed Vietnamese character built from base + diacritic components in the font), the `TrueTypeFontSubsetter` must transitively include ALL component GIDs. Failing to do this produces a malformed subset font that a PDF reader will reject or render incorrectly.

6. **loca short format overflow**: The short loca format stores `offset / 2` as `uint16`, max offset = 0x1FFFE (131,070 bytes). A subset font with many large glyphs could exceed this. Always write the subset in long loca format (indexToLocFormat=1) to avoid this edge case.

7. **JPEG progressive scan order**: Progressive JPEGs (FF C2) define multiple SOF scans. The `numberOfComponents` field in the first SOF2 gives the correct value; width/height are in the first SOF segment at the standard offsets. The scan algorithm above stops at the FIRST SOF marker found, which is correct.

8. **`head` checksumAdjustment invalidation in collections**: If the subset font is embedded in a PDF collection (not standard for single-font embedding), the checksumAdjustment should be 0 per spec. For normal single-font embedding this is not an issue — always calculate and write the correct value.

9. **Font weight parsing from `ICssFontFaceRule.Weight`**: The value `"bold"` = 700, `"normal"` = 400. Numeric strings like `"400"`, `"700"` need `int.TryParse`. Unknown values (e.g., `"bolder"`, `"lighter"`) should fall back to `FontWeight.Normal`.

10. **`MaxImagePixels` int overflow**: `decoded.Width * decoded.Height` for a 5000x5000 image = 25,000,000 which fits in `int` (max ~2.1 billion). But for a 50,000x50,000 image it overflows. Always use `(long)decoded.Width * decoded.Height` for the comparison.

---

## Don't Hand-Roll

These are handled by SixLabors.Fonts 2.x — do NOT re-implement:

- **Font metrics normalization** (Ascender/Descender in design units → points): the formula `value * fontSize / UnitsPerEm` is the standard conversion; SixLabors.Fonts exposes the raw design-unit values via `HorizontalMetrics`.
- **Unicode line-break algorithm (UAX#14)**: `TextMeasurer.MeasureAdvance` with a `WrappingLength` set in `TextOptions` handles line-break decisions internally for wrapping. For per-character advance width (no wrapping), set `WrappingLength = -1` or leave default (unbounded).
- **Glyph advance width lookup**: `TextMeasurer.MeasureAdvance(char.ToString(), opts).Width` returns the correct typographic advance — do NOT read `hmtx` table directly for width measurement.
- **OpenType GSUB/GPOS substitution and positioning**: SixLabors.Fonts applies these automatically during measurement — Vietnamese ligatures and mark positioning are handled.
- **Base64 decode**: `System.Convert.FromBase64String` — no custom base64 implementation needed.
- **Big-endian byte reading**: `System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian` / `ReadUInt32BigEndian` — no manual bit-shifting needed.

---

## Validation Architecture

### FONT-01/02 Tests (`FontPipelineTests.cs`)

- **Stub `IFontResolver`** returning known TTF bytes; verify `FontCollection.TryGet` succeeds after pipeline
- **Stub returning null**; verify font is skipped gracefully and no exception
- **MaxFontFiles boundary**: create stub with 33 declarations; verify `PdfInputLimitException` thrown before any `ResolveAsync` call
- **CFF-OTF passthrough**: provide OTF bytes (magic `0x4F54544F`); verify `EmbeddedFontInfo.SubsetBytes` equals original bytes

### FONT-03 Tests (`TrueTypeFontSubsetterTests.cs`)

- **Round-trip**: subset a real TTF (use a small open-source font like Roboto-Regular in test resources); verify output parses as valid TTF (magic bytes, table directory readable)
- **Size reduction**: verify `SubsetBytes.Length < originalBytes.Length` for a subset of ASCII + Vietnamese chars from a full Latin+Vietnamese font
- **Glyph count**: read `maxp.numGlyphs` from output; verify it equals `usedGlyphIds.Count + 1` (for .notdef)
- **Composite closure**: use a font with known composite glyphs; verify component GIDs present in subset

### FONT-04 Tests (`VietnameseDiacriticTests.cs`)

- Measure `"Tiếng Việt"` with `SixLaborsTextMetrics` using a font with Vietnamese support
- Verify width > 0 and no `CodePoint.ReplacementChar` is used (i.e., no fallback to .notdef)
- Verify line height is non-zero

### IMG-01/02 Tests (`ImagePipelineTests.cs`)

- **PNG**: provide minimal 1x1 PNG bytes (can synthesize: 8-byte magic + 4-byte len + "IHDR" + width/height); verify `PureImageDecoder.Decode` returns `Width=1, Height=1`
- **JPEG**: provide minimal JPEG with known dimensions; verify `PureImageDecoder.Decode` returns correct width/height
- **Progressive JPEG (FF C2)**: verify scan finds SOF2 marker
- **EXIF JPEG**: provide JPEG with APP1 before SOF; verify width/height still found

### IMG-03 Tests

- `data:image/png;base64,<base64-of-1x1-png>` → verify decoded bytes equal original PNG bytes
- `data:image/jpeg;base64,<base64-of-jpeg>` → verify decoded bytes equal original JPEG bytes
- Missing `;base64` with image contentType → verify `PdfInputException` thrown
- Extra whitespace in base64 payload → verify graceful decode after whitespace strip

### IMG-05 Tests

- Inject a fake `IImageDecoder` returning `DecodedImage(5001, 5000, ...)` → `(long)5001*5000 = 25,005,000 > 25,000,000` → verify `PdfInputLimitException` thrown
- Inject `DecodedImage(5000, 5000, ...)` → `25,000,000 == 25,000,000` → NOT thrown (boundary: `>` not `>=`)

---

## Security Domain

### IResourceResolver Enforcement

`ImagePipeline` must NEVER construct `HttpClient`, open files, or call DNS resolution. All external image bytes come exclusively through `IResourceResolver.ResolveAsync`. This is the security boundary preventing SSRF and path traversal. `IFontResolver` is the equivalent boundary for fonts.

Enforcement: `ImagePipeline` has no constructor injection of `HttpClient`, `IHttpClientFactory`, or any file system abstraction. Only `IResourceResolver` and `IImageDecoder` are injected.

### data: URI Validation

After `DataUriDecoder` extracts content type and bytes:
1. Content type must be one of the supported image formats (`image/png`, `image/jpeg`, `image/jpg`). Other content types should throw `PdfInputException("IMG-FORMAT", "Unsupported data: URI content type: {type}")`.
2. Raw bytes are passed to `PureImageDecoder.Decode` which validates magic bytes — this provides a second defense against content-type spoofing.
3. `MaxImagePixels` validation applies regardless of whether the image came from data URI or external resolver.

### Font Bytes Validation

`TrueTypeFontSubsetter` validates magic bytes before processing:
- If `sfntVersion` is not `0x00010000` (TTF) or `0x4F54544F` (CFF-OTF), throw `PdfInputException("FONT-FORMAT", "Unrecognized font format")`.
- `numTables` should be sanity-checked: `< 100` — a font with 100+ top-level tables is malformed.
- Table offsets must not exceed file bounds — validate each `offset + length <= fontBytes.Length`.

These checks prevent malformed font bytes from causing out-of-bounds reads in the subsetter.

### MaxFontFiles Enforcement Timing

`FontPipeline` checks `IStyledDocument.FontFaces.Count > MaxFontFiles` BEFORE any `ResolveAsync` call. This prevents an attacker from triggering 33+ resolver calls by declaring many `@font-face` rules — the check is O(1) and happens upfront.

---

## Sources

| Source | URL / Location | Confidence | Used For |
|--------|----------------|------------|----------|
| SixLabors.Fonts TextMeasurer.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/TextMeasurer.cs | HIGH | `MeasureAdvance` signature + return type `FontRectangle` |
| SixLabors.Fonts FontCollection.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/FontCollection.cs | HIGH | `Add(Stream)` signature, `TryGet` signature |
| SixLabors.Fonts Font.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/Font.cs | HIGH | `TryGetGlyphs` overloads, `FontMetrics` property, constructors |
| SixLabors.Fonts FontMetrics.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/FontMetrics.cs | HIGH | Abstract properties: `UnitsPerEm ushort`, `HorizontalMetrics`, `VerticalMetrics` |
| SixLabors.Fonts HorizontalMetrics.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/HorizontalMetrics.cs | HIGH | `Ascender short`, `Descender short`, `LineHeight short`, `AdvanceWidthMax short` |
| SixLabors.Fonts TextOptions.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/TextOptions.cs | HIGH | Constructor `TextOptions(Font)`, `Dpi` default 72F |
| SixLabors.Fonts FontFamily.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/FontFamily.cs | HIGH | `CreateFont(float, FontStyle)`, `TryGetMetrics` |
| SixLabors.Fonts CodePoint.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/Unicode/CodePoint.cs | HIGH | `CodePoint(char)`, `CodePoint(int)` constructors |
| SixLabors.Fonts CMapTable.cs | github.com/SixLabors/Fonts/blob/main/src/SixLabors.Fonts/Tables/General/CMapTable.cs | HIGH | `TryGetGlyphId(CodePoint, CodePoint?, out ushort, out bool)` — internal API, used as reference only |
| AngleSharp.Css ICssFontFaceRule.cs | github.com/AngleSharp/AngleSharp.Css/blob/master/src/AngleSharp.Css/Dom/ICssFontFaceRule.cs | HIGH | `Family`, `Weight`, `Style`, `Source` string properties |
| AngleSharp.Css ICssStyleSheet.cs | github.com/AngleSharp/AngleSharp.Css/blob/master/src/AngleSharp.Css/Dom/ICssStyleSheet.cs | HIGH | `Rules ICssRuleList` property, cast pattern from `IStyleSheet` |
| Existing codebase AngleSharpPageRule.cs | src/Muonroi.Pdf.Governance/Cascade/AngleSharpPageRule.cs | HIGH (in-repo) | Confirmed stylesheet iteration pattern: `document.StyleSheets.OfType<ICssStyleSheet>()`, `rules[i] is ICssFontFaceRule` cast |
| OpenType spec — Table Directory | learn.microsoft.com/typography/opentype/spec/otff | HIGH (authoritative) | Table directory structure, `sfntVersion` detection, TableRecord layout, checksum algorithm |
| OpenType spec — glyf table | learn.microsoft.com/typography/opentype/spec/glyf | HIGH (authoritative) | Simple vs composite glyph (`numberOfContours < 0`), component flags, `MORE_COMPONENTS 0x0020`, `glyphIndex uint16` |
| OpenType spec — loca table | learn.microsoft.com/typography/opentype/spec/loca | HIGH (authoritative) | Short (Offset16, divide by 2) vs long (Offset32) format; `numGlyphs+1` entries |
| OpenType spec — head table | learn.microsoft.com/typography/opentype/spec/head | HIGH (authoritative) | `indexToLocFormat` field at offset 50, `checksumAdjustment` at offset 8 |
| OpenType spec — cmap Format 4 | learn.microsoft.com/typography/opentype/spec/cmap | HIGH (authoritative) | `segCountX2`, `endCode[]`, `startCode[]`, `idDelta[]`, `idRangeOffset[]`, `glyphIdArray[]` |
| W3C PNG specification IHDR | w3.org/TR/PNG/#11IHDR | HIGH (authoritative) | Magic bytes `89 50 4E 47 0D 0A 1A 0A`, width at offset 16, height at offset 20 |
| EXIF JPEG SOF structure | exiftool.org/TagNames/JPEG.html | HIGH | SOF segment: marker 2B + length 2B + precision 1B + height 2B + width 2B |
| RFC 2397 data: URI | rfc-editor.org/rfc/rfc2397 | HIGH (authoritative) | `data:[mediatype][;base64],data` grammar; default mediatype `text/plain;charset=US-ASCII` |
| UAX#14 Vietnamese | unicode.org/reports/tr14/tr14-53.html | HIGH (authoritative) | Vietnamese: space-based breaking (AL class), no Brahmic syllable rules |
| Wikipedia Vietnamese alphabet | en.wikipedia.org/wiki/Vietnamese_alphabet | MEDIUM | Precomposed NFC characters are standard; NFD combining-mark approach is rare/legacy |
| Directory.Packages.props | src (in-repo) | HIGH (in-repo) | `SixLabors.Fonts 2.1.0` line 139; `AngleSharp.Css 1.0.0-beta.147` line 12 confirmed |
| PdfConfigs.cs | src (in-repo) | HIGH (in-repo) | `MaxImagePixels = 25_000_000`, `MaxFontFiles = 32` confirmed |
