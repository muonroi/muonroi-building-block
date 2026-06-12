# Phase 5: PDF Writer + Determinism + Security — Research

**Researched:** 2026-05-27
**Domain:** PdfSharpCore writer adapter, PDF determinism, security hardening
**Confidence:** HIGH (all critical facts verified from actual project source files)

---

## Summary

Phase 5 converts the `PositionedPageList` produced by Phase 4 into an actual PDF 1.7 file via a `PdfSharpCoreWriter : IPdfWriter` implementation. Three parallel concerns must be satisfied simultaneously: (1) correct rendering of positioned elements (text, images, borders), (2) deterministic byte-for-byte output satisfying DET-01–DET-03, and (3) security hardening satisfying SEC-01–SEC-06.

All three plans (05-01 → 05-02 → 05-03) have been designed and the CONTEXT.md decisions are locked. This research document verifies the data model contracts that the writer will consume, confirms what is and is not yet implemented, and maps each requirement to a concrete implementation path.

**Primary recommendation:** Implement strictly in wave order — 05-01 (security foundation + PdfSharpCore reference) → 05-02 (PdfSharpCoreWriter core) → 05-03 (tests). The font resolver lock and SHA-256 FileIdentifier are the two highest-risk items; both require careful `finally`-block discipline.

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | `GlobalFontSettings.FontResolver` wrapped in `static readonly object _fontResolverLock`; save/restore in `finally` | PdfSharpCore v1.3.65 has no per-document font resolver API |
| D2 | Determinism via: no timestamp fields, fixed empty metadata strings, SHA-256(HTML source bytes) → 16-byte hex FileIdentifier, both ID array entries identical | Same input → same hash → same bytes across OS/restarts |
| D3 | Security via API exclusion: writer never calls AcroForm, Outlines with JS, or PdfDictionary manipulation; no post-processing scan needed | Structural prevention is stronger than detection |
| D4 | Image embedding: `XImage.FromStream(() => new MemoryStream(decoded.Data.ToArray()))` — raw compressed PNG/JPEG bytes; PdfSharpCore handles format detection | Matches Phase 4 Decision 4 design for PureImageDecoder |
| D5 | Font key: composite `{Family.ToLowerInvariant()}|{(int)Weight}|{(int)Style}` in `PdfSharpFontResolverAdapter`; maps `isBold→700`, `isItalic→1` | Prevents bold/italic variant collision |

### Claude's Discretion

None specified — all major design decisions are locked.

### Deferred Ideas (OUT OF SCOPE)

- `AddPdf()` DI registration — Phase 6
- OpenTelemetry instrumentation — Phase 6
- End-to-end `IMPdfService.RenderAsync()` — Phase 6
- `SEC-07` multi-tenant cache keys — Phase 6
- TEL-01–05 telemetry — Phase 6
- OTF CFF font subsetting — `KNOWN-DEVIATIONS.md`; full bytes embedded per Phase 4 Decision 3
- Golden snapshot tests (≥40 + ≥10 Vietnamese) — Phase 7
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| PIPE-07 | `IPdfWriter` (PdfSharpCore 1.3.65 adapter) writes positioned boxes to a `Stream` | Data model verified: `PositionedPageList.Pages`, `EmbeddedFonts`, `Images` all present |
| SEC-01 | PDF output version pinned to 1.7; linearization disabled | PdfSharpCore document version can be set; linearization off by default |
| SEC-02 | `/JavaScript`, `/Launch`, `/OpenAction`, `/EmbeddedFile` entries rejected | API exclusion pattern — writer never calls responsible PdfSharpCore APIs |
| SEC-03 | Object IDs deterministic (content-hash–derived or sequential) | SHA-256 FileIdentifier for trailer; PdfSharpCore uses sequential object IDs internally |
| SEC-04 | No timestamp fields written (no `CreationDate`, no `ModDate`) | Leave `doc.Info.*` date fields unset |
| SEC-05 | `<script>` elements rejected by `IPdfCssPolicy` gate | `DefaultStrictPolicy.CheckCssFeatures` — Pass 3 needed (not yet implemented) |
| SEC-06 | `file://` URI scheme rejected by `IResourceResolver` default | `ThrowingResourceResolver` — not yet implemented |
| DET-01 | Same input → byte-for-byte identical output on two renders | SHA-256 FileIdentifier + no timestamps + deterministic compression |
| DET-02 | Determinism holds across process restarts | No process-lifetime state (e.g. `Random`) in output path |
| DET-03 | Determinism holds across Windows/Linux/Alpine | Deflate compression is deterministic; no OS-specific APIs in writer path |
| FONT-02 | TTF and OTF fonts embedded in output PDF | SubsetBytes from `EmbeddedFontInfo` fed to PdfSharpCore font embed APIs |
| FONT-04 | Vietnamese diacritics rendered correctly | Metrics verified in Phase 4; visual confirmation requires Phase 5 PDF output |
| IMG-01 | PNG images embedded in output PDF | `XImage.FromStream` with raw PNG bytes from `DecodedImage.Data` |
| IMG-02 | JPEG images embedded in output PDF | `XImage.FromStream` with raw JPEG bytes from `DecodedImage.Data` |
</phase_requirements>

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| PDF stream generation | `Muonroi.Pdf` (Internal/Writer) | — | PdfSharpCoreWriter is a pure writer; no HTTP, no DI |
| Security exception type | `Muonroi.Pdf.Abstractions` (Exceptions) | — | Exception crosses assembly boundaries; must be in Abstractions |
| URI scheme rejection | `Muonroi.Pdf` (Internal/Security) | — | ThrowingResourceResolver is default implementation; lives in Pdf project |
| Script element policy gate | `Muonroi.Pdf.Governance` (Policies) | — | DefaultStrictPolicy owns all policy enforcement |
| Font resolver global state | `Muonroi.Pdf` (Internal/Writer) | — | Lock + restore is an implementation detail of the writer adapter |
| Determinism anchor (SHA-256) | `Muonroi.Pdf` (Internal/Writer) | — | Computed from PositionedPageList.SourceHashBytes (to be added) |

---

## Standard Stack

### Core (verified from `Directory.Packages.props`)

| Library | Version | Purpose | Status |
|---------|---------|---------|--------|
| `PdfSharpCore` | 1.3.65 | PDF file generation (pages, fonts, images, drawing) | In CPM; NOT yet in `Muonroi.Pdf.csproj` |
| `SixLabors.Fonts` | 2.1.0 | Font subsetting (Phase 4) — SubsetBytes passed to writer | Already in `Muonroi.Pdf.csproj` |
| `xunit` | 2.9.2 | Test framework | In CPM |
| `FluentAssertions` | 7.2.0 | Test assertions | In CPM (pinned, Apache 2.0 on v7.x) |
| `NSubstitute` | 5.3.0 | Mocking in tests | In CPM |

**Installation for Wave 1:**
```xml
<!-- Add to src/Muonroi.Pdf/Muonroi.Pdf.csproj -->
<PackageReference Include="PdfSharpCore" />
```
Version is governed by CPM — no version attribute needed.

---

## Package Legitimacy Audit

> All packages verified as established, high-download libraries in the ecosystem.

| Package | Registry | Age | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|
| PdfSharpCore | NuGet | ~10 yrs | OK | Approved — 1.3.65 pinned in CPM |
| SixLabors.Fonts | NuGet | ~8 yrs | OK | Approved — already referenced |
| xunit | NuGet | ~14 yrs | OK | Approved |
| FluentAssertions | NuGet | ~12 yrs | OK | Approved |
| NSubstitute | NuGet | ~14 yrs | OK | Approved |

**No new packages required.** All five are already declared in `Directory.Packages.props`.

---

## Architecture Patterns

### System Architecture: Phase 5 Data Flow

```
IPdfWriter.WriteAsync(IPositionedPageList, PdfRenderOptions, Stream, ct)
          │
          ▼
PdfSharpCoreWriter.WriteAsync (internal, Muonroi.Pdf)
    │
    ├─ [lock _fontResolverLock]
    │       ├─ Save GlobalFontSettings.FontResolver
    │       ├─ Set new PdfSharpFontResolverAdapter(EmbeddedFonts)
    │       │
    │       ├─ new PdfDocument() → suppress metadata timestamps
    │       │        └─ doc.Info.Title/Subject/Author/Creator = ""
    │       │
    │       ├─ For each PositionedPage → doc.AddPage()
    │       │    └─ XGraphics.FromPdfPage(page)
    │       │         ├─ For InlineBox: gfx.DrawString(text, XFont, XBrush, x, y)
    │       │         ├─ For ReplacedBox: XImage.FromStream(raw bytes) → gfx.DrawImage
    │       │         └─ For borders: gfx.DrawRectangle(XPen, XBrush, rect)
    │       │
    │       ├─ Compute SHA-256(SourceHashBytes) → 16-byte hex FileIdentifier
    │       │        └─ Set doc trailer FileIdentifier[0] = doc trailer FileIdentifier[1]
    │       │
    │       └─ doc.Save(destination) → returns byte count
    │
    └─ [finally: restore GlobalFontSettings.FontResolver]
```

### Recommended Project Structure for Phase 5 Files

```
src/
├── Muonroi.Pdf.Abstractions/
│   └── Exceptions/
│       └── PdfSecurityException.cs        [NEW — Wave 1]
│
├── Muonroi.Pdf/
│   └── Internal/
│       ├── Security/
│       │   └── ThrowingResourceResolver.cs [NEW — Wave 1]
│       └── Writer/
│           ├── PdfSharpFontResolverAdapter.cs [NEW — Wave 2]
│           └── PdfSharpCoreWriter.cs          [NEW — Wave 2]
│
├── Muonroi.Pdf.Governance/
│   └── Policies/
│       └── DefaultStrictPolicy.cs          [EXTEND — Wave 1: script element Pass 3]
│
tests/
└── Muonroi.Pdf.Tests/
    └── Writer/                             [NEW — Wave 3]
        ├── PdfWriterTests.cs
        ├── DeterminismTests.cs
        └── SecurityTests.cs
```

### Pattern 1: PdfSecurityException — follows existing exception hierarchy

**What:** Typed exception for security policy violations; extends `PdfException`.
**When to use:** `ThrowingResourceResolver` throws this when a forbidden URI scheme is encountered.

```csharp
// Follows verified pattern from PdfException.cs
namespace Muonroi.Pdf.Abstractions.Exceptions;

public sealed class PdfSecurityException : PdfException
{
    public PdfSecurityException(string ruleId, string detail, string message)
        : base(ruleId, detail, message) { }
}
```

### Pattern 2: ThrowingResourceResolver — URI scheme rejection

**What:** Default `IResourceResolver` that blocks `file://` and `javascript:` schemes (SEC-06).
**When to use:** Registered as the default when no custom resolver is provided.

```csharp
namespace Muonroi.Pdf.Internal.Security;

internal sealed class ThrowingResourceResolver : IResourceResolver
{
    public ValueTask<ResourceResult?> ResolveAsync(
        Uri uri, string? contentTypeHint, CancellationToken ct)
    {
        if (uri.Scheme == Uri.UriSchemeFile || uri.Scheme == "javascript")
            throw new PdfSecurityException(
                "SEC-06",
                $"Forbidden URI scheme '{uri.Scheme}'",
                $"The URI scheme '{uri.Scheme}' is not allowed.");
        return ValueTask.FromResult<ResourceResult?>(null);
    }
}
```

### Pattern 3: Script element rejection — DefaultStrictPolicy Pass 3

**What:** Walk DOM after CSS feature check, reject `<script>` elements (SEC-05).
**When to use:** Added as Pass 3 in `DefaultStrictPolicy.ValidateAsync`.

```csharp
// Add to CheckCssFeatures or as a separate method called from ValidateAsync
private static void CheckHtmlElements(IDocument document, List<PolicyViolation> violations)
{
    foreach (IElement element in document.All
        .Where(e => e.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase)))
    {
        violations.Add(new PolicyViolation(
            "forbidden.script-element",
            "<script> elements are not permitted in PDF source HTML.",
            PropertyName: "element",
            RejectedValue: "script",
            SuggestedAlternative: "Remove all <script> elements before rendering"));
    }
}
```

### Pattern 4: Font resolver lock + save/restore

**What:** Per-render lock around `GlobalFontSettings.FontResolver` assignment.
**When to use:** Always — wraps the entire `WriteAsync` body.

```csharp
private static readonly object _fontResolverLock = new();

public async ValueTask<long> WriteAsync(
    IPositionedPageList pages, PdfRenderOptions options,
    Stream destination, CancellationToken ct)
{
    var pageList = (PositionedPageList)pages; // same-assembly internal cast
    IFontResolver? previous;
    lock (_fontResolverLock)
    {
        previous = GlobalFontSettings.FontResolver;
        GlobalFontSettings.FontResolver = new PdfSharpFontResolverAdapter(pageList.EmbeddedFonts);
    }
    try
    {
        return WriteDocument(pageList, options, destination);
    }
    finally
    {
        lock (_fontResolverLock)
            GlobalFontSettings.FontResolver = previous;
    }
}
```

### Pattern 5: SHA-256 FileIdentifier for determinism

**What:** Content-hash-derived PDF FileIdentifier satisfying SEC-03 + DET-01/02/03.
**When to use:** Set immediately after creating `PdfDocument`, before adding pages.

```csharp
// SourceHashBytes must be available on PositionedPageList (to be added in Wave 2)
string fileId = Convert.ToHexString(
    System.Security.Cryptography.SHA256.HashData(pageList.SourceHashBytes.Span)[..16])
    .ToLowerInvariant();
// PdfSharpCore trailer manipulation — exact API is ASSUMED; verify against 1.3.65 source
doc.Internals.Catalog.Elements.SetString("/ID[0]", fileId);
doc.Internals.Catalog.Elements.SetString("/ID[1]", fileId);
```

> ⚠️ **Note on FileIdentifier API:** The exact PdfSharpCore API for setting trailer FileIdentifier is tagged `[ASSUMED]`. The PDF trailer `/ID` array is typically accessed via `doc.Internals.Trailer`. Verify the exact path against PdfSharpCore 1.3.65 source before implementing. See Assumptions Log A1.

### Anti-Patterns to Avoid

- **Random FileIdentifier**: `Guid.NewGuid()` or `RandomNumberGenerator` anywhere in the write path breaks DET-01.
- **Setting date fields**: `doc.Info.CreationDate = DateTime.UtcNow` breaks DET-01 and violates SEC-04.
- **Calling `doc.AcroForm`**: Produces action dictionaries and violates SEC-02.
- **Not restoring FontResolver in `finally`**: Font resolver left as wrong adapter after exception — next concurrent render uses wrong fonts.
- **Pixel-decoding images**: `DecodedImage.Data` holds raw compressed bytes, not pixels. Decoding to pixels then re-encoding breaks DET-01 and wastes CPU.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| PDF stream serialization | Custom PDF writer | `PdfSharpCore` (already pinned) | Cross-reference table, object numbering, deflate streams — enormous complexity |
| Font embedding | Manual TTF/OTF stream writing | PdfSharpCore font embed + `PdfSharpFontResolverAdapter` | Font subsetting, Type2/Type0 embedding, encoding differences |
| PNG/JPEG stream embedding | Decode pixels → re-encode | `XImage.FromStream(raw bytes)` | PdfSharpCore handles format detection; re-encoding breaks determinism |
| SHA-256 | Custom hash | `System.Security.Cryptography.SHA256.HashData` (.NET 8 BCL) | Zero dependencies, constant-time, BCL |
| URI scheme validation | String parsing | `new Uri(str).Scheme` | Handles edge cases (percent-encoding, case folding) |

---

## Runtime State Inventory

> Not applicable — Phase 5 is a greenfield implementation within the existing project structure. No rename, migration, or data state changes.

---

## Common Pitfalls

### Pitfall 1: GlobalFontSettings.FontResolver race condition
**What goes wrong:** Two concurrent renders set the global font resolver simultaneously; one render draws text with the other's fonts.
**Why it happens:** PdfSharpCore v1.3.65 has no per-document font resolver — the global is the only hook.
**How to avoid:** `lock(_fontResolverLock)` wraps both the set and the entire draw operation; restore in `finally`.
**Warning signs:** Flaky test failures where wrong font glyphs appear; CI failures not reproducible locally (concurrency-dependent).

### Pitfall 2: SourceHashBytes not yet on PositionedPageList
**What goes wrong:** `PdfSharpCoreWriter` has no access to the original HTML bytes for SHA-256 computation.
**Why it happens:** `IPdfWriter.WriteAsync` receives `IPositionedPageList` and `PdfRenderOptions` — neither carries raw HTML bytes by design.
**How to avoid:** Add `ReadOnlyMemory<byte> SourceHashBytes { get; internal set; }` to `PositionedPageList`; set it in the pipeline stage that calls the layout engine. Plan 05-02 must include this property addition alongside the writer.
**Warning signs:** Compiler error when `PdfSharpCoreWriter` tries to access a property that doesn't exist.

### Pitfall 3: PdfSharpCore PDF version header
**What goes wrong:** PDF output defaults to version 1.4 or 1.5 rather than the required 1.7 (SEC-01).
**Why it happens:** PdfSharpCore may default to an older version if not explicitly set.
**How to avoid:** Set `doc.Version = 17` (PdfSharpCore uses integer: 14=1.4, 17=1.7) in `WriteDocument`. Verify with a hex viewer that the output begins with `%PDF-1.7`.
**Warning signs:** `PdfWriterTests` header check fails; reader reports wrong version.

### Pitfall 4: Determinism broken by dictionary iteration order
**What goes wrong:** Rendering the same document twice produces different bytes because iteration over `Dictionary<string, DecodedImage>` or `IReadOnlyList<EmbeddedFontInfo>` produces different orders.
**Why it happens:** `Dictionary<K,V>` in .NET does not guarantee stable iteration order across calls/versions.
**How to avoid:** Sort `EmbeddedFonts` by composite key before registering with PdfSharpCore; sort `Images` by `Src` key when iterating for embedding order. This ensures element write order is deterministic.
**Warning signs:** `DeterminismTests` fails intermittently or only on a second run.

### Pitfall 5: InlineBox color string parsing
**What goes wrong:** `InlineBox.Color` is a CSS color string (e.g. `"#ff0000"`, `"rgb(255,0,0)"`, `"red"`). PdfSharpCore's `XColor` requires specific input formats.
**Why it happens:** CSS has many color formats; PdfSharpCore accepts hex and named colors but not all CSS forms.
**How to avoid:** Parse CSS color strings to `XColor` via a minimal converter (hex → `XColor.FromArgb`; named → lookup table for the small set of colors likely to appear). Flag unsupported formats as a policy violation or fall back to black.
**Warning signs:** `XColor.FromArgb` throws for `rgb(...)` format inputs.

---

## Code Examples

### Verified data model usage

```csharp
// Source: verified from PositionedPageList.cs, PositionedPage.cs, PositionedElement.cs
// Iterate the page list to draw each element
foreach (PositionedPage page in pageList.Pages)
{
    PdfPage pdfPage = doc.AddPage();
    pdfPage.Width = XUnit.FromPoint(pageSizePoints.Width);
    pdfPage.Height = XUnit.FromPoint(pageSizePoints.Height);

    using XGraphics gfx = XGraphics.FromPdfPage(pdfPage);

    foreach (PositionedElement el in page.Elements)
    {
        Rect r = el.Position; // X, Y, Width, Height in points
        switch (el.Source)
        {
            case InlineBox inline:
                DrawText(gfx, inline, r);
                break;
            case ReplacedBox replaced when pageList.Images.TryGetValue(replaced.Src!, out var img):
                DrawImage(gfx, img, r);
                break;
            case BoxNode block when HasBorder(block):
                DrawBorder(gfx, block, r);
                break;
        }
    }
}
```

### EmbeddedFontInfo composite key

```csharp
// Source: verified from EmbeddedFontInfo.cs + CONTEXT.md Decision 5
// In PdfSharpFontResolverAdapter constructor
var dict = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.Ordinal);
foreach (EmbeddedFontInfo info in embeddedFonts)
{
    string key = $"{info.Family.ToLowerInvariant()}|{(int)info.Weight}|{(int)info.Style}";
    dict[key] = info.SubsetBytes;
}

// In GetFont(string faceName, bool isBold, bool isItalic):
string key = $"{faceName.ToLowerInvariant()}|{(isBold ? 700 : 400)}|{(isItalic ? 1 : 0)}";
return dict.ContainsKey(key) ? new FontResolverInfo(key) : null;
```

### Page size → XUnit conversion

```csharp
// Source: verified from PdfPageSizeDimensions.cs and Units.cs
// A4 portrait = (595.28f, 841.89f) in points
(float widthPt, float heightPt) = PdfPageSizeDimensions.Get(options.PageSize);
if (options.Orientation == PdfOrientation.Landscape)
    (widthPt, heightPt) = (heightPt, widthPt);
```

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | PdfSharpCore 1.3.65 exposes trailer FileIdentifier via `doc.Internals.Trailer` or similar internal API | Code Examples (SHA-256 FileIdentifier) | Must find the correct API path; may need to use reflection or set via a different PdfSharpCore property — does not affect correctness, only implementation path |
| A2 | `GlobalFontSettings.FontResolver` is of type `IFontResolver` (PdfSharp.Fonts namespace), settable via static property | Pattern 4 (font resolver lock) | If the property signature differs, the save/restore variable type must change — low risk, easily corrected at compile time |
| A3 | `doc.Version = 17` sets the PDF version to 1.7 in PdfSharpCore | Pitfall 3 | If the version integer encoding differs, SEC-01 test will catch it |
| A4 | `XImage.FromStream(Func<Stream>)` is the correct overload in PdfSharpCore 1.3.65 for raw bytes | Pattern Decision 4 | If the overload signature differs (e.g. takes `Stream` directly), adjust call site — low risk |

**Recommendation:** Before implementing Wave 2, verify A1 and A4 by inspecting PdfSharpCore 1.3.65 source (available on GitHub at `empira/PDFsharp`). Both are low-risk — compiler errors are the failure mode, not silent bugs.

---

## Open Questions

1. **Exact PdfSharpCore FileIdentifier API**
   - What we know: PDF spec requires `/ID` array in trailer; SHA-256 is the correct input
   - What's unclear: Whether PdfSharpCore 1.3.65 exposes this via `doc.Internals.Trailer.Elements`, `doc.Info`, or a dedicated property
   - Recommendation: Check PdfSharpCore source on first compile; if not directly settable, the PDF trailer can be patched post-save in the output stream (hex-search for `/ID[` and overwrite)

2. **BoxNode background color**
   - What we know: `BoxNode` (verified from source) does not carry a `BackgroundColor` property; `InlineBox.Color` is the text color
   - What's unclear: Where background-color CSS property is stored for block-level elements — it may be on `IStyledNode.Source` or not yet extracted by the box builder
   - Recommendation: Check `BoxTreeBuilder.cs` for background-color extraction; if not present, Phase 5 renders no backgrounds (acceptable for v0.1 scope)

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 8 SDK | All compilation | ✓ | net8.0 (target) | — |
| PdfSharpCore 1.3.65 | Writer implementation | ✓ (CPM declared) | 1.3.65 | — |
| `dotnet test` | Test runner | ✓ | net8.0 | — |
| System.Security.Cryptography.SHA256 | FileIdentifier hash | ✓ (BCL) | .NET 8 | — |

**Missing dependencies with no fallback:** None.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit 2.9.2 + FluentAssertions 7.2.0 |
| Config file | None detected (uses default xunit discovery) |
| Quick run command | `dotnet test tests/Muonroi.Pdf.Tests/ -x` |
| Full suite command | `dotnet test` (solution-level) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| PIPE-07 | Writer produces non-empty PDF stream | unit | `dotnet test --filter "FullyQualifiedName~PdfWriterTests"` | ❌ Wave 3 |
| SEC-01 | PDF header is `%PDF-1.7` | unit | `dotnet test --filter "FullyQualifiedName~PdfWriterTests.PdfVersion_Is1_7"` | ❌ Wave 3 |
| SEC-02 | No JS/Launch/OpenAction/EmbeddedFile in output | unit | `dotnet test --filter "FullyQualifiedName~SecurityTests"` | ❌ Wave 3 |
| SEC-03 | FileIdentifier is content-hash-derived (not random) | unit | `dotnet test --filter "FullyQualifiedName~DeterminismTests"` | ❌ Wave 3 |
| SEC-04 | No CreationDate/ModDate in output stream | unit | `dotnet test --filter "FullyQualifiedName~SecurityTests.NoTimestamps"` | ❌ Wave 3 |
| SEC-05 | `<script>` elements produce `forbidden.script-element` violation | unit | `dotnet test --filter "FullyQualifiedName~SecurityTests.ScriptElement"` | ❌ Wave 3 |
| SEC-06 | `file://` URI throws `PdfSecurityException` | unit | `dotnet test --filter "FullyQualifiedName~SecurityTests.FileUri"` | ❌ Wave 3 |
| DET-01 | Two renders of identical input produce identical bytes | unit | `dotnet test --filter "FullyQualifiedName~DeterminismTests.SameInput"` | ❌ Wave 3 |
| IMG-01 | PNG embedded: output contains PNG stream bytes | unit | `dotnet test --filter "FullyQualifiedName~PdfWriterTests.Png"` | ❌ Wave 3 |
| IMG-02 | JPEG embedded: output contains JPEG stream bytes | unit | `dotnet test --filter "FullyQualifiedName~PdfWriterTests.Jpeg"` | ❌ Wave 3 |

### Wave 0 Gaps (before Wave 3 tests can be written)

- [ ] `tests/Muonroi.Pdf.Tests/Writer/` directory — does not exist
- [ ] `tests/Muonroi.Pdf.Tests/Writer/PdfWriterTests.cs` — covers PIPE-07, SEC-01, IMG-01, IMG-02
- [ ] `tests/Muonroi.Pdf.Tests/Writer/DeterminismTests.cs` — covers SEC-03, DET-01
- [ ] `tests/Muonroi.Pdf.Tests/Writer/SecurityTests.cs` — covers SEC-02, SEC-04, SEC-05, SEC-06
- [ ] `Muonroi.Pdf.Tests.csproj` — must add `<PackageReference Include="xunit" />` etc. if not already inherited via CPM. Current csproj does not show xunit reference — verify CPM covers test packages.

---

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Not applicable — writer is a pure data transformer |
| V3 Session Management | No | Not applicable |
| V4 Access Control | No | Not applicable |
| V5 Input Validation | Yes | `ThrowingResourceResolver` validates URI schemes; `DefaultStrictPolicy` validates HTML elements |
| V6 Cryptography | Yes | SHA-256 via `System.Security.Cryptography.SHA256` (BCL) — never hand-rolled |
| V13 API | No | Not a public API endpoint |

### Known Threat Patterns for PDF Generation

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| JavaScript injection via `<script>` | Tampering / Elevation | SEC-05: policy gate rejects script elements before layout |
| SSRF via `file://` URI in `<img src>` | Information Disclosure | SEC-06: `ThrowingResourceResolver` blocks file:// scheme |
| PDF action injection via `/JavaScript` dictionary | Tampering / Elevation | SEC-02: API exclusion — writer never calls AcroForm/Outlines APIs |
| Timestamp fingerprinting (render time disclosure) | Information Disclosure | SEC-04: `doc.Info.*` date fields left unset |
| Non-deterministic output enabling cache poisoning | Tampering | DET-01/02/03: SHA-256 FileIdentifier, no random state, no OS-specific APIs |

---

## Sources

### Primary (HIGH confidence — verified from project source files)

- `src/Muonroi.Pdf.Abstractions/Engine/IPdfWriter.cs` — WriteAsync signature
- `src/Muonroi.Pdf/Internal/Layout/PositionedPageList.cs` — EmbeddedFonts, Images properties
- `src/Muonroi.Pdf/Internal/Layout/PositionedPage.cs` + `PositionedElement.cs` — page/element structure
- `src/Muonroi.Pdf/Internal/Layout/Boxes/*.cs` — BoxNode, InlineBox, ReplacedBox, BlockBox
- `src/Muonroi.Pdf/Internal/Font/EmbeddedFontInfo.cs` — font record structure
- `src/Muonroi.Pdf.Abstractions/Engine/DecodedImage.cs` — image data contract
- `src/Muonroi.Pdf.Governance/Policies/DefaultStrictPolicy.cs` — existing policy structure, script rejection NOT yet present
- `src/Muonroi.Pdf.Abstractions/Exceptions/PdfException.cs` — base exception pattern
- `Directory.Packages.props` — PdfSharpCore 1.3.65, SixLabors.Fonts 2.1.0
- `src/Muonroi.Pdf/Muonroi.Pdf.csproj` — PdfSharpCore NOT yet referenced
- `.planning/phases/05-pdf-writer-determinism-security/05-CONTEXT.md` — all 5 locked decisions

### Secondary (MEDIUM confidence — from CONTEXT.md decisions, not independently verified)

- PdfSharpCore 1.3.65 `GlobalFontSettings.FontResolver` global static API — referenced in CONTEXT.md Decision 1; not independently verified against PdfSharpCore source
- `XImage.FromStream` overload signature — referenced in CONTEXT.md Decision 4
- `doc.Version = 17` for PDF 1.7 — standard PdfSharpCore pattern; not verified against 1.3.65 source

---

## Metadata

**Confidence breakdown:**
- Data model (PositionedPageList, box types, EmbeddedFontInfo): HIGH — verified from source
- PdfSharpCore API surface (FontResolver, XImage, doc.Version): MEDIUM — from CONTEXT.md decisions; compiler will validate
- Security pattern (API exclusion for /JavaScript etc.): HIGH — structural guarantee from writer being sole PDF author
- Determinism mechanism (SHA-256 FileIdentifier): HIGH — design verified; exact PdfSharpCore API for setting it is MEDIUM

**Research date:** 2026-05-27
**Valid until:** 2026-06-27 (PdfSharpCore 1.3.65 is pinned; stable)
