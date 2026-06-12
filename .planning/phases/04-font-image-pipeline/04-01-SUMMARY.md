# Plan 04-01 Summary: FontFaceDeclaration + IStyledDocument.FontFaces

Added `FontFaceDeclaration` record to Abstractions and implemented `IStyledDocument.FontFaces` in `AngleSharpStyledDocument`.

## Tasks Completed

| Task | Commit |
|------|--------|
| Task 1: `FontFaceDeclaration.cs` created; `IStyledDocument` extended with `FontFaces` property | `14c8ad2` |
| Task 2: `AngleSharpStyledDocument` implements `FontFaces` via `ICssFontFaceRule` AST iteration | `36d7b10` |

## Files Created

- `src/Muonroi.Pdf.Abstractions/Engine/FontFaceDeclaration.cs` — `public sealed record FontFaceDeclaration(string Family, FontWeight Weight, FontStyle Style)`

## Files Modified

- `src/Muonroi.Pdf.Abstractions/Engine/IStyledDocument.cs` — added `IReadOnlyList<FontFaceDeclaration> FontFaces { get; }`
- `src/Muonroi.Pdf.Governance/Cascade/AngleSharpStyledDocument.cs` — implemented `FontFaces` via `ExtractFontFaces`, `ParseFontWeight`, `ParseFontStyle`; added `using` aliases to resolve `AngleSharp.Css.Dom.FontWeight/FontStyle` name collision with Abstractions enums
- `tests/Muonroi.Pdf.Tests/Helpers/FakeStyledDocument.cs` — added `FontFaces` property with empty-list default to satisfy updated interface

## Deviations

- **Alias disambiguation required**: `AngleSharp.Css.Dom` exports `FontWeight` and `FontStyle` enums that collide with `Muonroi.Pdf.Abstractions` enums of the same name. Resolved with `using PdfFontWeight = Muonroi.Pdf.Abstractions.FontWeight` and `using PdfFontStyle = Muonroi.Pdf.Abstractions.FontStyle` in `AngleSharpStyledDocument.cs`. Not anticipated in the plan but a straightforward resolution.
- **FakeStyledDocument update**: Test helper also implements `IStyledDocument` — required adding the new property. Not mentioned in the plan but necessary for zero-error full solution build.

## Known Issues

None. Full solution builds with 0 errors (30 pre-existing warnings unrelated to this plan).
