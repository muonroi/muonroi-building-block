# Roadmap: Muonroi.Pdf

## Overview

Nine phases deliver a pure-managed HTML/CSS-to-PDF renderer from zero to enterprise-grade. Phases 1–7 ship the v0.1 OSS engine: public contracts first, then pipeline stages in strict dependency order (parse → cascade → layout → font/image → write), locked by a golden snapshot corpus and CI gates before any NuGet publish. Phase 8 hardens v0.2 with compile-time source generation, AOT trim-safety, and allocation reduction. Phase 9 builds the commercial Enterprise tier on the stable OSS core.

## Phases

- [x] **Phase 1: Abstractions + Contracts** — Define all public API contracts and adapter seams in `Muonroi.Pdf.Abstractions` (netstandard2.0); zero implementation code (completed 2026-05-26)
- [x] **Phase 2: Parse + Cascade + Policy Gate** — Wire AngleSharp HTML parsing, AngleSharp.Css cascade, and `IPdfCssPolicy.DefaultStrict` in `Muonroi.Pdf.Governance` (completed 2026-05-26)
- [x] **Phase 3: Box Tree + Layout Engine** — Hand-written box tree with block/inline formatting, table layout, pagination, and page counters (completed 2026-05-26)
- [ ] **Phase 4: Font + Image Pipeline** — `IFontResolver` integration, Vietnamese diacritic shaping via SixLabors.Fonts, PNG/JPEG/data-URI image decoding
- [ ] **Phase 5: PDF Writer + Determinism + Security** — PdfSharpCore writer adapter hardened to PDF 1.7 with deterministic IDs and JS/Launch/EmbeddedFile rejection
- [ ] **Phase 6: DI + Telemetry + Integration** — `AddPdf()` DI registration, OpenTelemetry instrumentation, end-to-end `IMPdfService.RenderAsync()` integration
- [ ] **Phase 7: Golden Snapshots + CI Gates + Publishing** — 40+ golden tests, Vietnamese corpus, convention gates, NuGet publish at `1.0.0-alpha.N`
- [ ] **Phase 8: v0.2 — Source Generator + AOT + DesignSystem** — Compile-time `IMPdfRenderer<T>` fast path, trim-safe Alpine container, default design system templates
- [ ] **Phase 9: v1.0 Enterprise** — Postgres template registry, Redis hot-reload, SSIM canary, web designer, TCIS cutover

## Phase Details

### Phase 1: Abstractions + Contracts
**Goal**: All public API contracts and adapter seams exist in `Muonroi.Pdf.Abstractions`; every downstream implementation package can reference them without circular dependencies
**Depends on**: Nothing (first phase)
**Requirements**: PKG-01, ABST-01, ABST-02, ABST-03, ABST-04, ABST-05, ABST-06, ABST-07, ABST-08, ABST-09, ABST-10, ABST-11, ABST-12, ABST-13, ABST-14
**Success Criteria** (what must be TRUE):
  1. `Muonroi.Pdf.Abstractions` compiles targeting `netstandard2.0` with zero implementation code — interfaces and records only
  2. All six adapter interfaces (`IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter`, `IFontResolver`, `IResourceResolver`) are defined in the Abstractions assembly
  3. `PdfConfigs.Limits` exposes all seven hard limits as compile-time constants matching the documented values (MaxHtmlBytes 8 MB, MaxDomDepth 256, MaxElementCount 100k, MaxImagePixels 25 MP, MaxPages 1000, MaxRenderDurationMs 15000, MaxFontFiles 32)
  4. `PdfRenderResult` carries metadata only (`PageCount`, `ByteCount`, `Elapsed`, `TemplateHash`, `PolicyId`, `Diagnostics`); PDF bytes are written directly to the caller-supplied `Stream destination` on `IMPdfService.RenderAsync` — no content buffering on the result type
  5. `Directory.Packages.props` contains AngleSharp, AngleSharp.Css (pinned 1.0.0-beta.147), SixLabors.Fonts, and PdfSharpCore; zero inline `Version` attributes in any csproj
**Plans**: 4 plans

Plans:
- [x] 01-01-PLAN.md — Fix csproj (netstandard2.0), GlobalUsings, CPM version pins; add PdfConfigs, PdfRenderResult.Diagnostics, PdfTelemetryNames
- [x] 01-02-PLAN.md — Create Engine/ adapter seams: 4 marker types + 4 seam interfaces (IHtmlParser, ICssCascadeEngine, IImageDecoder, IPdfWriter)
- [x] 01-03-PLAN.md — Create Muonroi.Pdf.Enterprise stub (PKG-04) + build verification for both projects
- [x] 01-04-PLAN.md — Gap closure: commit untracked public API contracts, fix build warnings, sync REQUIREMENTS + ROADMAP to implemented signatures

### Phase 2: Parse + Cascade + Policy Gate
**Goal**: HTML input is parsed, CSS is cascaded, and every unsupported CSS construct is caught with a structured diagnostic before any layout code runs
**Depends on**: Phase 1
**Requirements**: PKG-03, PIPE-01, PIPE-02, PIPE-03, PIPE-04, GOV-01, GOV-02, GOV-03
**Success Criteria** (what must be TRUE):
  1. AngleSharp parses a valid HTML5 document through `IHtmlParser` and returns a DOM tree; the AngleSharp type does not leak through the adapter seam
  2. `ICssCascadeEngine` produces computed styles on the DOM using AngleSharp.Css 1.0.0-beta.147; the result is accessible without exposing any AngleSharp type to callers
  3. HTML input exceeding `MaxHtmlBytes` (8 MB) is rejected before parsing with a typed exception and structured error
  4. DOM depth or element count exceeding their limits triggers a structured error before any cascade or layout step
  5. A document using `display:flex`, `float`, or `position:absolute` triggers a `PolicyViolation` with property name, rejected value, CSS selector, and a suggested alternative; `Muonroi.Pdf.Governance` compiles and `IPdfCssPolicy.DefaultStrict` rejects all six blocked feature categories
**Plans**: 5 plans

Plans:
- [x] 02-01-PLAN.md — Abstractions gap closure: exception hierarchy (PdfException, PdfInputLimitException, PdfPolicyException), extended PolicyViolation, RequirePolicySignature on PdfConfigs
- [x] 02-02-PLAN.md — Create Muonroi.Pdf.Governance csproj (net8.0) + test project scaffold + register both in solution
- [x] 02-03-PLAN.md — HTML parsing adapter: AngleSharpParsedDocument + AngleSharpHtmlParser with PIPE-01/PIPE-02 limit enforcement
- [x] 02-04-PLAN.md — CSS cascade adapter: AngleSharpStyledDocument + AngleSharpCascadeEngine with IPdfDocumentContext eager metrics
- [x] 02-05-PLAN.md — Policy gate: DefaultStrictPolicy (GOV-01/GOV-02, 9 blocked features) + SignedPdfCssPolicyDecorator (GOV-03)

### Phase 3: Box Tree + Layout Engine
**Goal**: A styled DOM converts to a box tree and lays out into pages with correct block/inline/table formatting, margin collapsing, and pagination
**Depends on**: Phase 2
**Requirements**: PIPE-05, PIPE-06, LAYOUT-01, LAYOUT-02, LAYOUT-03, LAYOUT-04, LAYOUT-05, LAYOUT-06, LAYOUT-07, PAGE-01, PAGE-02, PAGE-03, PAGE-04, PAGE-05, PAGE-06, PAGE-07, PAGE-08
**Success Criteria** (what must be TRUE):
  1. Adjacent block elements with vertical margins collapse to the maximum margin per CSS 2.1 §8.3.1; BFC roots (overflow:hidden, table cells) do not collapse across the root boundary
  2. Inline text with mixed `vertical-align` values renders at correct baseline offsets; mixed Latin+Vietnamese in the same line breaks at correct Unicode break opportunities
  3. A table with `colspan=2` and `rowspan=2` cells lays out with correct column widths; `border-collapse:collapse` triggers a `PolicyViolation` naming `border-collapse:separate` as the alternative
  4. `page-break-before:always` forces a page break at the element boundary; the header defined in the `@page` top margin box repeats verbatim on every page
  5. `counter(pages)` in a footer resolves to the correct total page count via two-pass layout
**Plans**: 9 plans

Plans:
- [x] 03-01-PLAN.md — Abstractions contracts (IStyledNode/IComputedStyle/IPageRule) + Muonroi.Pdf csproj setup
- [x] 03-02-PLAN.md — Governance gap: AngleSharpStyledNode/ComputedStyle/PageRule + extend AngleSharpStyledDocument
- [x] 03-03-PLAN.md — Box tree types: geometry helpers + full BoxNode hierarchy (12 files)
- [x] 03-04-PLAN.md — BoxTreeBuilder + ITextMetrics seam + positioning types + test project scaffold
- [x] 03-05-PLAN.md — BlockLayoutEngine (BFC, margin collapsing) + InlineLayoutEngine (IFC, baseline)
- [x] 03-06-PLAN.md — TableLayoutEngine (colspan/rowspan) + PaginationEngine (breaks, counters, header/footer)
- [x] 03-07-PLAN.md — LayoutEngine two-pass entry point + KNOWN-DEVIATIONS.md
- [x] 03-08-PLAN.md — Unit tests SC1–SC5 (22 tests, dotnet test exits 0)
- [x] 03-09-PLAN.md — Gap closure: LAYOUT-07 border-collapse policy fix + governance test + KD-03-05 Vietnamese break test

### Phase 4: Font + Image Pipeline
**Goal**: Fonts are resolved, shaped, and subsetted; images are decoded; Vietnamese diacritics render correctly; all resource limits are enforced
**Depends on**: Phase 3
**Requirements**: FONT-01, FONT-02, FONT-03, FONT-04, FONT-05, FONT-06, IMG-01, IMG-02, IMG-03, IMG-04, IMG-05
**Success Criteria** (what must be TRUE):
  1. A `@font-face` declaration resolves font bytes via `IFontResolver`; the embedded TTF/OTF in the output PDF contains only glyphs used in the document (subsetting verified by embedded glyph table size)
  2. Vietnamese text "Tiếng Việt" renders with correctly stacked diacritics — no replacement glyphs, correct combining-mark positions above base glyphs
  3. A PNG image referenced via `data:image/png;base64,...` URI is decoded and embedded with no outbound network calls
  4. An external image `src` is resolved exclusively via `IResourceResolver.ResolveAsync`; any direct file-path or HTTP resolution throws `PdfSecurityException`
  5. An image whose decoded pixel count exceeds `MaxImagePixels` (25 MP) is rejected with a structured error before any layout measurement
**Plans**: 6 plans

Plans:
- [ ] 04-01-PLAN.md — FontFaceDeclaration + IStyledDocument.FontFaces (Abstractions) + AngleSharpStyledDocument implementation (Governance)
- [ ] 04-02-PLAN.md — SixLabors.Fonts csproj ref + EmbeddedFontInfo + SixLaborsTextMetrics + GlyphCollector
- [ ] 04-03-PLAN.md — DataUriDecoder + PureImageDecoder (PNG IHDR + JPEG SOF) + ImagePipeline async pre-pass
- [ ] 04-04-PLAN.md — TrueTypeFontSubsetter (TTF binary subsetter) + FontPipeline async orchestrator
- [ ] 04-05-PLAN.md — PositionedPageList extended + BoxTreeBuilder resolvedImages + LayoutEngine.LayoutAsync wiring
- [ ] 04-06-PLAN.md — Unit tests: FontPipelineTests, VietnameseDiacriticTests, ImagePipelineTests, TrueTypeFontSubsetterTests

### Phase 5: PDF Writer + Determinism + Security
**Goal**: The positioned box list writes to a deterministic, hardened PDF 1.7 stream; the default writer rejects all JavaScript/launch/embedded-file constructs and never writes timestamps
**Depends on**: Phase 4
**Requirements**: PIPE-07, SEC-01, SEC-02, SEC-03, SEC-04, SEC-05, SEC-06, SEC-07, DET-01, DET-02, DET-03
**Success Criteria** (what must be TRUE):
  1. Rendering the same HTML+CSS input twice in the same process produces byte-for-byte identical `Stream` output
  2. Rendering the same input after a process restart produces the same bytes (no process-lifetime state leaks into object IDs or content hashes)
  3. The output PDF version header is `%PDF-1.7`; no `CreationDate`, `ModDate`, producer timestamp, or random object IDs appear
  4. Calling the writer with a `/JavaScript` or `/EmbeddedFile` dictionary entry throws `PdfSecurityException`; the default `IPdfWriter` never writes these entries
  5. A `<script>` element in HTML input is rejected by the policy gate with a structured diagnostic before the box tree is built
**Plans**: TBD

### Phase 6: DI + Telemetry + Integration
**Goal**: The full pipeline is wired through `AddPdf()` DI, the engine emits correct OpenTelemetry spans and metrics, and a single `RenderAsync()` call drives HTML to a valid PDF stream end-to-end
**Depends on**: Phase 5
**Requirements**: PKG-02, DI-01, DI-02, DI-03, DI-04, PIPE-08, TEL-01, TEL-02, TEL-03, TEL-04, TEL-05
**Success Criteria** (what must be TRUE):
  1. `services.AddPdf(configuration)` on a fresh host registers all pipeline services; calling it twice does not register duplicates (`TryAddSingleton`)
  2. `IMPdfService.RenderAsync(html, options, stream, ct)` converts a valid HTML document to a non-empty `Stream` containing a `%PDF-1.7` header
  3. Each render emits a completed activity span on `Muonroi.BuildingBlock.Pdf` with `pdf.template_id` and `tenant.id` attributes in snake_case; `pdf.page_count` histogram records the page count
  4. A render exceeding `MaxRenderDurationMs` (15 s) is cancelled with `OperationCanceledException`
  5. `PdfConfigs` bound from `"PdfConfigs"` IConfiguration section with `MaxPages: 0` throws a validation exception at startup — before any render is attempted
**Plans**: TBD

### Phase 7: Golden Snapshots + CI Gates + Publishing
**Goal**: The engine is regression-locked by a verified golden corpus; all convention gates pass; four packages are published to NuGet
**Depends on**: Phase 6
**Requirements**: PKG-04, PKG-05, PKG-06, PKG-07, TEST-01, TEST-02, TEST-03, TEST-04, PERF-01, PERF-02, GATE-01, GATE-02, GATE-03
**Success Criteria** (what must be TRUE):
  1. 40+ golden snapshot tests pass; rendering the same HTML corpus twice produces identical bytes (determinism canary in CI)
  2. 10+ Vietnamese golden snapshots confirm diacritic rendering is stable; `KNOWN-DEVIATIONS.md` lists every intentional CSS 2.1 deviation in the declared subset
  3. Cold render of the 50 KB reference template completes in ≤300 ms; warm render in ≤80 ms on a single developer-machine thread
  4. `check-modular-boundaries.ps1`, `pre-publish-gate.ps1`, and `InjectAssemblyHash.ps1` all exit 0 in CI
  5. `Muonroi.Pdf`, `Muonroi.Pdf.Abstractions`, `Muonroi.Pdf.Governance`, and `Muonroi.Pdf.Enterprise` appear in the NuGet feed at `1.0.0-alpha.N` with CPM-compliant csproj files
**Plans**: TBD

### Phase 8: v0.2 — Source Generator + AOT + DesignSystem
**Goal**: A compile-time source generator triples warm throughput, the engine publishes as a trim-safe Alpine container under 40 MB, and a default design system ships three starter templates
**Depends on**: Phase 7
**Requirements**: SG-01, SG-02, SG-03, AOT-01, AOT-02, AOT-03, DS-01, DS-02, ALLOC-01
**Success Criteria** (what must be TRUE):
  1. A source-generated `IMPdfRenderer<TModel>` implementation is emitted at compile time; the call site requires no code change vs the runtime factory path
  2. Source-generated warm throughput on the 50 KB template is ≥3× the v0.1 runtime factory baseline (BenchmarkDotNet)
  3. A `PublishAot` sample on Alpine renders the full v0.1 golden snapshot corpus with byte-identical output to the JIT path; published image is <40 MB
  4. Hot-path heap allocations are ≥30% lower than the v0.1 baseline (BenchmarkDotNet memory diagnostics)
  5. `Muonroi.Pdf.DesignSystem.Default` ships invoice, receipt, and report templates; all three pass `IPdfCssPolicy.DefaultStrict` with zero violations
**Plans**: TBD

### Phase 9: v1.0 Enterprise
**Goal**: Enterprise teams govern, version, canary-deploy, and live-preview templates through a self-service Designer; TCIS.ePort runs on the live engine with DinkToPdf removed
**Depends on**: Phase 8
**Requirements**: REG-01, REG-02, REG-03, LIC-01, LIC-02, HOT-01, HOT-02, CANARY-01, CANARY-02, CANARY-03, DESIGN-01, DESIGN-02, DESIGN-03, TCIS-01, TCIS-02, COMM-01, COMM-02
**Success Criteria** (what must be TRUE):
  1. A template published via `Muonroi.Pdf.Enterprise.Registry` propagates to all N nodes within 5 seconds via Redis hot-reload; Tenant A's publish does not invalidate Tenant B's cache
  2. A Canary rollout where SSIM score drops below the configured threshold triggers automatic rollback before 100% traffic
  3. The Designer edit-preview-publish round-trip completes in <10 s at P95; live preview is pinned to the exact engine version deployed in production
  4. TCIS.ePort renders all invoice templates via `IMPdfService` with `DinkToPdf` removed from its dependency graph and zero wkhtmltopdf CVEs in the production vulnerability scan
  5. At least 3 paid Enterprise customers are active and ARR is ≥$60k at v1.0 GA
**Plans**: TBD
**UI hint**: yes

## Progress

**Execution Order:**
Phases execute sequentially: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Abstractions + Contracts | 4/4 | Complete    | 2026-05-26 |
| 2. Parse + Cascade + Policy Gate | 5/5 | Complete    | 2026-05-26 |
| 3. Box Tree + Layout Engine | 9/9 | Complete    | 2026-05-26 |
| 4. Font + Image Pipeline | 0/TBD | Not started | - |
| 5. PDF Writer + Determinism + Security | 0/TBD | Not started | - |
| 6. DI + Telemetry + Integration | 0/TBD | Not started | - |
| 7. Golden Snapshots + CI Gates + Publishing | 0/TBD | Not started | - |
| 8. v0.2 — Source Generator + AOT + DesignSystem | 0/TBD | Not started | - |
| 9. v1.0 Enterprise | 0/TBD | Not started | - |
