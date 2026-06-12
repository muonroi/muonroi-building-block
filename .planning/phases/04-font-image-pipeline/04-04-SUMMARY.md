# Plan 04-04 Summary — TrueTypeFontSubsetter + FontPipeline

Created the TTF binary subsetter and the async font resolution orchestrator for the FONT-03 pipeline.

## Tasks Completed

| Task | Commit |
|------|--------|
| Task 1: TrueTypeFontSubsetter | d33efde |
| Task 2: FontPipeline | d33efde |

## Files Created

- `src/Muonroi.Pdf/Internal/Font/TrueTypeFontSubsetter.cs`
- `src/Muonroi.Pdf/Internal/Font/FontPipeline.cs`

## Deviations

- **CFF-OTF pass-through (documented):** Fonts with `sfntVersion == 0x4F54544F` return unchanged. Known limitation documented in plan.
- **Build fix applied:** `GetComponentGids` was originally written as a `yield`-iterator with `ReadOnlySpan<byte>` parameter, which the C# compiler rejects (CS4007). Converted to a regular method returning `List<ushort>`. A duplicate `segCount` local variable inside `BuildCmapTable` was renamed to `srcSegCount` to resolve CS0136.

## Known Issues

None — `dotnet build src/Muonroi.Pdf/Muonroi.Pdf.csproj` passes with zero errors.
