# Phase 04, Plan 03 — Image Decoding Sub-Pipeline

**Completed:** 2026-05-27

## Summary

Implemented the image decoding sub-pipeline: RFC 2397 `DataUriDecoder`, magic-byte-based `PureImageDecoder` (PNG IHDR + JPEG SOF), and the async `ImagePipeline` pre-layout pass. All external images route exclusively through `IResourceResolver`; no direct HTTP or file system access anywhere in the engine.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| Task 1 | `PdfFormatException` + `DataUriDecoder` + `PureImageDecoder` | `8d43eeb` |
| Task 2 | `ImagePipeline` async pre-layout pass | `6a537b2` |

## Files Created or Modified

| File | Action |
|------|--------|
| `src/Muonroi.Pdf.Abstractions/Exceptions/PdfFormatException.cs` | Created |
| `src/Muonroi.Pdf/Internal/Image/DataUriDecoder.cs` | Created |
| `src/Muonroi.Pdf/Internal/Image/PureImageDecoder.cs` | Created |
| `src/Muonroi.Pdf/Internal/Image/ImagePipeline.cs` | Created |

## Deviations from Plan

- **`PdfFormatException` added to Abstractions**: The plan referenced `PdfException("IMG-FORMAT", message)` as a two-arg constructor, but `PdfException` is abstract with no public constructor. A concrete `PdfFormatException(string ruleId, string message)` subclass was added to `Muonroi.Pdf.Abstractions/Exceptions/` to serve as the format error type for all IMG-FORMAT throws.
- **`limits` parameter handling**: `PdfConfigs.PdfLimits` members are all compile-time `const`. The `limits` parameter is acknowledged with `_ = limits;` to prevent unused-parameter warnings; the constant is referenced via `PdfConfigs.PdfLimits.MaxImagePixels` to avoid CS0176 (static member accessed via instance).

## Known Issues

None. Build: 0 errors, 0 new warnings beyond the pre-existing CS1591 XML doc pattern in the Abstractions project.
