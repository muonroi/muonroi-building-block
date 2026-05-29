# PNG Test Fixtures — Phase 11.2

## Provenance

All PNG fixtures used in `PngDecoderTests` are **hand-crafted in code** via `PngFixtureBuilder`
(located in `tests/Muonroi.Pdf.Tests/Fixtures/Png/PngFixtureBuilder.cs`).

No binary fixture files are committed. The builder produces structurally valid PNGs at test
runtime, which makes them deterministic, reviewable, and free from external tools (no SkiaSharp
or ImageSharp in the production `Muonroi.Pdf` project).

## Fixtures produced

| Name               | color_type | Size  | Description                                                    |
|--------------------|-----------|-------|----------------------------------------------------------------|
| palette_4color     | 3         | 16×16 | 8-bit indexed, 4 colours, no transparency                      |
| palette_trns       | 3         | 16×16 | 8-bit indexed, 4 colours, colour index 0 fully transparent     |
| rgba_logo          | 6         | 32×32 | 8-bit RGBA, gradient alpha (top=opaque, bottom=transparent)    |

## Re-generating

The builder is self-contained C#. To inspect or regenerate, run:

```pwsh
dotnet test .\tests\Muonroi.Pdf.Tests\Muonroi.Pdf.Tests.csproj --filter "PngDecoder" -v normal
```
