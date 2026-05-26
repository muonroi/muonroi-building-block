# Discussion Log: Phase 1 — Abstractions + Contracts

**Date**: 2026-05-26
**Mode**: Headless autonomous — all decisions made by agent without interactive input
**Phase**: 1 of 9

---

## Summary

Six gray areas were identified by inspecting the partial existing implementation in `src/Muonroi.Pdf.Abstractions/` against REQUIREMENTS.md and PROJECT.md. All decisions were made autonomously.

---

## Gray Areas Identified and Resolved

### Area 1: TFM mismatch

**Question**: The csproj currently targets `net8.0` (due to a `ProjectReference` to `Muonroi.Core.Abstractions`). REQUIREMENTS PKG-01 mandates `netstandard2.0`.

**Finding**: Inspected all 13 source files in `Muonroi.Pdf.Abstractions/`. Zero types from `Muonroi.Core.Abstractions` are actually used — the reference is scaffolding residue.

**Decision**: Remove `<ProjectReference>` to `Muonroi.Core.Abstractions`, change to `netstandard2.0`.

**Rationale**: Contracts assembly must be zero-dependency for maximum portability and v0.2 source generator compatibility.

---

### Area 2: PdfConfigs vs PdfPolicyLimits

**Question**: REQUIREMENTS call for `PdfConfigs.Limits` with 7 compile-time constants. `PdfPolicyLimits` already exists with 16 properties and different default values. Are these the same thing?

**Decision**: No — they are different layers. `PdfConfigs` = IConfiguration-bound DI options. `PdfPolicyLimits` = per-render policy runtime object. Both must exist.

**Key difference**: `PdfPolicyLimits.MaxHtmlBytes` default is 2 MB (Strict); `PdfConfigs.Limits.MaxHtmlBytes` default is 8 MB per REQUIREMENTS. Different semantic roles justify different defaults.

---

### Area 3: Engine/ adapter seam signatures

**Question**: The Engine/ directory is empty. What method signatures should the four adapter interfaces have that don't leak third-party types?

**Decision**: Use opaque marker interfaces (`IParsedDocument`, `IStyledDocument`, `IPositionedPageList`) as the inter-stage currency. `DecodedImage` is a sealed record (not opaque) since its fields are needed by the layout engine.

**Rationale**: Full encapsulation — no AngleSharp, SixLabors, or PdfSharpCore types visible from the Abstractions assembly.

---

### Area 4: PdfRenderResult — metadata-only vs Content:Stream

**Question**: REQUIREMENTS ABST-12 says `PdfRenderResult` should include `Content : Stream`. The current implementation is metadata-only.

**Decision**: Keep metadata-only. Add `Diagnostics : IReadOnlyList<PolicyViolation>` which is the valid gap.

**Rationale**: Caller supplies destination stream; including `Content : Stream` in the result creates resource-ownership ambiguity. The REQUIREMENTS text was a spec draft artifact.

---

### Area 5: Directory.Packages.props gaps

**Question**: Four PDF dependencies are not in CPM. Should they be added in Phase 1 even before any implementation package uses them?

**Decision**: Yes — add all four version entries now. CPM requires all versions pre-declared; version entries without PackageReferences are harmless.

---

### Area 6: Telemetry/ directory empty

**Question**: Telemetry is Phase 6 scope (TEL-01–TEL-05) but the directory already exists. Should Phase 1 define any telemetry constants?

**Decision**: Add `PdfTelemetryNames.cs` as a static constants-only file. Zero implementation. Prevents magic strings in Phase 2–6.

---

## Deferred Ideas

- `IMPdfService.RenderAsync<TModel>` generic overload mentioned in PROJECT.md — decided redundant given `IMPdfRenderer<T>` exists; no action.
- `IPdfTemplate` interface — not required by REQUIREMENTS; `string TemplateId` on `IMPdfRenderer<T>` is sufficient.
