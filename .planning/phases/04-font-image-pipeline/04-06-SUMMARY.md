# Plan 04-06 Summary — Font & Image Pipeline Unit Tests

Added unit test coverage for all Phase 4 font and image pipeline requirements (FONT-01 through FONT-06, IMG-01 through IMG-05).

## Tasks Completed

| Task | Commit | Description |
|------|--------|-------------|
| 1 | `8fe0870` | FontPipelineTests, VietnameseDiacriticTests, TestFont.ttf embedded resource, csproj update |
| 2 | `5a8fcd7` | ImagePipelineTests (PNG/JPEG decoder, DataUri, pipeline limits) |
| 3 | `bdb863c` | TrueTypeFontSubsetterTests (CFF passthrough, TTF subset reduction) |

## Files Created or Modified

- `tests/Muonroi.Pdf.Tests/Font/FontPipelineTests.cs` — 4 tests: max-font-files limit, null resolver skip, valid TTF metrics, at-limit no-throw
- `tests/Muonroi.Pdf.Tests/Font/VietnameseDiacriticTests.cs` — 3 tests: Vietnamese precomposed char width, line height/ascender, surrogate skip
- `tests/Muonroi.Pdf.Tests/Font/TrueTypeFontSubsetterTests.cs` — 5 tests: CFF passthrough, unrecognized format exception, TTF subset smaller, valid table directory, maxp numGlyphs updated
- `tests/Muonroi.Pdf.Tests/Image/ImagePipelineTests.cs` — 12 tests: PNG/JPEG header parsing, progressive JPEG, bad magic, DataUri decode, whitespace strip, missing base64 flag, resolver routing, null skip, pixel limit exceeded, pixel limit boundary
- `tests/Muonroi.Pdf.Tests/TestResources/TestFont.ttf` — NotoSans-Regular (Apache 2.0) embedded test font
- `tests/Muonroi.Pdf.Tests/Muonroi.Pdf.Tests.csproj` — added `<EmbeddedResource Include="TestResources/**" />`

## Test Results

`dotnet test` — Passed: 47, Failed: 0, Skipped: 0

## Deviations

None. All tests implemented exactly as specified in the plan.

## Known Issues

None.
