# Roadmap: Muonroi.Pdf

## Overview

Nine phases deliver a pure-managed HTML/CSS-to-PDF renderer from zero to enterprise-grade. Phases 1–7 ship the v0.1 OSS engine: public contracts first, then pipeline stages in strict dependency order (parse → cascade → layout → font/image → write), locked by a golden snapshot corpus and CI gates before any NuGet publish. Phase 8 hardens v0.2 with compile-time source generation, AOT trim-safety, and allocation reduction. Phase 9 builds the commercial Enterprise tier on the stable OSS core.

## Phases

- [x] **Phase 1: Abstractions + Contracts** — Define all public API contracts and adapter seams in `Muonroi.Pdf.Abstractions` (netstandard2.0); zero implementation code (completed 2026-05-26)
- [x] **Phase 2: Parse + Cascade + Policy Gate** — Wire AngleSharp HTML parsing, AngleSharp.Css cascade, and `IPdfCssPolicy.DefaultStrict` in `Muonroi.Pdf.Governance` (completed 2026-05-26)
- [x] **Phase 3: Box Tree + Layout Engine** — Hand-written box tree with block/inline formatting, table layout, pagination, and page counters (completed 2026-05-26)
- [x] **Phase 4: Font + Image Pipeline** — `IFontResolver` integration, Vietnamese diacritic shaping via SixLabors.Fonts, PNG/JPEG/data-URI image decoding (completed 2026-05-27)
- [x] **Phase 5: PDF Writer + Determinism + Security** — PdfSharpCore writer adapter hardened to PDF 1.7 with deterministic IDs and JS/Launch/EmbeddedFile rejection (completed 2026-05-27)
- [ ] **Phase 6: DI + Telemetry + Integration** — `AddPdf()` DI registration, OpenTelemetry instrumentation, end-to-end `IMPdfService.RenderAsync()` integration
- [ ] **Phase 7: Golden Snapshots + CI Gates + Publishing** — 40+ golden tests, Vietnamese corpus, convention gates, NuGet publish at `1.0.0-alpha.N`
- [x] **Phase 8: v0.2 — Source Generator + AOT + DesignSystem** — Compile-time `IMPdfRenderer<T>` fast path, trim-safe Alpine container, default design system templates (completed 2026-05-27; SC4 alloc target deferred to Phase 8.5)
- [x] **Phase 8.5: Owned PDF Writer (SC4 carry-over)** — Replaced PdfSharpCore with an owned, allocation-controlled, AOT-trivial PDF 1.7 writer (CID Type0/Identity-H + ToUnicode, FlateDecode via ZLibStream, JPEG/PNG XObjects). Closes ALLOC-01/SC4: total alloc 51.62 MB vs 288.96 threshold (82% headroom). PdfSharpCore fully removed (+ transitives ImageSharp/CodePages). 215/215 tests, 56 snapshots re-baselined. Verified 7/7 (08.5-VERIFICATION.md). Completed 2026-05-27.
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
- [x] 04-01-PLAN.md — FontFaceDeclaration + IStyledDocument.FontFaces (Abstractions) + AngleSharpStyledDocument implementation (Governance)
- [x] 04-02-PLAN.md — SixLabors.Fonts csproj ref + EmbeddedFontInfo + SixLaborsTextMetrics + GlyphCollector
- [x] 04-03-PLAN.md — DataUriDecoder + PureImageDecoder (PNG IHDR + JPEG SOF) + ImagePipeline async pre-pass
- [x] 04-04-PLAN.md — TrueTypeFontSubsetter (TTF binary subsetter) + FontPipeline async orchestrator
- [x] 04-05-PLAN.md — PositionedPageList extended + BoxTreeBuilder resolvedImages + LayoutEngine.LayoutAsync wiring
- [x] 04-06-PLAN.md — Unit tests: FontPipelineTests, VietnameseDiacriticTests, ImagePipelineTests, TrueTypeFontSubsetterTests

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
**Plans**: 3 plans

Plans:
- [x] 05-01-PLAN.md — PdfSecurityException + ThrowingResourceResolver + <script> policy rejection + PdfSharpCore csproj ref
- [x] 05-02-PLAN.md — PdfSharpFontResolverAdapter + PdfSharpCoreWriter (text, image, font, determinism, security hardening)
- [x] 05-03-PLAN.md — Tests: PdfWriterTests, DeterminismTests, SecurityTests (≥16 new tests)

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
**Plans**: 5 plans

Plans:
- [x] 07-01-PLAN.md — Golden harness (hand-rolled byte comparer) + corpus registry + determinism canary + block-layout first batch + .gitattributes
- [x] 07-02-PLAN.md — Remaining structural corpus (inline/table/paged-media/image/font/security) to reach 40+ cases
- [x] 07-03-PLAN.md — Vietnamese corpus (10+) + glyph-coverage guard + finalize KNOWN-DEVIATIONS.md
- [x] 07-04-PLAN.md — Informational perf gate (cold/warm, generous CI ceiling, skippable, SlowIntegration)
- [ ] 07-05-PLAN.md — Gate scripts green + pack 4 .nupkg at 1.0.0-alpha.N + PKG-05 meta-package & PKG-06 OSS-BOUNDARY

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
**Plans**: 08-01..08-05 (complete; SC4 carried to Phase 8.5)
**Note**: SC1/SC2/SC3/SC5 met. SC4 (≥30% alloc reduction) deferred — per-stage profiling showed
the writer (PdfSharpCore `DrawString`, per-word) is 92% of allocations; the localized text-metrics
+ XFont caches landed but cannot reach 30%. Resolution is a strategic writer rebuild (Phase 8.5).

### Phase 8.5: Owned PDF Writer (SC4 carry-over)
**Goal**: Own the final PDF-serialization layer with an allocation-controlled, deterministic,
AOT-trivial PDF 1.7 writer that emits content streams (`TJ` per line with precomputed glyph IDs +
advances) from the positioned glyphs + subset font bytes the engine already produces — eliminating
the per-word `DrawString` pipeline that dominates render allocations.
**Depends on**: Phase 8
**Requirements**: ALLOC-01 (SC4 carry-over)
**Success Criteria** (what must be TRUE):
  1. A 2–3 day spike proves a `TJ`-per-line content-stream emitter cuts WRITE-stage allocations
     enough to bring total render allocation ≥30% below the 412.8 MB v0.1 baseline (BenchmarkDotNet)
  2. The owned writer produces valid PDF 1.7 with deterministic /ID and font-subset prefixes
     (folding in the current `NormalizeForDeterminism` behavior natively)
  3. All golden snapshots are re-baselined and reviewed; Vietnamese diacritic corpus still passes
  4. PdfSharpCore is removed from the core render path (or retained only behind an adapter seam)
**Fallback**: migrate to upstream empira PDFsharp 6.x (MIT, net8) if the owned-writer spike misses. (Not needed — owned writer hit SC4 with 82% headroom.)
**Plans**: 08.5-01..07 (complete) — branch `phase/08.5-owned-pdf-writer`, commit 3ea4348
**Status**: COMPLETE — all 4 success criteria met; verified 7/7 (08.5-VERIFICATION.md, Opus, independently reproduced). Measured SC4 total 51.62 MB. PdfSharpCore removed. Non-blocking: external PDF validator (qpdf/veraPDF) pass recommended before GA.

### Phase 8.6: Rendering Fidelity — close CSS/HTML5/font/image gaps (tech-debt paydown)
**Goal**: Eliminate the silent-drop rendering gaps surfaced by the Phase 8.5 capability audit so they do not harden into tech debt before v1.0. Parsed-but-not-rendered CSS and unsupported inputs should either render correctly or fail loudly under policy — never silently produce wrong output.
**Depends on**: Phase 8.5 (owned writer is the rendering surface these gaps live in)
**Requirements**: FIDELITY-01..12
**Scope decided (interview 2026-05-27)**: 3 clusters below. Driver = general coverage before v1.0 (not TCIS-specific). Cross-cutting philosophy = **fail-loud**: any unsupported input (OTF-CFF, PNG RGBA, unknown CSS) must throw a clear `PdfFormatException`/policy violation — NEVER silently emit wrong output.
**Out of scope (deliberate, follow-up candidate)**: `background-color` + `border` drawing (parsed-but-not-drawn) — still the largest visual gap; not included this phase.
**Success Criteria** (what must be TRUE):
  1. **Text layout fidelity** — `text-align` (left/right/center/justify) and `line-height` are honored by the layout engine; `text-decoration` (underline/strikethrough) is drawn by the writer. Golden coverage added.
  2. **HTML5 semantics** — `<br>`/`<hr>` break/rule, `<ul>/<ol>/<li>` markers, and `<a>` link annotations render; anything not implemented is explicitly policy-gated with a clear reason, never silently dropped.
  3. **Font + image robustness (fail-loud)** — OTF-CFF (`.otf` PostScript-outline) fonts either subset+embed correctly OR are rejected with a clear `PdfFormatException` (NO silent Latin-1 fallback that corrupts Vietnamese/Unicode); WOFF/WOFF2 decision documented. PNG alpha/palette/grayscale handled or cleanly rejected; the "8-bit RGB only" boundary enforced loudly.
  4. No regression: full suite green; golden snapshots re-baselined for any intentional visual change.
**Fallback**: if budget tightens, prioritize SC3 (font/image fail-loud safety) and SC1 (text layout); SC2 HTML5 semantics rolls to a follow-up.
**Plans**: 6 plans

Plans:
- [x] 08.6-01-PLAN.md — Cluster 3: TrueTypeFontSubsetter OTF-CFF throw + WOFF explicit message; PureImageDecoder PNG variant gate at decode boundary (FIDELITY-08..11)
- [x] 08.6-02-PLAN.md — Box model contracts: BoxNode.TextAlign, InlineBox.LineHeightFactor/TextDecoration/LinkHref, LineBreakBox, HrBox, PositionedPage.LinkAnnotations (foundation for Plans 03+04)
- [x] 08.6-03-PLAN.md — Layout engine: BoxTreeBuilder CSS reading + HTML5 element dispatch + list markers + href filter; InlineLayoutEngine text-align/line-height/br/link-rects; BlockLayoutEngine hr reservation (FIDELITY-01..07, FIDELITY-12)
- [x] 08.6-04-PLAN.md — Writer: OwnedPdfWriter text-decoration drawing + hr drawing + /Annots link annotations (FIDELITY-01, 03, 05, 07)
- [x] 08.6-05-PLAN.md — Policy: DefaultStrictPolicy javascript:/file: href scheme gate (FIDELITY-12)
- [x] 08.6-06-PLAN.md — Tests: FontRejectionTests, ImageRejectionTests, PolicyTests, FidelityGoldenTests (7 cases), Html5SemanticsGoldenTests (5 cases); full suite green + human visual verify

**Status**: COMPLETE — 12/12 success criteria verified goal-backward (08.6-VERIFICATION.md, Opus, independent). 275/275 tests pass, SEC-02 clean (0 non-comment forbidden actions). A rasterization visual gate (VisualRegressionTests.cs, PDFtoImage) was added after the original 6 plans exposed blank/overlap/missing-decoration regressions through byte-only tests; debug rounds 60d5a5e→2dde780 fixed those and 1f1a362 closed the residual empty-string style-clobber class (display/text-align/vertical-align/table-layout). Underline + list-marker output visually confirmed by Opus. Out of scope (carried forward): background-color + border drawing. Completed 2026-05-27.

**UI hint**: no

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
Phases execute sequentially: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 8.5 → 8.6 → 9

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Abstractions + Contracts | 4/4 | Complete    | 2026-05-26 |
| 2. Parse + Cascade + Policy Gate | 5/5 | Complete    | 2026-05-26 |
| 3. Box Tree + Layout Engine | 9/9 | Complete    | 2026-05-26 |
| 4. Font + Image Pipeline | 6/6 | Complete    | 2026-05-27 |
| 5. PDF Writer + Determinism + Security | 0/3 | Not started | - |
| 6. DI + Telemetry + Integration | 0/TBD | Not started | - |
| 7. Golden Snapshots + CI Gates + Publishing | 4/5 | In Progress|  |
| 8. v0.2 — Source Generator + AOT + DesignSystem | 5/5 | Complete (SC4 deferred to 8.5) | 2026-05-27 |
| 8.5. Owned PDF Writer (SC4 carry-over) | 7/7 | Complete (SC4 closed, PdfSharpCore removed) | 2026-05-27 |
| 8.6. Rendering Fidelity (CSS/HTML5/font/image gaps) | 6/6 | Complete (12/12 SC, visual gate, verified Opus) | 2026-05-27 |
| 9. v1.0 Enterprise | 0/TBD | Not started | - |
