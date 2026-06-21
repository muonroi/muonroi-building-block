# Roadmap: Muonroi.Pdf

> Cross-phase gap and tech-debt inventory: see `.planning/GAPS-AND-DEBT.md`

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
- [x] **Phase 8.7: Legacy Print-HTML Profile v1** — float/clear + position:absolute + table hardening + real-template corpus (completed 2026-05-28; 94% gate, HSLA_E deferred to 8.8)
- [x] **Phase 8.8: Float Child Rendering** — HSLA_E + image-in-float fix (G1+G2), root cause from `RESEARCH-HSLA-E.md` (`ContentOriginX` not propagated to float children). G1+G2 fixed; G8 (page 1 empty) deferred to 8.9 (completed 2026-05-28).
- [x] **Phase 8.9: Visual Fidelity Primitives + Pagination + Inline Flow** — G3 table grid, G7 UA-inline display, G7b mixed text+inline batching, G8 body height pagination, TD9 page count assertion. 18/18 visual gate achieved. G4+G5 deferred to 8.11 (no template demand). G9 image-in-float placeholder discovered → 8.10. Completed 2026-05-28.
- [ ] **Phase 8.10: Float Algorithm Refactor (ExcludedShapes)** — Clean-room WeasyPrint `avoid_collisions`. 6 atomic commits. Byte-identical. Foundation for nested BFC + `position:absolute`.
- [ ] **Phase 8.11: Layout Edge Cases** — Vertical-align edge, nested BFC stacks, `position:absolute` × float, page-break-inside floats, shrink-to-fit auto float, CSS `column-count` interaction. Per-template demand. TBD.
- [x] **Phase 9: v1.0 Enterprise** — Postgres template registry, Redis hot-reload, SSIM canary, web designer, TCIS cutover — CLOSED 2026-05-29; see `.planning/PHASE-09-CLOSEOUT.md`
- [x] **Phase 10: TCIS Cutover Sweep + v1.0 GA** — Full DinkToPdf/libwkhtmltox removal from TCIS (10.1-10.4) + v1.0.0 version stamp (10.6); publish (10.7-10.9) deferred to ops — CLOSED 2026-05-29; see `.planning/PHASE-10-CLOSEOUT.md`
- [x] **Phase 12: Owned CSS Cascade (B1)** — Replace AngleSharp.Css `GetComputedStyle` (beta, throws on em/rem/% headless) with an owned cascade; demote AngleSharp.Css to a parser. Retires the G14–G29 per-property fallback class. Design: `.planning/DESIGN-owned-cascade-B1.md`; spike: `.planning/SPIKE-cascade-render-device.md`
 (completed 2026-06-19)

- [x] **Phase 13: Full-HTML Running Header/Footer** — Upgrade `options.Header/Footer` from text-only to full-HTML 3-column running content (images, `HeightMm`, `ShowLine`); page numbering via `counter(page)/counter(pages)`. Plan: `.planning/PHASE-13-PLAN.md` (completed 2026-06-20)
- [x] **Phase 14: CSS Print Fidelity Gaps** — Close 3 print-oriented CSS gaps vs DinkToPdf: `@page` margin-box parsing (pure-CSS running header/footer), `linear-gradient` backgrounds (PDF axial shading), and `transform:rotate` watermark. JS/flex/grid/radial stay out of scope. Plan: `.planning/PHASE-14-PLAN.md` (completed 2026-06-20)
- [x] **Phase 15: Radial Gradients + Affine Transforms** — Extend Phase 14: `radial-gradient` backgrounds (PDF ShadingType 3, reuse axial-shading infra) + full 2D affine `transform` (translate/scale/matrix + multi-function chains, reuse CTM machinery). conic-gradient/JS/flex/grid stay out of scope; flexbox deferred to a later rendering phase. (completed 2026-06-20)
- [x] **Phase 16: PDF Enterprise ↔ Governance/ControlPlane Integration** — Deepen `Muonroi.Pdf.Enterprise` from thin v1.0 stubs into the shared Muonroi enterprise rails (real `ActivationProof` license gate, control-plane-governed templates, `Quota` metering, `Compliance` audit) — max ecosystem reuse, ZERO change to the OSS engine (SC5). (completed 2026-06-20)

- [x] **Phase 17: Monetization Rail — Enforced Quota + Usage→Billing + Subscription** — Close the monetization gap both control-plane and license-server explicitly deferred. Turn record-only metering + placeholder pricing into an enforced, billable ecosystem rail: hard quota enforcement at the control-plane API boundary (NEVER the OSS render path), usage aggregation → priced line items + invoice-preview, an `IBillingProvider` seam (record-only default; payment-processor adapter behind the seam, deferred), and subscription/renewal lifecycle in license-server. Cross-repo (building-block + control-plane + license-server); ZERO change to OSS engine (SC5). (completed 2026-06-21; verified 8/8 — 17-VERIFICATION.md)

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

### Phase 8.7: Legacy Print-HTML Profile v1 — real-template layout fidelity (open-core) [COMPLETED]

**Goal**: Render the real production template corpus faithfully by closing the layout gaps the corpus actually needs, establishing a bounded, document-oriented CSS profile + a clean CSS-decoupled layout IR + a published capability contract. Commercial open-core scope (NOT TCIS-specific). Fail-loud outside the profile; never silently mis-render.
**Depends on**: Phase 8.6 (rendering surface), Phase 8.5 (owned writer)
**Corpus / fixtures**: D:\Data\Template\Htmls\PreviewRegistion (18 templates, e.g. HSLA_E/F) vs reference filled PDFs in Downloads. Representative, NOT scope boundary.
**Scope (from 18-template census 2026-05-28 — ZERO modern CSS present)**:

  - `float:left/right` + `clear` (gap #1); `position:absolute` (gap #2)
  - Hardening: `vertical-align` in table cells (673 hits), `border-collapse:collapse`, background-color/image, `rem` units, `white-space:pre-line/pre-wrap`, `text-transform:uppercase`, `nobr`

**Out of scope (fail-loud)**: flexbox, grid, modern CSS (not in corpus); `{{...}}` templating (caller fills; library stays HTML→PDF)
**Success Criteria** (what must be TRUE):

  1. Float multi-column layout renders side-by-side (header logo|title|order-block; `wXX float-left/right` + clearfix) — no vertical-stack collapse.
  2. `position:absolute` honored relative to its containing block.
  3. Tables render correctly at corpus scale (colspan/rowspan, `vertical-align` in cells, border-collapse) including the heavy 49–55KB `*_F` files.
  4. base64 PNG + JPEG render at correct size/position; background-color fills render.
  5. Fidelity gate: all 18 templates rasterized and visually confirmed (Opus) + structurally compared against reference PDFs; large divergence = fail.
  6. Fail-loud: out-of-profile input throws a clear `PdfFormatException`/policy violation; never silent wrong output.
  7. No regression: existing suite green; new golden fixtures for the corpus.
  8. **Capability contract** published (supported layout primitives + template format) + layout IR decoupled from CSS — the seam consumed by the Phase 9 Designer (ui-engine) and registry (control-plane).

**Plans**: 8 plans

Plans:

- [x] 08.7-01-PLAN.md — Wave 1: Fix TableLayoutEngine IndexOutOfRangeException (AssignColumnIndices bounds guard) + regression test
- [x] 08.7-02-PLAN.md — Wave 1: Create LegacyPrintPolicy (float/abs-pos/border-collapse allowed; flex/grid/fixed/script still blocked)
- [x] 08.7-03-PLAN.md — Wave 1: Bundle Liberation Fonts (OFL-1.1) as EmbeddedResource; wire family-name fallback mapping in FontPipeline
- [x] 08.7-04-PLAN.md — Wave 2: border-collapse:collapse + vertical-align in TableLayoutEngine + golden tests (depends on 01+02)
- [x] 08.7-05-PLAN.md — Wave 3: float:left/right + clear BFC accumulator in BlockLayoutEngine + golden tests (depends on 02+04)
- [x] 08.7-06-PLAN.md — Wave 3: position:absolute deferred-pass in BlockLayoutEngine + golden tests (depends on 02+04; sequential after 05)
- [x] 08.7-07-PLAN.md — Wave 4: background-color/image drawing in OwnedPdfWriter + text-transform/white-space/nobr/rem in InlineLayoutEngine (depends on 05+06)
- [x] 08.7-08-PLAN.md — Wave 5: Restore real-template harness (18 fixtures, LegacyPrintPolicy, PNG rasterization) + CAPABILITY-CONTRACT.md + Opus visual gate (depends on 03+07)

**Status**: COMPLETE — 94% visual gate (17/18). HSLA_F, HBL, CAPR_E verified visually; HSLA_E A5 landscape deferred to Phase 8.8 as known-issue. 7026/7026 unit tests pass; 17/17 real-template baseline tests pass. 13 goldens re-baselined. Capability contract published. See `.planning/phase-08.7/VERIFICATION.md`. Completed 2026-05-28.

**UI hint**: no

### Phase 8.8: Float Child Rendering — HSLA_E fix (G1 + G2) [COMPLETED]

**Goal**: 18/18 real-template visual gate + logo image visible in float children. Root cause: `ContentOriginX` not propagated into float-child dispatch (same fix pattern as Fix A2 in Wave 8c, missed for float children).
**Depends on**: Phase 8.7 (all prior fixes, capability contract, real-template harness)
**Scope**:

  - G1: text/HR/block inside float uses correct X origin (`ContentOriginX = floatX + paddingLeft + borderLeft`)
  - G2: image inside float — `ImageBox` dispatch also reads `ContentOriginX`; HSLA_F logo visible

**Success Criteria** (what must be TRUE):

  1. HSLA_E renders 3-column header + customer section + table + footer; non-empty, no blank page.
  2. HSLA_F logo image visible and correctly positioned inside float column.
  3. All 17 previously-passing templates regression-clean.
  4. 18/18 visual gate; 335/335 unit tests green.

**Status**: COMPLETE — G1 + G2 fixed. 335/335 tests. G8 (HSLA_E content on page 2) discovered and deferred to 8.9 (body `height:148mm` pagination interaction). See `.planning/phase-08.8/VERIFICATION.md`. Completed 2026-05-28.

### Phase 8.9: Visual Fidelity Primitives — G8 + G7 + table grid + checkbox + form underline

**Goal**: Form-style templates structurally match reference fill PDFs. Includes G8 (HSLA_E page 1 pagination fix) and G7 (inline `<span>`/`<label>` empty display string, root cause known: `BoxTreeBuilder.cs:133`).
**Depends on**: Phase 8.8
**Scope**:

  - G8: HSLA_E body `height:148mm` pagination — page 1 empty; fix body-height interpretation so content starts on page 1
  - G7: `<span>`/`<label>` inline default empty display string — `BoxTreeBuilder.cs:133`
  - G3: `border-collapse:collapse` cells draw cell boundary grid lines (`OwnedPdfWriter` TableCellBox dispatch)
  - G4: `<input type="checkbox">` / `<input type="radio">` render as glyphs (square + X/✓), not stray text fragments — new `InputControlBox` node + writer dispatch
  - G5: `<input type="text">` renders with `border-bottom` underline — new `InputFieldBox` node
  - TD1: add `[Skip]` to `HslaERootCauseDiagnostic.cs` or repurpose as permanent assertion
  - TD6: `ContentOriginX > 0f` sentinel — replace with `HasValue` check
  - TD8: harden PNG 1×1 12-byte IDAT edge case in engine path
  - TD9: extend `VisualRegressionTests` / `RealTemplateBaselineTests` to rasterize all pages or assert page count

**Success Criteria** (what must be TRUE):

  1. HSLA_E content renders on page 1 (G8 closed); 18/18 visual gate achieved.
  2. All real templates with `border-collapse:collapse` show grid lines.
  3. Checkboxes render as glyphs; stray `×` fragment gone.
  4. Text inputs render with bottom underline.
  5. 335+ unit tests + new tests pass; no regression on HSLA_F/HBL/CAPR_E.

**Note**: G4+G5 deferred to 8.11 (research audit revealed 0 templates use `<input>`). G7b (mixed text+inline batching) discovered post-G7 and fixed in same phase. G9 (image-in-float red placeholder) discovered during visual review → 8.10.
**Status**: COMPLETE — TD9 (`e95db78`), G8 (`0b5ca9b`), G7 (`0542d76`), G3 (`2ca4830`), G7b (`df229b8`). 18/18 visual gate. 363/363 tests. See `.planning/phases/08.9-fidelity/VERIFICATION.md`. Completed 2026-05-28.

### Phase 8.10: Float Algorithm Refactor (ExcludedShapes)

**Goal**: Replace cursor-based float positioning (`LeftFloatRight`/`RightFloatLeft` scalar fields) with a correct ExcludedShapes list query (WeasyPrint clean-room `avoid_collisions`). Pure algorithmic refactor — byte-identical PDF output. Foundation for nested BFC stacks and `position:absolute` × float.
**Depends on**: Phase 8.9
**Scope**: 6 atomic commits (see `.planning/phase-08.10/PLAN.md`):

  1. Add `FloatSide` / `FloatExclusion` / `FloatPlacementSolver` stubs
  2. Implement solver + 14 synthetic unit tests
  3. Mirror cursor writes into Exclusions list (cursor still authoritative)
  4. Flip all reads to FloatPlacementSolver (cursors write-only)
  5. Remove cursor fields; Exclusions is sole source of truth
  6. Add `clear:left/right/both` tests + verify `ClearY` behavior

**Success Criteria** (what must be TRUE):

  1. Float positioning uses `FloatExclusion` list query; cursor fields removed.
  2. `FloatPlacementSolverTests` covers ≥10 synthetic scenarios incl. multi-row, mixed-side, clear.
  3. Byte-identical PDF goldens at steps 3–5.
  4. 7026+ unit tests + ≥14 new solver tests pass.

**Status**: Planned 2026-05-28

### Phase 8.11: Layout Edge Cases

**Goal**: Close remaining layout edge cases surfaced by real templates and CSS 2.1 compliance work. All items are per-template demand — implement as needed.
**Depends on**: Phase 8.10
**Scope (TBD — items listed by priority):**

  - Vertical-align edge cases (non-middle/top/bottom in mixed inline contexts)
  - Nested BFC stacks (`overflow:hidden`, `inline-block` float isolation)
  - `position:absolute` × float interaction
  - Page-break-inside floats (float straddles page boundary)
  - Shrink-to-fit `width:auto` floats (min-content pre-pass)
  - CSS `column-count` × float interaction (low priority, not in v1 corpus)

**Status**: Planned 2026-05-28

### Phase 9: v1.0 Enterprise — multi-repo workstreams (no new repos)

**Goal**: Enterprise teams govern, version, canary-deploy, and live-preview PDF templates through a self-service Designer; TCIS.ePort runs on the live engine with DinkToPdf removed. PDF becomes a second product line riding the EXISTING open-core SaaS rails (see ecosystem topology) — reusing license-server, control-plane, and ui-engine; NO new repos.
**Depends on**: Phase 8.7 (capability contract + layout IR is the Designer/registry seam)
**Requirements**: REG-01, REG-02, REG-03, LIC-01, LIC-02, HOT-01, HOT-02, CANARY-01, CANARY-02, CANARY-03, DESIGN-01, DESIGN-02, DESIGN-03, TCIS-01, TCIS-02, COMM-01, COMM-02
**Cross-repo workstreams** (each verifies independently; E2E integration last):

  - **WS-A — building-block** (C# runtime): `Muonroi.Pdf.Enterprise` (registry client, Redis hot-reload subscriber, capability gates via `EnsureFeatureOrThrow`); pure-managed SSIM scorer; PDF capability keys (`pdf.designer`, `pdf.registry`, `pdf.canary`).
  - **WS-B — control-plane** (private SaaS): add "PDF templates" domain to `ControlPlane.Api` (registry/versioning/maker-checker approval/hot-reload/audit) REUSING the existing ruleset infra; PDF canary quality-gate invoking the SSIM scorer; host the Designer app in `control-plane-dashboard`.
  - **WS-C — ui-engine** (frontend): new commercial component `@muonroi/ui-engine-pdf-designer` (`MuPdfTemplateDesignerReact`), mirroring `rule-components`/`MuRuleFlowDesignerReact`; emits only capability-contract-valid layout.
  - **WS-D — license-server** (private SaaS): PDF commercial entitlements in the RSA-signed ActivationProof (issue/revoke).
  - **TCIS cutover**: lives in TCIS.ePort repo (outside these four); consumes `IMPdfService` + the registry.

**Success Criteria** (what must be TRUE):

  1. A template published via `Muonroi.Pdf.Enterprise.Registry` propagates to all N nodes within 5 seconds via Redis hot-reload; Tenant A's publish does not invalidate Tenant B's cache
  2. A Canary rollout where SSIM score drops below the configured threshold triggers automatic rollback before 100% traffic
  3. The Designer edit-preview-publish round-trip completes in <10 s at P95; live preview is pinned to the exact engine version deployed in production
  4. TCIS.ePort renders all invoice templates via `IMPdfService` with `DinkToPdf` removed from its dependency graph and zero wkhtmltopdf CVEs in the production vulnerability scan
  5. At least 3 paid Enterprise customers are active and ARR is ≥$60k at v1.0 GA

**Plans**: TBD (plan per workstream after 8.7 closes)
**UI hint**: yes

### Phase 12: Owned CSS Cascade (B1)

**Goal**: The engine resolves computed styles via an owned cascade — `AngleSharp.Css.GetComputedStyle` is never called. AngleSharp.Css is demoted to CSS parsing (rules + `@page` + `@font-face`); AngleSharp core handles selector matching. The G14–G29 BoxTreeBuilder fallbacks become redundant.
**Depends on**: Phase 8.x (current cascade seam), and the committed G25/G27/G28/G29 fixes (branch `fix/pdf-legacy-print-cascade-gaps`).
**Design**: `.planning/DESIGN-owned-cascade-B1.md` (full architecture, components, cascade algorithm, migration phases B1.1/B1.2/B1.3, risks, test strategy). Spike verdict (A dead): `.planning/SPIKE-cascade-render-device.md`.
**Scope (this plan = B1.1 only)**:

  - Implement `CssRuleSet`, `CascadeResolver`, `OwnedComputedStyle`, `OwnedStyledNode` in `src/Muonroi.Pdf.Governance/Cascade/`.
  - Wire `AngleSharpStyledDocument` to the owned cascade; never call `GetComputedStyle`.
  - Keep BoxTreeBuilder fallbacks as belt-and-suspenders (delete in B1.3).
  - Run full golden suite; re-baseline ONLY `%`-table cases (visually verified); simple-doc goldens stay byte-identical.

**Out of scope**: B1.2 (policy migration off `GetComputedStyle`), B1.3 (delete G14–G29 fallbacks).
**Success Criteria** (what must be TRUE):

  1. No code path calls `IWindow.GetComputedStyle` / `ComputeCurrentStyle`; the `catch (ArgumentException)` in `AngleSharpStyledNode` is gone.
  2. The owned cascade resolves the Profile v1 property surface (display, box-model longhands, text/font, float/clear/position, white-space, word-break, table props) with selector specificity + `!important` + inheritance + em/rem→px; `%` left literal for layout.
  3. Descendant selectors (`.table-bodered2 tr.no-border td`), shorthand expansion (`padding: 2px 6px`), and inheritance behave per the G25/G27/G28/G29 regression tests — now satisfied by the cascade, not fallbacks.
  4. Full `Muonroi.Pdf.Tests` suite green; only `%`-table goldens re-baselined (visually verified); determinism canary unaffected.

**Plans**: 4 plans (B1.1)

Plans:

- [x] 12-01-PLAN.md — CssRuleSet: collect author rules once, split grouped selectors, specificity + source order + declarations
- [x] 12-02-PLAN.md — CascadeResolver + OwnedComputedStyle: match/sort/inline-overlay/shorthand/UA-defaults/inheritance/em-rem-to-px (incl. G25/G27/G28/G29)
- [x] 12-03-PLAN.md — OwnedStyledNode + wire AngleSharpStyledDocument; remove GetComputedStyle catch path
- [x] 12-04-PLAN.md — Full golden suite + %-table re-baseline (visual checkpoint) + determinism canary

### Phase 15: Radial Gradients + Affine Transforms

**Goal**: Extend the Phase 14 writer-level CSS features — render `radial-gradient(...)` backgrounds via PDF radial shading and generalize `transform` from single `rotate()` to the full 2D affine set (translate/scale/matrix + multi-function chains) — reusing the axial-shading and CTM infrastructure built in Phase 14. Additive; no golden re-baseline expected.
**Depends on**: Phase 14 (axial shading ShadingType 2 + CTM helpers RotMatrix/AppendCm + RotationGroup; shipped develop commit 3dfb7842)
**Scope**:

  - `radial-gradient`: parse + emit PDF ShadingType 3 (radial); narrow LegacyPrintPolicy to also allow radial-gradient. conic-gradient + repeating-* stay rejected.
  - `transform` affine: translate/translateX/translateY, scale/scaleX/scaleY, matrix(), and multi-function chains composed into a single CTM; widen the LegacyPrintPolicy transform gate (currently single-rotate only).
  - Open questions for discuss: skew() in/out; transform-origin support (Phase 14 pivots about box-center).

**Out of scope**: conic-gradient, JavaScript, flexbox (deferred to Phase 16), CSS grid.
**Success Criteria** (what must be TRUE):

  1. `background: radial-gradient(...)` renders as a PDF radial shading; `conic-gradient`/`repeating-*` still throw a policy violation.
  2. `transform: translate()/scale()/matrix()` and multi-function chains render correctly composed; unsupported transform functions still throw.
  3. Full Muonroi.Pdf suite green; existing 17 TCIS templates byte-unchanged; PerfGate within ceilings (cold<=1500ms/warm<=400ms).

**Plans**: 2 plans

Plans:
**Wave 1**

- [x] 15-01-PLAN.md — Wave 1: full 2D affine transform (TransformGroup matrix carrier + TryParseTransformMatrix compose + widened policy gate + writer generalization + transform test flips)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 15-02-PLAN.md — Wave 2: radial-gradient (RadialGradient model+parser, BuildRadialShadingDict ShadingType 3 + ellipse anisotropic cm, gradient gate widening, radial test flips; depends on 15-01 via shared files)

### Phase 16: PDF Enterprise ↔ Governance/ControlPlane Integration

**Goal**: Deepen `Muonroi.Pdf.Enterprise` from the thin v1.0 stubs (Phase 9) into the shared Muonroi enterprise rails so the PDF product line inherits licensing, anti-tamper, audit/compliance, quota and SLO for free — maximizing ecosystem reuse, with ZERO changes to the OSS engine (`Muonroi.Pdf`).
**Depends on**: Phase 9 (v1.0 Enterprise WS-A..D scaffolding); `Muonroi.Governance.Enterprise` (License/ControlPlane/Compliance/Operations); the control-plane + license-server repos.
**Scope** (cross-repo workstreams, mirror Phase 9 A–D; no new repos):
  - **WS-A — building-block**: `Muonroi.Pdf.Enterprise` references `Muonroi.Governance.Enterprise`; replace `AlwaysAllowFeatureGate` with a real `IFeatureGate` bound to RSA `ActivationProof` + `MEnterpriseFailClosedMatrix`; wire `pdf.designer/registry/canary` keys; per-tenant render metering via `Muonroi.Quota`; emit publish/render events into the `Compliance` evidence pack + `AuditTrail`.
  - **WS-B — control-plane**: model PDF templates as a governed domain REUSING the ruleset registry/versioning/maker-checker/approval/audit infra; SSIM canary quality gate.
  - **WS-C — ui-engine**: extend the PDF Designer to be entitlement-aware (capability-gated UI).
  - **WS-D — license-server**: confirm/extend PDF commercial entitlements in the signed ActivationProof.
**Out of scope**: any change to `Muonroi.Pdf` (OSS) — the one-way Enterprise→OSS boundary (SC5) is inviolable; new repos; flexbox / rendering-engine work (separate track).
**Success Criteria** (what must be TRUE):
  1. `Muonroi.Pdf.Enterprise.EnsureFeatureOrThrow` runs on a REAL ActivationProof: an unlicensed `pdf.*` capability throws `FeatureNotLicensedException`, a licensed one passes; `Muonroi.Pdf` (OSS) still references nothing under `*.Enterprise`.
  2. PDF template publish/version/approve flows through the existing control-plane governance (reused, not duplicated); a publish propagates per-tenant with no cross-tenant cache invalidation.
  3. Per-tenant PDF render metering is recorded via `Muonroi.Quota`; the compliance evidence pack includes PDF publish/render audit events.
  4. Full `Muonroi.Pdf.Tests` + governance suites green; the OSS engine stays byte-identical (no golden re-baseline).
**Plans**: 5 plans
- [x] 16-01-PLAN.md — WS-A D-01: real LicenseFeatureGate + pdf.* in all four governance registries (Wave 1)
- [x] 16-02-PLAN.md — WS-A D-02: record-only per-render Quota metering wrapper (Wave 2)
- [x] 16-03-PLAN.md — WS-B D-03: PdfAuditControlPlaneStore compliance evidence-pack adapter (Wave 1)
- [x] 16-04-PLAN.md — WS-B D-04: canary score endpoint auto-rollback below SSIM threshold (Wave 1)
- [x] 16-05-PLAN.md — WS-C entitlement-aware Designer gate + WS-D confirm + SC4 green gate (Wave 3)

### Phase 17: Monetization Rail — Enforced Quota + Usage→Billing + Subscription

**Goal**: Close the monetization gap that `muonroi-control-plane` and `muonroi-license-server` both explicitly deferred (verified: control-plane has zero `Billing/Invoice/Payment/Stripe/Subscription`; `QuotaEnforcementMiddleware` exists in building-block but is never registered in the control-plane host; `PricingEndpoints` carries placeholder prices; license-server `09.4-ws-d-license-pdf/PLAN.md:34` defers *"Billing / metering ARR for PDF tier"*). Turn the record-only metering + placeholder pricing into an **enforced, billable rail** reused across every product line — without touching the OSS engine.

**Depends on**: Phase 16 (real license gate + `Muonroi.Quota` metering + `Compliance`); existing `QuotaEnforcementMiddleware`, `PricingEndpoints`, license-server entitlement model.

**Scope** (cross-repo workstreams; no new repos):
  - **WS-A — building-block**: billing seam in a new `Muonroi.Billing.Abstractions` (`IBillingProvider`, `UsageLineItem`, `IUsageAggregator`, `PricingPlan`); a record-only default `IBillingProvider` (logs, never calls out); usage rollup over `ITenantQuotaStore` events; source `MaxPdfRendersPerDay` (and other quota limits) from the licensed tier instead of `int.MaxValue`. NO change to `Muonroi.Pdf` (OSS).
  - **WS-B — control-plane**: register `UseQuotaEnforcement()` in the host with real per-tier limits; turn `PricingEndpoints` placeholders into a real `PricingPlan` model; add a usage-aggregation + **invoice-preview** endpoint that prices a tenant's metered usage for a period; wire the record-only `IBillingProvider`.
  - **WS-D — license-server**: subscription record + **renewal endpoint** + expiry/grace lifecycle (no more manual re-issue only); expose tier→quota-limit mapping consumed by control-plane/building-block.

**Out of scope** (deliberate): live payment-processor (Stripe) integration — the adapter lives behind `IBillingProvider` and is **stubbed/deferred** (no external dependency at build or test time); dunning, tax, multi-currency, proration math beyond simple per-unit pricing; ANY change to `Muonroi.Pdf` (OSS) — the one-way Enterprise→OSS boundary (SC5) is inviolable; new repos.

**Success Criteria** (what must be TRUE):

  1. A tenant exceeding its per-tier limit at the **control-plane API boundary** gets HTTP 429 via the now-registered `UseQuotaEnforcement()`; the OSS `IMPdfService.RenderAsync` path is still **never** blocked (SC5 preserved — enforcement sits at the API/enterprise layer, not in the engine).
  2. Per-tenant metered usage aggregates into priced `UsageLineItem`s via a real `PricingPlan`; an **invoice-preview** endpoint returns the computed amount for a billing period (replacing `PricingEndpoints` placeholders).
  3. `IBillingProvider` seam exists with a **record-only** default impl (No Silent Catch on provider failure); the payment-processor adapter is behind the seam and is NOT required to build or test (zero external dependency at build time).
  4. license-server exposes a **subscription + renewal** lifecycle (renew endpoint, expiry/grace, tier→limit mapping); manual key re-issue is no longer the only renewal path.
  5. Full `Muonroi.Pdf.Tests` + governance + control-plane + license-server suites green; the OSS engine stays byte-identical (no golden re-baseline).

**Requirements**: MON-01, MON-02, MON-03, MON-04, MON-05, MON-06, MON-07, MON-08
**Plans**: 5 plans (4 waves) — COMPLETE 2026-06-21; verified 8/8 (17-VERIFICATION.md)

Plans:
- [x] 17-01-PLAN.md — Muonroi.Billing.Abstractions seam + record-only IBillingProvider (building-block)
- [x] 17-02-PLAN.md — IUsageAggregator impl + tier-sourced quota limits (building-block)
- [x] 17-03-PLAN.md — register UseQuotaEnforcement() + invoice-preview + PricingPlan pricing (control-plane)
- [x] 17-04-PLAN.md — subscription + renewal lifecycle + tier->limit mapping (license-server)
- [x] 17-05-PLAN.md — cross-repo green + SC5 byte-identical OSS + no-billing-leak guard

**UI hint**: no

## Progress

**Execution Order:**
Phases execute sequentially: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 8.5 → 8.6 → 8.7 → 8.8 → 8.9 → 8.10 → 8.11 → 9

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
| 8.7. Legacy Print-HTML Profile v1 (float/clear + abs-pos + hardening) | 8/8 | Complete (94% gate, HSLA_E deferred to 8.8) | 2026-05-28 |
| 8.8. Float Child Rendering (HSLA_E + image-in-float) | 1/1 | Complete (G8 deferred to 8.9) | 2026-05-28 |
| 8.9. Visual Fidelity Primitives (table grid + checkbox + form underline) | 0/TBD | Planned | - |
| 8.10. Float Algorithm Refactor (ExcludedShapes) | 0/TBD | Planned | - |
| 8.11. Layout Edge Cases | 0/TBD | Planned | - |
| 9. v1.0 Enterprise (multi-repo workstreams A–D) | 0/TBD | Not started | - |
| 12. Owned CSS Cascade (B1) | 4/4 | Complete    | 2026-06-19 |
