# Plan 01-01 Summary: csproj Infrastructure + PdfConfigs + PdfTelemetryNames + PdfRenderResult.Diagnostics

Fixed the Muonroi.Pdf.Abstractions project infrastructure, added PdfConfigs POCO with 7 compile-time Limits constants, added PdfTelemetryNames string constants, and extended PdfRenderResult with a Diagnostics field.

## Tasks Completed

| Task | Commit | Description |
|------|--------|-------------|
| Task 1: Fix csproj + GlobalUsings + CPM pins | `ce2fe06` | netstandard2.0 target, removed ProjectReference, removed Metrics using, pinned 4 PDF packages |
| Task 2: PdfConfigs + PdfRenderResult + PdfTelemetryNames | `163d705` | New PdfConfigs.cs, updated PdfRenderResult.cs, new Telemetry/PdfTelemetryNames.cs |

## Files Created or Modified

- **Modified** `src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj` — retargeted to `netstandard2.0`, removed `Muonroi.Core.Abstractions` ProjectReference, hook added `<LangVersion>latest</LangVersion>`
- **Modified** `src/Muonroi.Pdf.Abstractions/GlobalUsings.cs` — removed `global using System.Diagnostics.Metrics`
- **Modified** `Directory.Packages.props` — added CPM version pins: AngleSharp 1.3.0, AngleSharp.Css 1.0.0-beta.147, PdfSharpCore 1.3.65, SixLabors.Fonts 2.1.0
- **Created** `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` — sealed POCO with `SectionName = "PdfConfigs"` and nested `Limits` class with 7 compile-time consts (ABST-14)
- **Modified** `src/Muonroi.Pdf.Abstractions/PdfRenderResult.cs` — added `IReadOnlyList<PolicyViolation> Diagnostics` as final positional parameter (ABST-12)
- **Created** `src/Muonroi.Pdf.Abstractions/Telemetry/PdfTelemetryNames.cs` — static class with 5 const string fields, no Meter/ActivitySource instances

## Deviations from Plan

- **LangVersion hook**: A pre-commit hook automatically added `<LangVersion>latest</LangVersion>` to the csproj. This is additive and does not break any invariant; netstandard2.0 targeting is preserved.
- **Verification false positive**: The plan's verification grep for `ActivitySource` in PdfTelemetryNames.cs matched the field name `ActivitySourceName` (substring match). The file is correct — there is no `ActivitySource` type or instance, only the required const string field.

## Known Issues

None. All must-haves satisfied:
- ✓ `netstandard2.0` target with zero ProjectReference entries
- ✓ No `System.Diagnostics.Metrics` global using
- ✓ All four PDF CPM version pins present with no inline Version in any csproj
- ✓ PdfConfigs.Limits has all 7 compile-time const values matching ABST-14
- ✓ PdfRenderResult includes `IReadOnlyList<PolicyViolation> Diagnostics` as final parameter
- ✓ PdfTelemetryNames has 5 const string fields, no Meter/ActivitySource types
