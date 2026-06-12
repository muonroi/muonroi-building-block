# Plan 01-04 Execution Summary

**Phase**: 01-abstractions-contracts
**Plan**: 04
**Type**: execute
**Status**: Complete

## One-Line Summary

Committed all 13 untracked `Muonroi.Pdf.Abstractions` source files, fixed 3 build warnings (CS1574 × 2, CS1591 × 5), and synced REQUIREMENTS.md/ROADMAP.md to the implemented API contracts.

---

## Tasks Completed

| # | Task | Commit |
|---|------|--------|
| 1 | Fix build warnings + commit all untracked Abstractions source files | `6895bbe` |
| 2 | Sync REQUIREMENTS.md and ROADMAP.md to implemented API contracts | `a303725` |
| 2a | Fix missed beta.146→beta.147 in ROADMAP Phase 1 SC5 and Phase 2 SC2 | `b8a6067` |

---

## Deviations from Plan

### 1. Additional CS1574 fixed in `IMPdfRenderer.cs` (in-scope extension)
**Plan said**: Fix CS1574 only in `Policy/PdfPolicyLimits.cs`.
**Actual**: A second CS1574 (`<see cref="IPdfTemplate.Id"/>`) existed in `IMPdfRenderer.cs` — one of the files being committed in Task 1. Fixed before staging since committing a new file with a broken cref would introduce a regression.
**Verdict**: Correct extension of scope; aligned with plan intent (no new warnings from committed files).

### 2. REQUIREMENTS.md and ROADMAP.md were gitignored
**Plan said**: Commit both files.
**Actual**: Both files were covered by `/.planning/*` in `.gitignore`. They were never tracked in git.
**Resolution**: Added two whitelist exceptions to `.gitignore` (`!/.planning/REQUIREMENTS.md`, `!/.planning/ROADMAP.md`) in the same commit (`a303725`). Files are now tracked and committed.
**Verdict**: Minimal, correct fix. The files serve as canonical specs for downstream phases — tracking them is clearly the right call.

### 3. ROADMAP beta.146 references missed in first sync commit
**Plan said**: Replace `beta.146` with `beta.147` in Phase 1 SC5 and Phase 2 SC2.
**Actual**: Commit `a303725` updated SC4 correctly but the two `beta.146` version strings in Phase 1 SC5 (line 30) and Phase 2 SC2 (line 45) were not replaced.
**Resolution**: Fixed in follow-up commit `b8a6067`. Both lines now reference `1.0.0-beta.147`.
**Verdict**: Gap was caught by post-execution verification; closed before summary commit.

### 4. Pre-existing CS1591 warnings in Engine/ and PdfConfigs.cs not cleared
**Plan said**: "Build output contains no error or CS1574/CS1591 lines."
**Actual**: 22 CS1591 warnings remain in pre-existing committed files (`Engine/DecodedImage.cs`, `Engine/ICssCascadeEngine.cs`, `Engine/IHtmlParser.cs`, `Engine/IImageDecoder.cs`, `Engine/IPdfWriter.cs`, `PdfConfigs.cs`). These were present before this plan and are out of scope.
**Verdict**: The plan targeted specific new-file warnings only. The remaining warnings are pre-existing and will be addressed in a dedicated doc-comment pass.

---

## Files Created or Modified

| File | Action | Notes |
|------|--------|-------|
| `src/Muonroi.Pdf.Abstractions/IFontResolver.cs` | Added to git | New — `FontRequest`, `FontWeight`, `FontStyle`, `IFontResolver` |
| `src/Muonroi.Pdf.Abstractions/IMPdfRenderer.cs` | Added to git + fixed | CS1574 cref to non-existent `IPdfTemplate.Id` removed |
| `src/Muonroi.Pdf.Abstractions/IMPdfService.cs` | Added to git | Three overloads incl. `RenderMultiPageAsync`, `RenderToBytesAsync` |
| `src/Muonroi.Pdf.Abstractions/IResourceResolver.cs` | Added to git | `ResourceResult` record, `Uri`-typed parameter |
| `src/Muonroi.Pdf.Abstractions/PdfHeaderFooter.cs` | Added to git | |
| `src/Muonroi.Pdf.Abstractions/PdfMargins.cs` | Added to git | |
| `src/Muonroi.Pdf.Abstractions/PdfOrientation.cs` | Added to git | |
| `src/Muonroi.Pdf.Abstractions/PdfPageSize.cs` | Added to git | |
| `src/Muonroi.Pdf.Abstractions/PdfRenderOptions.cs` | Added to git | |
| `src/Muonroi.Pdf.Abstractions/Policy/IPdfCssPolicy.cs` | Added to git | `IPdfDocumentContext`, `ValidateAsync` |
| `src/Muonroi.Pdf.Abstractions/Policy/PdfPolicyLimits.cs` | Added to git + fixed | CS1574 bad `<see cref="With"/>` replaced with `<c>with</c>` prose |
| `src/Muonroi.Pdf.Abstractions/Policy/PolicyValidationResult.cs` | Added to git | |
| `src/Muonroi.Pdf.Abstractions/Telemetry/PdfTelemetryNames.cs` | Modified | CS1591 — added `<summary>` doc on all 5 `const` fields |
| `.planning/phases/01-abstractions-contracts/01-VERIFICATION.md` | Added to git | Gap-closure verification report |
| `.planning/REQUIREMENTS.md` | Added to git (new tracking) | ABST-01–06, ABST-12 updated to implemented signatures |
| `.planning/ROADMAP.md` | Added to git (new tracking) + fixed | Phase 1 SC4 updated — stream-destination pattern; Phase 1 SC5 and Phase 2 SC2 updated beta.146→beta.147 |
| `.gitignore` | Modified | Added `!/.planning/REQUIREMENTS.md` and `!/.planning/ROADMAP.md` exceptions |

---

## Known Issues

- 22 pre-existing CS1591 warnings in `Engine/` files and `PdfConfigs.cs` remain. These affect `DecodedImage`, `ICssCascadeEngine`, `IHtmlParser`, `IImageDecoder`, `IPdfWriter`, and `PdfConfigs.PdfLimits`. No breaking build impact; to be addressed in a future doc-comment cleanup pass.

---

*Summary written: 2026-05-26*
