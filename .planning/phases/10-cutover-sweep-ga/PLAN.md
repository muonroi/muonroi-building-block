# Phase 10 — TCIS Cutover Sweep + v1.0 GA (PLAN)

> **Branch (master/orchestration):** `phase/10-cutover-sweep-ga` in building-block
> **Predecessor:** Phase 9 CLOSED `3c3e513`
> **Goal:** Remove DinkToPdf + libwkhtmltox from ALL remaining TCIS services + publish v1.0 GA NuGet packages.

## Scope split

### 10.A — TCIS cutover sweep (mirror 9.5 pattern per repo)

| Sub | Repo | Primary DinkToPdf usage |
|---|---|---|
| 10.1 | `tcis.eport.download.aggregate.services` | `EirFileGenerationService.cs` — direct HtmlToPdfDocument |
| 10.2 | `tcis.eport.eeir.aggregate.services` | `EirChunkedExportProvider.cs` (line ~611) + `EirDetailsByIdsQueryHandler.cs` + GlobalUsings |
| 10.3 | `tcis.eport.fullcontainerdelivery.aggregate.services` | `PreviewRegistionFormQueryHandler.cs` + `PreviewRegistionInTruckFormQueryHandler.cs` + GlobalUsings |
| 10.4 | `tcis.eport.common` | DELETE `Exporters/HtmlToPdf/` (PdfNativeLoader + ExporterStartupExtention + NativeLibraries dir) + remove DinkToPdf PackageReference |
| 10.5 | template assets | CSS `counter(page)` migration for page-counter headers (Wave B 9.5 documented regression) |

**Order:** 10.1, 10.2, 10.3 parallel (no inter-dependencies — each is an independent gRPC service); 10.4 sequential (gated by 10.1-3); 10.5 parallel (no code).

### 10.B — v1.0 NuGet GA cut

| Sub | Item |
|---|---|
| 10.6 | Version bump `0.1.0-alpha` → `1.0.0` (Muonroi.Pdf + Muonroi.Pdf.Enterprise + @muonroi/ui-engine-pdf-designer) |
| 10.7 | Publish OSS `Muonroi.Pdf 1.0.0` to nuget.org (Apache-2.0) |
| 10.8 | Publish Commercial `Muonroi.Pdf.Enterprise 1.0.0` to private feed |
| 10.9 | Publish `@muonroi/ui-engine-pdf-designer 1.0.0` to private npm registry |
| 10.10 | Public docs site lift (`PROFILE-V1.md` → muonroi.io/pdf/v1) — DEFERRED to ops side if no infra |
| 10.11 | Migration guide for external consumers |

**Order:** 10.6 first (atomic bump); 10.7-10.9 parallel (independent feeds); 10.10-10.11 docs (last).

## Cutover pattern (10.1-10.3 mirror 9.5)

Each follows the EXACT 9.5 wave shape:
1. **R** — read FileExporterService.cs cutover for reference; survey the target repo's DinkToPdf surface
2. **A** — ProjectReference to `Muonroi.Pdf` + `AddPdf(configuration)` DI + `PdfConfigs` appsettings
3. **B** — replace `IConverter`/`HtmlToPdfDocument` with `IMPdfService.RenderMultiPageAsync` + drop globals + remove `PdfNativeLoader` call
4. **C** — smoke test (one real template render → valid PDF) + CHANGELOG
5. Commit on `phase/10.N-cutover-{repo}` → merge to `release/tep-sprint4.0` (or whatever current sprint branch)

## Success criteria

| ID | Criterion | Verify |
|----|-----------|--------|
| SC1 | 4 TCIS service repos build clean with DinkToPdf removed | `dotnet list package` |
| SC2 | `libwkhtmltox.{dll,so}` ABSENT from every service's build output | filesystem check |
| SC3 | `tcis.eport.common` no longer references `DinkToPdf`; `PdfNativeLoader.cs` deleted | grep + ls |
| SC4 | Each service's smoke produces structurally valid PDF (PDF magic + EOF + rasteriser-clean) | per-repo smoke |
| SC5 | `Muonroi.Pdf 1.0.0` + `Muonroi.Pdf.Enterprise 1.0.0` + `@muonroi/ui-engine-pdf-designer 1.0.0` packaged | tgz/nupkg inspection |
| SC6 | Phase 9 SC#4 fully satisfied (zero wkhtmltopdf CVEs attributable to TCIS) | dep scan |

## Risks

1. **Cross-repo coupling unknowns** — 10.4 (common cleanup) only safe after all transitive consumers migrate. PLAN gates ordering.
2. **Template-specific rendering quirks** — each service has its own template shape; smoke tests may surface gaps not in the 17-template engine corpus.
3. **gRPC contracts** — must stay byte-identical for all 3 cutover services (mirror 9.5 SC3).
4. **NuGet publish credentials** — likely require user/ops to confirm before publish. PLAN gates 10.7-10.9 behind explicit confirmation.

## References

- `.planning/PHASE-09-CLOSEOUT.md`
- `D:\sources\TEP\tcis.eport.fileexporter.services\.planning\phases\09.5-tcis-cutover\` (the canonical pattern)
- Memory `[[project_muonroi_pdf_charter]]`
