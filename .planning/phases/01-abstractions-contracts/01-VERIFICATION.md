---
phase: 01-abstractions-contracts
verified: 2026-05-26T12:00:00Z
status: passed
score: 5/5 must-haves verified
re_verification:
  previous_status: gaps_found
  previous_score: 3/5
  gaps_closed:
    - "ROADMAP SC4 updated — stream-destination pattern documented; no Content:Stream requirement"
    - "ROADMAP SC5 updated — AngleSharp.Css pinned to 1.0.0-beta.147 throughout ROADMAP; zero beta.146 references remain"
  gaps_remaining: []
  regressions: []
---

# Phase 1: Abstractions + Contracts Verification Report

**Phase Goal:** All public API contracts and adapter seams exist in `Muonroi.Pdf.Abstractions`; every downstream implementation package can reference them without circular dependencies.
**Verified:** 2026-05-26T12:00:00Z
**Status:** passed
**Re-verification:** Yes — after gap closure (Plan 01-04)

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|---------|
| 1 | `Muonroi.Pdf.Abstractions` compiles targeting `netstandard2.0` with zero implementation code | ✓ VERIFIED | `Muonroi.Pdf.Abstractions.csproj` line 3: `<TargetFramework>netstandard2.0</TargetFramework>`; all 25 `.cs` files contain only interfaces, records, enums, and constants — no domain logic or I/O |
| 2 | All six adapter interfaces (`IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter`, `IFontResolver`, `IResourceResolver`) are defined in the Abstractions assembly | ✓ VERIFIED | All six present in `Engine/` and root of `src/Muonroi.Pdf.Abstractions/` with correct non-empty method signatures |
| 3 | `PdfConfigs.Limits` exposes all seven hard limits as compile-time constants matching documented values | ✓ VERIFIED | `PdfConfigs.cs` lines 18–24: MaxHtmlBytes=8_388_608, MaxDomDepth=256, MaxElementCount=100_000, MaxImagePixels=25_000_000, MaxPages=1_000, MaxRenderDurationMs=15_000, MaxFontFiles=32 — all `const`, all match spec |
| 4 | `PdfRenderResult` is metadata-only; PDF bytes written to caller-supplied `Stream destination` on `IMPdfService.RenderAsync` — no content buffering on the result type | ✓ VERIFIED | `PdfRenderResult.cs`: sealed record with 6 metadata fields only (PageCount, ByteCount, Elapsed, TemplateHash, PolicyId, Diagnostics); no Content/Stream property; ROADMAP SC4 now reflects stream-destination design |
| 5 | `Directory.Packages.props` contains AngleSharp, AngleSharp.Css (pinned 1.0.0-beta.147), SixLabors.Fonts, and PdfSharpCore; zero inline `Version` attributes in any csproj | ✓ VERIFIED | `Directory.Packages.props`: AngleSharp 1.3.0, AngleSharp.Css 1.0.0-beta.147 (line 12), SixLabors.Fonts 2.1.0, PdfSharpCore 1.3.65; grep for `Version=` in `src/**/*.csproj` → 0 matches |

**Score:** 5/5 truths verified

---

## Re-verification: Gap Closure Confirmation

### Gap 1 (previously FAILED → CLOSED)

**Truth:** ROADMAP SC4 must reflect stream-destination pattern — no `Content : Stream` on `PdfRenderResult`

**Before:** ROADMAP SC4 read "PdfRenderResult exposes `Content : Stream`" — contradicted CONTEXT.md Decision 4 (stream-destination design adopted; Content:Stream creates ownership ambiguity).

**After:** ROADMAP SC4 now reads: "PdfRenderResult carries metadata only (PageCount, ByteCount, Elapsed, TemplateHash, PolicyId, Diagnostics); PDF bytes are written directly to the caller-supplied `Stream destination` on `IMPdfService.RenderAsync` — no content buffering on the result type."

**Evidence:** `sed -n '28,32p' .planning/ROADMAP.md` → confirmed updated text; phrase "Content : Stream" does not appear anywhere in ROADMAP.md.

**Status:** ✓ CLOSED

---

### Gap 2 (previously FAILED → CLOSED)

**Truth:** ROADMAP must reference AngleSharp.Css pinned to 1.0.0-beta.147 (beta.146 does not exist on NuGet)

**Before:** ROADMAP Phase 1 SC5 and Phase 2 SC2 both read "1.0.0-beta.146" — mismatched `Directory.Packages.props` which had beta.147.

**After:** Both SC5 and Phase 2 SC2 now reference "1.0.0-beta.147".

**Evidence:** `grep -c "beta.146" .planning/ROADMAP.md` → 0; `sed -n '28,32p' .planning/ROADMAP.md` → SC5 line confirms "beta.147".

**Status:** ✓ CLOSED

---

## Regression Check (Previously Passing SCs)

| SC | Quick Check | Result |
|----|-------------|--------|
| SC1 — netstandard2.0 + zero implementation | `csproj` TargetFramework; all `.cs` files remain interfaces/records/enums/constants only | PASS — no regression |
| SC2 — All six adapter interfaces | All 6 adapter interface files present in Engine/ and root | PASS — no regression |
| SC3 — PdfConfigs.Limits, 7 constants | grep MaxHtmlBytes through MaxFontFiles in PdfConfigs.cs — all 7 at correct values | PASS — no regression |

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj` | netstandard2.0, no implementation | ✓ VERIFIED | TFM confirmed; 0 ProjectReference to implementation packages |
| `src/Muonroi.Pdf.Abstractions/Engine/IHtmlParser.cs` | Adapter seam | ✓ VERIFIED | `ParseAsync(string html, CancellationToken) : ValueTask<IParsedDocument>` |
| `src/Muonroi.Pdf.Abstractions/Engine/ICssCascadeEngine.cs` | Adapter seam | ✓ VERIFIED | `CascadeAsync(IParsedDocument, string?, CancellationToken) : ValueTask<IStyledDocument>` |
| `src/Muonroi.Pdf.Abstractions/Engine/IImageDecoder.cs` | Adapter seam | ✓ VERIFIED | `Decode(ReadOnlySpan<byte>, string) : DecodedImage` |
| `src/Muonroi.Pdf.Abstractions/Engine/IPdfWriter.cs` | Adapter seam | ✓ VERIFIED | `WriteAsync(IPositionedPageList, PdfRenderOptions, Stream, CancellationToken) : ValueTask<long>` |
| `src/Muonroi.Pdf.Abstractions/IFontResolver.cs` | Adapter seam | ✓ VERIFIED | `ResolveAsync(FontRequest, CancellationToken) : ValueTask<ReadOnlyMemory<byte>?>` |
| `src/Muonroi.Pdf.Abstractions/IResourceResolver.cs` | Adapter seam | ✓ VERIFIED | `ResolveAsync(Uri, string?, CancellationToken) : ValueTask<ResourceResult?>` |
| `src/Muonroi.Pdf.Abstractions/PdfConfigs.cs` | 7 hard limits as const | ✓ VERIFIED | All 7 constants at correct values |
| `src/Muonroi.Pdf.Abstractions/PdfRenderResult.cs` | Metadata-only record | ✓ VERIFIED | 6 metadata fields; no content buffering |
| `Directory.Packages.props` | CPM, 4 packages, no inline Version | ✓ VERIFIED | All 4 packages present; AngleSharp.Css=beta.147; 0 inline Version attrs in csproj |
| `.planning/ROADMAP.md` | SC4 = stream-destination; SC5 = beta.147 | ✓ VERIFIED | Both SCs updated; "Content : Stream" gone; "beta.146" gone |

---

## Requirements Coverage

| Requirement | Status | Evidence |
|-------------|--------|---------|
| PKG-01 | ✓ SATISFIED | netstandard2.0, zero implementation, 0 build errors |
| ABST-01 | ✓ SATISFIED | `IMPdfService` with 3 overloads (RenderAsync stream-dest, RenderMultiPageAsync, RenderToBytesAsync); REQUIREMENTS.md updated |
| ABST-02 | ✓ SATISFIED | `IMPdfRenderer<TModel>` with TemplateId + RenderAsync(TModel, Stream, options?, ct); REQUIREMENTS.md updated |
| ABST-03 | ✓ SATISFIED | `IMPdfRendererFactory` with Get<T>(templateId) + TryGet<T>; REQUIREMENTS.md updated |
| ABST-04 | ✓ SATISFIED | `IPdfCssPolicy` with ValidateAsync(IPdfDocumentContext, ct) : ValueTask<PolicyValidationResult>; REQUIREMENTS.md updated |
| ABST-05 | ✓ SATISFIED | `IResourceResolver.ResolveAsync(Uri, string?, ct) : ValueTask<ResourceResult?>`; REQUIREMENTS.md updated |
| ABST-06 | ✓ SATISFIED | `IFontResolver.ResolveAsync(FontRequest, ct) : ValueTask<ReadOnlyMemory<byte>?>`; REQUIREMENTS.md updated |
| ABST-07 | ✓ SATISFIED | `ICssCascadeEngine` adapter seam in `Engine/` |
| ABST-08 | ✓ SATISFIED | `IHtmlParser` adapter seam in `Engine/` |
| ABST-09 | ✓ SATISFIED | `IImageDecoder.Decode(ReadOnlySpan<byte>, string contentType)` |
| ABST-10 | ✓ SATISFIED | `IPdfWriter.WriteAsync(IPositionedPageList, PdfRenderOptions, Stream, ct)` |
| ABST-11 | ✓ SATISFIED | `PdfRenderOptions` record with page size, orientation, margins, resolver refs, policy ref |
| ABST-12 | ✓ SATISFIED | `PdfRenderResult` metadata-only (6 fields); REQUIREMENTS.md updated to remove Content:Stream |
| ABST-13 | ✓ SATISFIED | `PdfConfigs.SectionName = "PdfConfigs"` |
| ABST-14 | ✓ SATISFIED | All 7 hard limits at correct const values |

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| Multiple `Engine/` files | — | CS1591 missing XML doc comments (22 pre-existing warnings) | ℹ️ Info | Build 0 errors; warnings pre-date Phase 1; planned for future doc-comment pass |

No TBD, FIXME, XXX, TODO, HACK, or placeholder content found in any `.cs` file under `src/Muonroi.Pdf.Abstractions/`.

---

## Behavioral Verification

**Build:** `dotnet build src/Muonroi.Pdf.Abstractions/Muonroi.Pdf.Abstractions.csproj --no-incremental`
**Result:** Build succeeded. 0 Error(s). 22 pre-existing CS1591 warnings (not blocking).

---

## Human Verification Required

N/A — Infrastructure/contracts phase with no user-facing elements. All acceptance criteria are verifiable programmatically.

---

## Gaps Summary

No gaps remaining. All 5 Phase 1 success criteria are verified:

1. **SC1 PASS** — netstandard2.0, zero implementation code
2. **SC2 PASS** — All 6 adapter interfaces with correct method signatures
3. **SC3 PASS** — All 7 hard limits as compile-time constants at correct values
4. **SC4 PASS** — PdfRenderResult is metadata-only; ROADMAP updated to stream-destination design (closed from 3/5)
5. **SC5 PASS** — AngleSharp.Css pinned to beta.147 in CPM; ROADMAP updated; zero inline Version attrs (closed from 3/5)

Phase 1 goal achieved: all public API contracts and adapter seams exist in `Muonroi.Pdf.Abstractions`; downstream packages can reference them without circular dependencies.

---

_Verified: 2026-05-26T12:00:00Z_
_Verifier: Claude (gsd-verifier) — re-verification after gap closure_
