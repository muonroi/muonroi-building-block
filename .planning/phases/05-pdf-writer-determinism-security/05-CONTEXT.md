# Phase 5 Context: PDF Writer + Determinism + Security

**Phase**: 5 of 9
**Name**: PDF Writer + Determinism + Security
**Date captured**: 2026-05-27
**Mode**: Headless autonomous (no interactive discussion)

---

## Domain

Phase 5 converts the `PositionedPageList` produced by Phase 4's layout engine into an actual PDF 1.7 file using `PdfSharpCore`. The phase satisfies all SEC-01–SEC-07 security requirements and DET-01–DET-03 determinism requirements. Font subset bytes and decoded image bytes from Phase 4's `EmbeddedFonts` and `Images` properties are embedded into the PDF stream. No new public contracts are introduced — `IPdfWriter` was defined in Phase 1; this phase provides its default implementation.

---

## Canonical References

- `.planning/REQUIREMENTS.md` — SEC-01 through SEC-07, DET-01 through DET-03, PIPE-07, FONT-02, IMG-01, IMG-02
- `.planning/ROADMAP.md` — Phase 5 success criteria (SC1–SC5)
- `.planning/PROJECT.md` — Key Decisions table; D13 (PDF writer hardened: v1.7 pinned, linearization off, JS/Launch/OpenAction/EmbeddedFile rejected, deterministic IDs); D14 (`IResourceResolver` bytes-only)
- `.planning/phases/04-font-image-pipeline/04-CONTEXT.md` — Decision 3 (EmbeddedFontInfo; TrueTypeFontSubsetter; TTF only, OTF CFF = full bytes); Decision 4 (PureImageDecoder returns raw compressed bytes, NOT pixels — feed directly to XImage.FromStream); Decision 6 (PositionedPageList.EmbeddedFonts, PositionedPageList.Images carrier to Phase 5)
- `.planning/phases/05-pdf-writer-determinism-security/05-01-PLAN.md` — PdfSecurityException, ThrowingResourceResolver, script element policy
- `.planning/phases/05-pdf-writer-determinism-security/05-02-PLAN.md` — PdfSharpFontResolverAdapter, PdfSharpCoreWriter core implementation
- `.planning/phases/05-pdf-writer-determinism-security/05-03-PLAN.md` — PdfWriterTests, DeterminismTests, SecurityTests
- `src/Muonroi.Pdf.Abstractions/IPdfWriter.cs` — adapter seam; `WriteAsync(IPositionedPageList pages, Stream destination, PdfRenderOptions options, CancellationToken ct)`
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfException.cs` — base exception; `PdfSecurityException` extends this
- `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` — internal carrier; `EmbeddedFonts`, `Images` added in Phase 4
- `src/Muonroi.Pdf/Internal/Font/EmbeddedFontInfo.cs` — `internal sealed record EmbeddedFontInfo(Family, Weight, Style, SubsetBytes, UsedGlyphIds)`
- `Directory.Packages.props` — PdfSharpCore 1.3.65 (verified present); SixLabors.Fonts 2.1.0

---

## Existing State (verified from plans 2026-05-27)

| Component | Status |
|-----------|--------|
| `IPdfWriter` | Defined in Phase 1 Abstractions — no implementation yet |
| `PdfSharpCore 1.3.65` | In `Directory.Packages.props`; referenced in `Muonroi.Pdf.csproj` (added in Plan 05-01) |
| `PdfSecurityException` | Created in Plan 05-01 |
| `ThrowingResourceResolver` | Created in Plan 05-01 |
| `DefaultStrictPolicy` — script rejection | Extended in Plan 05-01 |
| `PdfSharpCoreWriter` | Created in Plan 05-02 |
| `PdfSharpFontResolverAdapter` | Created in Plan 05-02 |
| Phase 5 tests | Created in Plan 05-03 |

---

## Implementation Decisions

### Decision 1: `GlobalFontSettings.FontResolver` — per-render lock + restore

**Problem**: PdfSharpCore uses a global static `GlobalFontSettings.FontResolver` to resolve fonts by family name during `XFont` construction. Each render may have different custom fonts (from `EmbeddedFontInfo`). Concurrent renders would race on this global.

**Decision**: `PdfSharpCoreWriter.WriteAsync` holds a `static readonly object _fontResolverLock` and wraps the write operation in `lock(_fontResolverLock)`:
1. Save `previousResolver = GlobalFontSettings.FontResolver`
2. Set `GlobalFontSettings.FontResolver = new PdfSharpFontResolverAdapter(pageList.EmbeddedFonts)`
3. Execute all PdfSharpCore drawing operations
4. Restore `GlobalFontSettings.FontResolver = previousResolver` in a `finally` block

This makes Phase 5 single-threaded on the global font resolver. Phase 6 DI registers `PdfSharpCoreWriter` as a scoped (not singleton) service; the lock is the correctness guarantee.

**Why**: PdfSharpCore's design forces this pattern — there is no per-document font resolver API in v1.3.65. The `IPdfWriter` adapter seam exists precisely to hide this implementation detail; callers never see PdfSharpCore types.

---

### Decision 2: Determinism — suppress timestamps + content-hash FileIdentifier

**Problem**: DET-01/DET-02/DET-03 require byte-for-byte identical output across calls, restarts, and OS. PdfSharpCore writes `CreationDate` and `ModDate` by default. The PDF `FileIdentifier` array (two 16-byte hex strings in the trailer) is random by default.

**Decision**:
1. Leave `doc.Info.CreationDate` and `doc.Info.ModDate` unset — PdfSharpCore omits them when not assigned. Do not set any `doc.Info.*` date fields.
2. Set all string metadata fields to fixed empty strings (`doc.Info.Title = ""`, etc.) to prevent any version-dependent default.
3. The `FileIdentifier` is derived from a SHA-256 of the UTF-8 bytes of the HTML source string. The first 16 bytes of the hash become the identifier string (hex-encoded). Both ID array entries use the same value (per PDF spec, the second entry is for version tracking; using the same value is valid for a non-revisable PDF).
4. PdfSharpCore uses sequential integer object IDs internally — no randomness in object numbering.
5. `doc.Options.NoCompression = false` (default) — deflate compression is deterministic for the same input bytes.

**How to pass HTML bytes to the writer**: `PdfRenderOptions` already has a free slot for additional context. The `IPdfWriter.WriteAsync` signature receives `PdfRenderOptions` — add an internal extension: `PdfSharpCoreWriter` casts the `IPositionedPageList` to `PositionedPageList` (same-assembly internal cast) and reads a `SourceHashBytes` property added in Phase 4e / passed through the pipeline.

**Alternative considered**: Use a fixed constant FileIdentifier (all zeros). Rejected — SEC-03 requires "content-hash–derived or sequential" IDs. A fixed constant is neither; a collision between two different documents sharing the same identifier is a correctness bug.

**Why**: SHA-256 of the HTML source is the natural determinism anchor. Same input → same hash → same FileIdentifier. The hash is computed in `PdfSharpCoreWriter` from a `ReadOnlySpan<byte>` already available in the pipeline. No new API surface exposed.

---

### Decision 3: Security rejection — API exclusion, not post-processing

**Problem**: SEC-02 requires that `/JavaScript`, `/Launch`, `/OpenAction`, `/EmbeddedFile` PDF dictionary entries are never written. How to enforce this without post-processing?

**Decision**: `PdfSharpCoreWriter.WriteAsync` creates a `PdfDocument` and only calls:
- `doc.AddPage()` → returns a `PdfPage`
- `XGraphics.FromPdfPage(page)` → drawing context
- `gfx.DrawString(text, font, brush, x, y)` — text rendering
- `gfx.DrawImage(ximage, x, y, width, height)` — image rendering
- `gfx.DrawRectangle(pen, brush, rect)` — borders/backgrounds

It never calls:
- `doc.AcroForm` — produces form fields and potentially action dictionaries
- `page.Annotations.Add(...)` with any action annotation type
- `doc.Outlines.Add(...)` with JavaScript actions
- Any `PdfDictionary` manipulation that sets `/JavaScript`, `/Launch`, `/OpenAction`, `/EmbeddedFile`

Because the writer is the sole author of the `PdfDocument`, and it doesn't call these APIs, the PDF output structurally cannot contain these entries. No post-processing or PDF scanning is needed.

**Why**: The `IPdfWriter` adapter seam (Phase 1 Decision 12) means the default implementation controls 100% of what goes into the PDF. The threat is accidental use of PdfSharpCore APIs — the safest mitigation is simply not using them, not trying to detect/strip them after the fact.

---

### Decision 4: Image embedding — raw bytes to `XImage.FromStream`

**Problem**: Phase 4's `PureImageDecoder` returns `DecodedImage.Data` containing the original compressed PNG/JPEG bytes (not decoded pixels). Phase 5 must embed these as PDF image streams.

**Decision**: For each `ReplacedBox` in a `PositionedPage`, look up `pageList.Images[src]` to get the `DecodedImage`. Create `XImage xImage = XImage.FromStream(() => new MemoryStream(decoded.Data.ToArray()))` — PdfSharpCore's `XImage.FromStream` accepts raw PNG/JPEG streams and handles format detection internally. Draw with `gfx.DrawImage(xImage, x, y, width, height)`. Dispose `xImage` after drawing.

The `width` and `height` passed to `DrawImage` are the positioned dimensions from the layout engine (in points), not the natural image dimensions. PdfSharpCore scales the image to fit the box.

**Why**: This is the exactly correct integration point. `PureImageDecoder` was designed in Phase 4 Decision 4 specifically to return raw bytes for `XImage.FromStream` — the Phase 4 context document says: "Phase 5 feeds these to `XImage.FromStream`." No intermediate pixel conversion. Pure passthrough.

---

### Decision 5: Font name key in `PdfSharpFontResolverAdapter` — composite `{Family}|{Weight}|{Style}`

**Problem**: Multiple `EmbeddedFontInfo` records can have the same `Family` (e.g., "NotoSans" Regular vs Bold). PdfSharpCore calls `GetFont(faceName, isBold, isItalic)`. If two fonts share the same family name but differ only in weight/style, a simple family-name key causes collision.

**Decision**: `PdfSharpFontResolverAdapter` builds an internal `Dictionary<string, ReadOnlyMemory<byte>>` keyed by `$"{info.Family.ToLowerInvariant()}|{(int)info.Weight}|{(int)info.Style}"`. The `GetFont(string faceName, bool isBold, bool isItalic)` implementation:
1. Constructs the composite key from `faceName.ToLowerInvariant()`, `isBold ? 700 : 400`, `isItalic ? 1 : 0` (matching `FontWeight`/`FontStyle` enum int values).
2. If found: returns `new FontResolverInfo(compositeKey)` (the source key used in `GetFontData`).
3. If not found: returns `null` — PdfSharpCore falls back to system/default font metrics (acceptable; Phase 5 embeds subset TTFs for document fonts, not system fonts).

`GetFontData(string faceDataKey)` does a direct dictionary lookup by the composite key and returns `.ToArray()`.

**Why**: Composite keying avoids weight/style collisions at the cost of string allocation per lookup. In Phase 5, font lookup happens once per unique `XFont` construction per page — the number of unique fonts is bounded by `MaxFontFiles = 32`, making per-lookup allocation negligible.

---

## File Creation Plan

**Plan 05-01 (Wave 1)** — security foundation:
1. `src/Muonroi.Pdf.Abstractions/Exceptions/PdfSecurityException.cs` — `sealed class PdfSecurityException : PdfException`
2. `src/Muonroi.Pdf/Internal/Security/ThrowingResourceResolver.cs` — `internal sealed class ThrowingResourceResolver : IResourceResolver`; throws on `file://` and `javascript:`; returns null for all other schemes
3. `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` — extend with script element pass (Pass 3 after existing CSS passes)
4. `src/Muonroi.Pdf/Muonroi.Pdf.csproj` — add `<PackageReference Include="PdfSharpCore" />`

**Plan 05-02 (Wave 2, depends on 05-01)**:
5. `src/Muonroi.Pdf/Internal/Writer/PdfSharpFontResolverAdapter.cs` — `internal sealed class PdfSharpFontResolverAdapter : PdfSharp.Fonts.IFontResolver`; composite key; backed by `EmbeddedFontInfo[]`
6. `src/Muonroi.Pdf/Internal/Writer/PdfSharpCoreWriter.cs` — `internal sealed class PdfSharpCoreWriter : IPdfWriter`; lock + restore font resolver; suppress timestamps; SHA-256 FileIdentifier; DrawString/DrawImage/DrawRectangle only

**Plan 05-03 (Wave 3, depends on 05-01 + 05-02)**:
7. `tests/Muonroi.Pdf.Tests/Writer/PdfWriterTests.cs` — PDF 1.7 header, non-empty output, font/image rendering
8. `tests/Muonroi.Pdf.Tests/Writer/DeterminismTests.cs` — byte-for-byte identical output on two renders
9. `tests/Muonroi.Pdf.Tests/Writer/SecurityTests.cs` — ThrowingResourceResolver, script element policy

---

## Out of Phase 5 Scope

- `AddPdf()` DI registration and full pipeline orchestration — Phase 6
- OpenTelemetry instrumentation (`PdfTelemetryDescriptor`) — Phase 6
- End-to-end `IMPdfService.RenderAsync()` — Phase 6 (Phase 5 only wires `IPdfWriter` standalone)
- Golden snapshot tests (≥40 + ≥10 Vietnamese) — Phase 7
- NuGet publishing — Phase 7
- `SEC-07` (multi-tenant cache key enforcement) — Phase 6 (cache lives in the service layer, not the writer)
- `TEL-01–05` telemetry requirements — Phase 6
- OTF CFF font subsetting — `KNOWN-DEVIATIONS.md`; full bytes embedded per Phase 4 Decision 3

---

## Autonomous Gray Area Resolutions

| Gray Area | Decision | Rationale |
|-----------|----------|-----------|
| `GlobalFontSettings.FontResolver` thread safety | Per-render static lock + save/restore in `finally` | PdfSharpCore v1.3.65 has no per-document font resolver API; lock is the only correct approach |
| Determinism mechanism for PDF FileIdentifier | SHA-256 of HTML source bytes → 16-byte hex string | Same input → same hash; satisfies SEC-03 "content-hash–derived" and DET-01/DET-02/DET-03 |
| How to block /JavaScript etc. without post-processing | API exclusion: `PdfSharpCoreWriter` simply never calls the responsible PdfSharpCore APIs | Writer is sole PDF author; structural prevention is stronger and cheaper than detection |
| Image embedding bridge (compressed bytes vs pixels) | `XImage.FromStream(raw compressed bytes)` — PdfSharpCore handles PNG/JPEG internally | Phase 4 Decision 4 designed `PureImageDecoder` specifically for this integration |
| Font name collision in resolver adapter | Composite `{Family}|{Weight}|{Style}` key | Prevents bold/italic variant collision; bounded by MaxFontFiles=32 so allocation cost is negligible |
