# Plan 04-02 Summary: SixLabors.Fonts Integration + Font Internal Types

Added SixLabors.Fonts 2.1.0 to Muonroi.Pdf and created three internal types in `Internal/Font/` enabling real font measurement and codepoint collection for Phase 5 font subsetting.

## Tasks Completed

| Task | Commit |
|------|--------|
| Add SixLabors.Fonts to csproj + create EmbeddedFontInfo + SixLaborsTextMetrics | `2bb7687` |
| Create GlyphCollector post-layout codepoint accumulator | `c72b711` |

## Files Created or Modified

- **Modified**: `src/Muonroi.Pdf/Muonroi.Pdf.csproj` — added `<PackageReference Include="SixLabors.Fonts" />` (CPM, no inline version)
- **Created**: `src/Muonroi.Pdf/Internal/Font/EmbeddedFontInfo.cs` — internal record carrying `Family`, `Weight`, `Style`, `SubsetBytes`, `UsedCodepoints`
- **Created**: `src/Muonroi.Pdf/Internal/Font/SixLaborsTextMetrics.cs` — `ITextMetrics` implementation using `TextMeasurer.MeasureAdvance` for char width and `FontMetrics` for line height/ascender/descender
- **Created**: `src/Muonroi.Pdf/Internal/Font/GlyphCollector.cs` — post-layout pass over `PositionedPageList` collecting Unicode codepoints per font family via `Font.TryGetGlyphs`

## Deviations

- `Font` type alias (`SLFont = SixLabors.Fonts.Font`) and `SLFontFamily` alias required because the file lives in namespace `Muonroi.Pdf.Internal.Font`, which shadows the bare name `Font` (compiler resolves it as the current namespace). Same alias pattern applied in both `SixLaborsTextMetrics` and `GlyphCollector`.
- `CodePoint` required `using SixLabors.Fonts.Unicode;` — not brought in by `using SixLabors.Fonts;` alone.

## Known Issues

None. Build passes with zero errors.
