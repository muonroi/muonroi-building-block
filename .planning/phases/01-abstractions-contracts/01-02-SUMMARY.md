# Plan 01-02 Execution Summary

Created 8 zero-implementation files in `src/Muonroi.Pdf.Abstractions/Engine/` establishing the adapter seam boundary that prevents AngleSharp, SixLabors.Fonts, and PdfSharpCore types from leaking into the Abstractions assembly.

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| Task 1 | Engine/ opaque marker types (IParsedDocument, IStyledDocument, IPositionedPageList, DecodedImage) + LangVersion=latest | `0a0843a` |
| Task 2 | Engine/ adapter seam interfaces (IHtmlParser, ICssCascadeEngine, IImageDecoder, IPdfWriter) | `566690c` |

## Files Created

- `src/Muonroi.Pdf.Abstractions/Engine/IParsedDocument.cs` — empty marker interface, opaque AngleSharp DOM handle
- `src/Muonroi.Pdf.Abstractions/Engine/IStyledDocument.cs` — empty marker interface, opaque computed-styles handle
- `src/Muonroi.Pdf.Abstractions/Engine/IPositionedPageList.cs` — empty marker interface, opaque layout-output handle
- `src/Muonroi.Pdf.Abstractions/Engine/DecodedImage.cs` — `sealed record` with Width, Height, Data (ReadOnlyMemory<byte>), ContentType
- `src/Muonroi.Pdf.Abstractions/Engine/IHtmlParser.cs` — `ParseAsync(string, CancellationToken) → ValueTask<IParsedDocument>`
- `src/Muonroi.Pdf.Abstractions/Engine/ICssCascadeEngine.cs` — `CascadeAsync(IParsedDocument, string?, CancellationToken) → ValueTask<IStyledDocument>` (mandatory D4 escape hatch)
- `src/Muonroi.Pdf.Abstractions/Engine/IImageDecoder.cs` — `Decode(ReadOnlySpan<byte>, string) → DecodedImage` (synchronous, CPU-bound)
- `src/Muonroi.Pdf.Abstractions/Engine/IPdfWriter.cs` — `WriteAsync(IPositionedPageList, PdfRenderOptions, Stream, CancellationToken) → ValueTask<long>`

## Files Modified

- `src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj` — added `<LangVersion>latest</LangVersion>` (required for ReadOnlyMemory<byte> and nullable annotations); added `System.Memory` and `System.Threading.Tasks.Extensions` package references
- `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` — renamed nested class `Limits` → `PdfLimits` to resolve CS0102 naming conflict with the `Limits` property
- `Directory.Packages.props` — added `System.Memory 4.5.5` and `System.Threading.Tasks.Extensions 4.5.4` version pins

## Files Created (post-initial-execution fixes)

- `src/Muonroi.Pdf.Abstractions/IsExternalInit.cs` — `IsExternalInit` polyfill required for C# 9 `init`-only setters and `record` types when targeting `netstandard2.0`

## Deviations

1. **Plan 01 spec claimed zero package references** but `ValueTask<T>`, `ReadOnlyMemory<T>`, and `ReadOnlySpan<T>` are NOT in the `netstandard2.0` BCL — they require `System.Memory 4.5.5` and `System.Threading.Tasks.Extensions 4.5.4`. Both packages added to CPM and csproj.
2. **IsExternalInit polyfill required** — `netstandard2.0` does not define `System.Runtime.CompilerServices.IsExternalInit`, which the C# 9 compiler emits for records and `init`-only setters. Added as an `internal static class` polyfill.
3. **PdfConfigs.Limits CS0102 conflict** — C# forbids a property and a nested type with the same simple name in the same class. Renamed the nested class from `Limits` to `PdfLimits` while keeping the property name `Limits` intact for IConfiguration binding.

All 8 Engine/ files match the plan specification exactly.

## Invariants Verified

- Zero third-party namespace `using` directives in any Engine/ file (AngleSharp mention is doc-comment only in IParsedDocument.cs)
- All 8 files in namespace `Muonroi.Pdf.Abstractions.Engine`
- Marker interfaces have empty bodies — no base interface extension
- IImageDecoder.Decode is synchronous (no ValueTask) per plan intent
- IPdfWriter references PdfRenderOptions via `using Muonroi.Pdf.Abstractions;`
- Build: `dotnet build` succeeds with 0 errors after fixes (commit `61f0808`)

## Known Issues

None — build is green.
