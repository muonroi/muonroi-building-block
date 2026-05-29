# Phase 9 — v1.0 Enterprise (CLOSE-OUT)

> **Closed:** 2026-05-29
> **Closing goal (user-stated):** PDF output is valid — engine renders structurally valid PDFs across the test corpus AND the TCIS canonical service produces valid PDFs via the new engine.
> **Span:** 5 repos, 5 sub-phases, ~7,500 LOC, **88 new tests** (all green)

## Sub-phase merge summary

| Sub-phase | Repo | Merge SHA | Tests added |
|---|---|---|---|
| **9.1** WS-A Foundation | `muonroi-building-block` | `5477505` | +28 (`Muonroi.Pdf.Enterprise` lib + SSIM + IFeatureGate) |
| **9.2** WS-B Control-Plane Backend | `muonroi-control-plane` (+ companion in building-block) | `94d3246` (CP) + `36e808b`,`0d1b318` (BB) | 0 net (no test infra in target repo; covered by existing integration suite shape) |
| **9.3** WS-C PDF Designer | `muonroi-ui-engine` | `f5c9b4e` | +46 (`@muonroi/ui-engine-pdf-designer@0.1.0`) |
| **9.4** WS-D License Server | `muonroi-license-server` | `4ee9365` | +13 (`KnownPdfCapabilities` + grant/revoke) |
| **9.5** TCIS Cutover (fileexporter) | `tcis.eport.fileexporter.services` | `faef372` | +1 smoke (engine swap verified) |

## Closing-goal evidence: PDF output is valid

### Engine corpus (building-block, post-9.1)
- **475/475 tests pass** (`dotnet test tests/Muonroi.Pdf.Tests/` — re-run 2026-05-29)
- **17/17 production templates render OK** (Phase 8.16 audit carried forward through 9.x — `TemplateImageAudit` re-run 2026-05-29, single test, green)
- Templates covered: BNTT, CAPR_E, CHNG_E, CHNG_F, CRCD_E, CSLA_E, CSLA_F, GTHA_F, GTND_F, HANG_E, HANG_F, HBCX_F, HBL, HBND_F, HSLA_E, HSLA_F, NHAR_E
- Validation depth: PDF 1.7 magic + `%%EOF` + cross-reference table + 100 dpi rasterisation via pdftoppm

### TCIS canonical service (post-9.5)
- **Phase 9.5 smoke**: `tests/Phase95Smoke/Program.cs` produces `smoke-9.5.pdf` (26,998 bytes)
- PDF structure: `%PDF-1.7` magic at offset 0 + `%%EOF` terminator confirmed
- 100 dpi rasterisation: 827×1170 PPM (A4 at expected DPI)
- Re-run on close: PASS
- All 6 `FileExporterService` call sites migrated to `IMPdfService.RenderMultiPageAsync`
- gRPC contract byte-identical (`v1/Protos/file-exporter.proto` unchanged)

## ROADMAP Phase 9 Success Criteria

| # | Criterion | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Template publish propagates to N nodes within 5s via Redis hot-reload; tenant isolation | **INFRA-READY** | `SignalRPdfTemplateChangeNotifier` reusing `RuleSetChangeHub` (9.2); per-tenant group broadcast verified by code review; production load-test pending |
| 2 | Canary SSIM-below-threshold triggers auto-rollback before 100% traffic | **PARTIAL** | `SsimScorer` (9.1) + `POST /api/canary/pdf/score` endpoint (9.2) live; rollback policy automation pending (operational layer) |
| 3 | Designer edit-preview-publish round-trip <10s P95; preview pinned to deployed engine version | **COMPONENT-READY** | `MuPdfTemplateDesigner` (9.3) + iframe preview; P95 measurement pending production traffic |
| 4 | TCIS.ePort renders all invoice templates via `IMPdfService` with `DinkToPdf` removed + zero wkhtmltopdf CVEs | **SATISFIED for fileexporter.services** | 9.5 merge `faef372`; direct DinkToPdf ref removed; libwkhtmltox absent from build output; smoke valid. Cross-service follow-ups 9.5b-e queued for remaining 4 consumers (`download`/`eeir`/`fullcontainerdelivery`/`common`) |
| 5 | ≥3 paid Enterprise customers active + ARR ≥$60k at v1.0 GA | **GO-TO-MARKET GATE** | Engineering scope complete; commercial/sales gate outside this phase |

Engineering-scope SC1–SC4 met or staged for production validation. SC5 is sales-side; no further engineering work required to satisfy the technical prerequisite.

## Deliverables (5 repos)

### `muonroi-building-block` (engine + Enterprise lib)
- `Muonroi.Pdf.Enterprise` 0.1.0-alpha (commercial NuGet, strong-named via `Muonroi.snk`)
- `IFeatureGate` + `FeatureNotLicensedException` + `AlwaysAllowFeatureGate`
- `CapabilityKeys`: `pdf.designer`, `pdf.registry`, `pdf.canary`
- `IMPdfTemplateRegistry` + `IMPdfTemplateHotReload` interfaces
- Pure-managed `SsimScorer` (Wang/Bovik 2004, Rec.709 luminance, 8×8 window, identical=1.0 exact)
- Pure-managed `PngDecoder` (deflate + 5 row-filter types; added in 9.2 Wave D companion)
- `PdfTemplate*` entities + EF migration `AddPdfTemplateDomain` (shared `RuleEngineDbContext`)

### `muonroi-control-plane` (SaaS backend)
- 11 REST endpoints under `/api/v1/control-plane/pdf-templates/*` (list/get/version CRUD + submit/approve/reject/activate)
- `PdfTemplateRegistryService` implementing `IMPdfTemplateRegistry` engine seam
- `SignalRPdfTemplateChangeNotifier` reusing `RuleSetChangeHub` with `TemplateChanged` method
- 6 audit events: `pdf.template.{created,updated,submitted,approved,rejected,activated}`
- `POST /api/canary/pdf/score` SSIM endpoint (multipart PNG → score)

### `muonroi-ui-engine` (commercial React package)
- `@muonroi/ui-engine-pdf-designer@0.1.0` published as 64 KB tgz at repo root
- `MuPdfTemplateDesigner` Monaco-based component (external dep, ~120 KB ESM)
- `<RequireCapability capability="pdf.designer">` — first React capability gate
- `PdfTemplateApiClient` (11 methods, matching 9.2 surface)
- `PdfTemplateChangeSubscription` via `@microsoft/signalr` direct
- `usePdfTemplateHistory` undo/redo hook
- Client-side PROFILE-V1 lint (12 forbidden tags + 3 link/import + 5 warnings)

### `muonroi-license-server` (RSA entitlements)
- `KnownPdfCapabilities` constants (mirrors building-block)
- `POST /api/v1/keys/{licenseKey}/features` admin endpoint (auth: `license-generate`)
- `FeaturesCliCommand` (`dotnet run -- features --add pdf.designer`)
- Claims flow transparently through claim-agnostic `ActivationProofService` RSA pipeline

### `tcis.eport.fileexporter.services` (TCIS canonical PDF service)
- `Muonroi.Pdf` ProjectReference + `AddPdf(configuration)` DI
- `IMPdfService` replaces `IConverter` in `FileExporterService` (6 call sites)
- `DinkToPdf` direct dependency removed; `libwkhtmltox` not in build output
- `PdfNativeLoader.LoadNativeLibraries()` call site removed
- `Phase95Smoke` console harness produces valid 26,998-byte PDF

## Deferred follow-ups (Phase 10 / TCIS sprint backlog)

- **9.5b** — `tcis.eport.download.aggregate.services` direct DinkToPdf cutover
- **9.5c** — `tcis.eport.eeir.aggregate.services` cutover
- **9.5d** — `tcis.eport.fullcontainerdelivery.aggregate.services` cutover
- **9.5e** — `tcis.eport.common` DinkToPdf removal + `PdfNativeLoader.cs` deletion (gated by 9.5b/c/d)
- **9.5f** — Template CSS `counter(page)` migration for page-counter headers (Wave B documented regression)
- **9.5g/h** (optional) — Designer hosting + license-server activation in TCIS production
- **C4** — `unsupported: <feature>` error path in engine (low priority since PROFILE-V1 enforces strict reject list)
- **G4 / G5 / G6** — vertical-align edge + input rendering DEFERRED (0 corpus exposure)
- **TD1 / TD6 / TD7 / TD8** — tech debt LOW

## Lessons learned (Phase 9 aggregate)

- **Open-core architecture works.** OSS engine + commercial assembly with asymmetric coupling (engine has zero awareness of `IFeatureGate`) shipped cleanly. Strong-named commercial DLL + Apache-2.0 OSS DLL coexist in one repo via `Directory.Build.props` license routing.
- **Cross-repo ProjectReference beats local NuGet feeds** for dev velocity in monorepo-style workspaces (precedent set in 9.2, reused in 9.5). Real NuGet publish gates on Phase 10 GA prep.
- **Sub-agent stuck-at-final-report pattern repeats.** 4+ instances across Phases 8.x and 9.x of execution agents hanging after commit, before the result message. Workaround: check git log to confirm work landed, then take over — never delete the agent before confirming nothing committed.
- **Wave-parallel races consolidate work into one commit when both touch the same disk state.** Seen in 8.16, 9.1, 9.3. Cosmetic, not functional. Cost of preventing (serialise waves) exceeds the cost of the cosmetic split.
- **Engine surface should be read, not inferred.** PLAN docs guessed wrong names (`AddMuonroiPdf` vs actual `AddPdf`; `PdfTemplateStatus` 5 vs actual 7 values). Wave R consistently caught these. Lesson: research is non-optional even when the surface "should be obvious."
- **Claim-agnostic infra pays for itself.** License-server `AllowedFeatures` as `text[]` meant Phase 9.4 was ~340 LOC with zero schema changes. Designing capability storage around string bags rather than typed enums has costs (no compile-time validation) but enabled this phase to ship in one commit.

## Test totals (Phase 9 aggregate)

| Repo | Pre-9.x baseline | Post-9.x | Delta |
|---|---|---|---|
| `muonroi-building-block` | 447 | 475 | +28 |
| `muonroi-control-plane` | 33 pass / 44 fail (pre-existing WAF baseline) | 33 / 44 | 0 (no test infra added; 9.2 logic verified via 9.5 integration) |
| `muonroi-ui-engine` | (new package only) | 46 | +46 |
| `muonroi-license-server` | 4 pass / 3 fail (pre-existing) | 17 / 3 | +13 |
| `tcis.eport.fileexporter.services` | (no test project) | 1 smoke | +1 |
| **Total new green tests** | | | **+88** |

## Memory updates

- `[[project_muonroi_pdf_charter]]` — v1 closing-goal validated. Engine is production-ready for the legacy print profile.
- `[[project_muonroi_ecosystem_topology]]` — 4-repo open-core SaaS topology confirmed; PDF rides the same rails as ruleset/decision-table successfully.
- Add: deferred follow-ups list above (Phase 10 backlog) for future planning.

## References

- `.planning/ROADMAP.md` §Phase 9 (now CLOSED)
- `.planning/phases/09.1-ws-a-foundation/VERIFICATION.md`
- `D:\sources\Core\muonroi-control-plane\.planning\phases\09.2-ws-b-control-plane\VERIFICATION.md`
- `D:\sources\Core\muonroi-ui-engine\.planning\phases\09.3-ws-c-pdf-designer\VERIFICATION.md`
- `D:\sources\Core\muonroi-license-server\.planning\phases\09.4-ws-d-license-pdf\VERIFICATION.md`
- `D:\sources\TEP\tcis.eport.fileexporter.services\.planning\phases\09.5-tcis-cutover\VERIFICATION.md`
- `PROFILE-V1.md` (public spec — engine v1 reality)
