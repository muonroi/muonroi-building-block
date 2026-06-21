# Requirements: Muonroi.Pdf

**Defined:** 2026-05-26
**Core Value:** A .NET team can render a deterministic, policy-enforced PDF from HTML+CSS in a single `AddPdf()` call, on any OS, with no native binary, no browser engine, and no outbound network — and get the same byte-for-byte output every time.

---

## v0.1 Requirements — OSS Engine (target: M+5)

### Package Structure

- [x] **PKG-01**: `Muonroi.Pdf.Abstractions` project exists targeting `netstandard2.0` with zero implementation code — public contracts only
- [ ] **PKG-02**: `Muonroi.Pdf` project exists targeting `net8.0` with engine implementation and DI registration in namespace `Muonroi.Pdf.Extensions`
- [x] **PKG-03**: `Muonroi.Pdf.Governance` project exists targeting `net8.0` with CSS policy enforcement and signed config verification
- [ ] **PKG-04**: `Muonroi.Pdf.Enterprise` project exists as an empty stub targeting `net8.0`, with `<IsCommercialPackage>true</IsCommercialPackage>` in its csproj, locking the namespace and assembly hash pipeline
- [ ] **PKG-05**: `Muonroi.BuildingBlock.All` meta-package includes `Muonroi.Pdf`, `Muonroi.Pdf.Abstractions`, and `Muonroi.Pdf.Governance`
- [ ] **PKG-06**: `OSS-BOUNDARY.md` allowlist updated to include the three OSS Pdf packages
- [ ] **PKG-07**: All packages publish at version `1.0.0-alpha.N` via NuGet; no per-csproj `Version` attributes (CPM compliance)

### Public Contracts (`Muonroi.Pdf.Abstractions`)

- [x] **ABST-01**: `IMPdfService` interface defined with three overloads: (1) `RenderAsync(string html, Stream destination, PdfRenderOptions options, CancellationToken ct)` — primary stream-destination overload, avoids buffering; (2) `RenderMultiPageAsync(IReadOnlyList<string> htmlPages, Stream destination, PdfRenderOptions options, CancellationToken ct)` — merges fragments into one PDF; (3) `RenderToBytesAsync(string html, PdfRenderOptions options, CancellationToken ct) : Task<(byte[] Bytes, PdfRenderResult Metadata)>` — convenience overload for callers that need bytes directly
- [x] **ABST-02**: `IMPdfRenderer<TModel>` interface defined with `string TemplateId { get; }` and `RenderAsync(TModel model, Stream destination, PdfRenderOptions? options, CancellationToken ct) : Task<PdfRenderResult>`; the renderer writes directly to the destination stream and returns metadata
- [x] **ABST-03**: `IMPdfRendererFactory` interface defined with `Get<TModel>(string templateId) : IMPdfRenderer<TModel>` (throws `KeyNotFoundException` when unknown) and `TryGet<TModel>(string templateId, out IMPdfRenderer<TModel>? renderer) : bool`; resolves renderers by template id
- [x] **ABST-04**: `IPdfCssPolicy` interface defined with `string Id { get; }` (stable policy identifier for telemetry), `PdfPolicyLimits Limits { get; }` (hard numerical limits enforced before parsing), and `ValidateAsync(IPdfDocumentContext documentContext, CancellationToken ct) : ValueTask<PolicyValidationResult>`; `IPdfDocumentContext` exposes `ElementCount`, `MaxDepth`, `TotalStylesheetBytes`, `SourceHtmlBytes` — opaque to callers, produced by the parse/cascade stage
- [x] **ABST-05**: `IResourceResolver` interface defined with `ResolveAsync(Uri uri, string? contentTypeHint, CancellationToken ct) : ValueTask<ResourceResult?>` — bytes-only; companion record `ResourceResult(ReadOnlyMemory<byte> Bytes, string ContentType)`; `Uri` type (not `string`) prevents string-concatenation path-traversal; returns `null` when forbidden or not found
- [x] **ABST-06**: `IFontResolver` interface defined with `ResolveAsync(FontRequest request, CancellationToken ct) : ValueTask<ReadOnlyMemory<byte>?>` — bytes-only, returns `null` when no match; companion `FontRequest(string Family, FontWeight Weight = Normal, FontStyle Style = Normal)` includes Weight for bold/semibold variant resolution; `FontWeight` and `FontStyle` enums defined in Abstractions
- [x] **ABST-07**: `ICssCascadeEngine` interface defined as adapter seam over AngleSharp.Css, enabling future swap in one class
- [x] **ABST-08**: `IHtmlParser` interface defined as adapter seam over AngleSharp, enabling future swap in one class
- [x] **ABST-09**: `IImageDecoder` interface defined with `Decode(ReadOnlySpan<byte> data) : DecodedImage` — adapter seam
- [x] **ABST-10**: `IPdfWriter` interface defined as adapter seam over PdfSharpCore, enabling future writer swap
- [x] **ABST-11**: `PdfRenderOptions` record/class defined with: page size (A4/A5/Letter/Legal), orientation, margin overrides, resource resolver reference, font resolver reference, css policy reference
- [x] **ABST-12**: `PdfRenderResult` record defined as metadata-only: `PageCount : int`, `ByteCount : long`, `Elapsed : TimeSpan`, `TemplateHash : string`, `PolicyId : string`, `Diagnostics : IReadOnlyList<PolicyViolation>`; PDF bytes are written directly to the caller-supplied `Stream destination` on `IMPdfService.RenderAsync` — the result type carries no content (avoids buffering large documents in memory)
- [x] **ABST-13**: `PdfConfigs` options class defined with `SectionName = "PdfConfigs"` (flat, no colon path), containing a nested `Limits` object
- [x] **ABST-14**: `PdfConfigs.Limits` defines all six hard limits: `MaxHtmlBytes = 8_388_608` (8 MB), `MaxDomDepth = 256`, `MaxElementCount = 100_000`, `MaxImagePixels = 25_000_000`, `MaxPages = 1000`, `MaxRenderDurationMs = 15_000`, `MaxFontFiles = 32`

### DI Registration

- [ ] **DI-01**: `AddPdf(IServiceCollection, IConfiguration)` extension method exists in `Muonroi.Pdf.Extensions` namespace within the `Muonroi.Pdf` package
- [ ] **DI-02**: All services registered with `TryAddSingleton` to avoid duplicate registration errors
- [ ] **DI-03**: `PdfConfigs` bound from `IConfiguration` section `"PdfConfigs"` and validated on startup (throws on invalid limits)
- [ ] **DI-04**: Default implementations of `IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter` registered when no custom implementation is provided

### Rendering Pipeline

- [x] **PIPE-01**: HTML input passes through `IHtmlParser` (AngleSharp) to produce a DOM — parser rejects input exceeding `PdfConfigs.Limits.MaxHtmlBytes`
- [x] **PIPE-02**: DOM depth and element count validated against `PdfConfigs.Limits.MaxDomDepth` and `MaxElementCount`; render aborted with structured error on violation
- [x] **PIPE-03**: `ICssCascadeEngine` (AngleSharp.Css 1.0.0-beta.146) resolves computed styles on the DOM
- [x] **PIPE-04**: `IPdfCssPolicy` gate runs after cascade; unsupported CSS properties produce structured `PolicyViolation` diagnostics, not silent fallback
- [x] **PIPE-05**: Hand-written box tree constructed from styled DOM — no dependency on archived HtmlRenderer.PdfSharp, no GDI+ dependency
- [x] **PIPE-06**: Layout engine produces a final page list with box positions before `IPdfWriter` is called
- [x] **PIPE-07**: `IPdfWriter` (PdfSharpCore 1.3.x adapter) writes the positioned boxes to a `Stream`
- [ ] **PIPE-08**: Total render time enforced against `PdfConfigs.Limits.MaxRenderDurationMs`; render cancelled with `OperationCanceledException` on timeout

### Layout Engine

- [x] **LAYOUT-01**: Block formatting context (BFC) established correctly for block-level elements; margin collapsing applied per CSS 2.1 spec
- [x] **LAYOUT-02**: Inline formatting context handles white-space, line-break, and vertical-align properties
- [x] **LAYOUT-03**: Baseline alignment computed correctly for mixed inline content
- [x] **LAYOUT-04**: `display:table`, `display:table-row`, `display:table-cell` rendered with correct column/row sizing
- [x] **LAYOUT-05**: `colspan` and `rowspan` attributes respected in table layout
- [x] **LAYOUT-06**: `border-collapse: separate` applied correctly; `border-spacing` honored
- [x] **LAYOUT-07**: `border-collapse: collapse` rejected by `IPdfCssPolicy` with a structured diagnostic naming the alternative

### Pagination

- [x] **PAGE-01**: `@page` rule parsed; margin boxes (top, right, bottom, left) applied to each page
- [x] **PAGE-02**: Standard page sizes supported: A4, A5, Letter, Legal — both portrait and landscape
- [x] **PAGE-03**: `page-break-before`, `page-break-after`, `page-break-inside` properties respected; `avoid` honored where feasible
- [x] **PAGE-04**: Repeated page header rendered from `@page` top margin box on every page
- [x] **PAGE-05**: Repeated page footer rendered from `@page` bottom margin box on every page
- [x] **PAGE-06**: `counter(page)` resolves to the current page number (1-based)
- [x] **PAGE-07**: `counter(pages)` resolves to the total page count
- [x] **PAGE-08**: Generated page count within `PdfConfigs.Limits.MaxPages`; render aborted with structured error on violation

### Font Handling

- [x] **FONT-01**: `@font-face` declarations resolved via `IFontResolver` — bytes-only, no URI dereferencing by the engine
- [x] **FONT-02**: TTF and OTF font formats embedded in the output PDF
- [x] **FONT-03**: Font subsetting applied via SixLabors.Fonts 2.1.x — only glyphs used in the document are embedded
- [x] **FONT-04**: Vietnamese diacritic stacking rendered correctly using SixLabors.Fonts shaping (combining diacritics positioned above/below base glyph) _(metrics + glyph embedding verified by automated tests; pixel-level visual spot-check recommended at v0.1 release)_
- [x] **FONT-05**: Mixed Latin + Vietnamese line-breaking computes break opportunities correctly
- [x] **FONT-06**: Number of loaded font files validated against `PdfConfigs.Limits.MaxFontFiles`; font loading aborted on violation

### Image Handling

- [x] **IMG-01**: PNG images decoded and embedded in the output PDF
- [x] **IMG-02**: JPEG images decoded and embedded in the output PDF
- [x] **IMG-03**: Base64 `data:` URI images decoded inline — no outbound network call
- [x] **IMG-04**: External `src` URIs resolved exclusively via `IResourceResolver.ResolveAsync` — engine never opens a network connection or file path directly
- [x] **IMG-05**: Decoded pixel count validated against `PdfConfigs.Limits.MaxImagePixels`; image rejected with structured error on violation

### Security Hardening

- [x] **SEC-01**: PDF output version pinned to 1.7; linearization disabled in the default `IPdfWriter` implementation
- [x] **SEC-02**: `/JavaScript`, `/Launch`, `/OpenAction`, `/EmbeddedFile` PDF dictionary entries rejected — any attempt to write them throws in the default writer
- [x] **SEC-03**: Object IDs in the generated PDF are deterministic (content-hash–derived or sequential), never random
- [x] **SEC-04**: No timestamp fields written to the PDF (no `CreationDate`, no `ModDate`) _(fixed sentinel date written for byte-determinism; no current-time leakage — verified by test)_
- [x] **SEC-05**: `<script>` elements in HTML input rejected by `IPdfCssPolicy` (or equivalent HTML policy gate) with a structured diagnostic
- [x] **SEC-06**: `file://` URI scheme rejected by `IResourceResolver` default implementation
- [ ] **SEC-07**: Multi-tenant cache keys derived from `(ITenantContext.TenantId, contentHash)` via ambient `ITenantContext` — caller-supplied strings never used as cache keys

### Deterministic Output

- [ ] **DET-01**: Rendering the same HTML+CSS input twice with the same `PdfRenderOptions` produces byte-for-byte identical PDF output
- [ ] **DET-02**: Determinism holds across process restarts (no process-lifetime state affects output bytes)
- [ ] **DET-03**: Determinism holds across different OS (Windows, Linux, Alpine) for the same input

### Telemetry

- [ ] **TEL-01**: `PdfTelemetryDescriptor : ITelemetryDescriptor` class exists with a public parameterless constructor
- [ ] **TEL-02**: Activity source named `Muonroi.BuildingBlock.Pdf` emitted for each render operation
- [ ] **TEL-03**: Metric `pdf.operation` (counter) recorded per render with `pdf.template_id` and `tenant.id` tags in snake_case
- [ ] **TEL-04**: Metric `pdf.page_count` (histogram) recorded per completed render
- [ ] **TEL-05**: `IMLog<T>` used for all internal logging — no raw `ILogger<T>` or `Console` calls

### Governance (`Muonroi.Pdf.Governance`)

- [x] **GOV-01**: `IPdfCssPolicy.DefaultStrict` rejects: `display:flex`, `display:grid`, `float`, `position:absolute`, `position:fixed`, `position:sticky`, CSS animations, CSS transitions, `@import` with external URIs
- [x] **GOV-02**: Every policy rejection includes a structured `PolicyViolation` with: property name, rejected value, CSS selector, and a suggested alternative
- [x] **GOV-03**: Policy configs can be signed via `Muonroi.Governance.Policy.PolicyVerifier`; engine refuses unsigned configs when signing is required by `PdfConfigs`

### Test Coverage

- [ ] **TEST-01**: ≥40 internal golden snapshot tests covering: block layout, inline layout, table layout, page breaks, image embedding, font embedding
- [ ] **TEST-02**: ≥10 Vietnamese golden snapshots covering: diacritic stacking, mixed Latin+Vietnamese text, line-breaking
- [ ] **TEST-03**: CSS 2.1 spec subset passes at ≥95% on declared modules; deviations documented in `KNOWN-DEVIATIONS.md`
- [ ] **TEST-04**: `KNOWN-DEVIATIONS.md` published listing every intentional deviation from CSS 2.1 in the declared subset

### Performance

- [x] **PERF-01**: Cold render of a 50 KB single-page template completes in ≤300 ms on a developer machine (single thread)
- [x] **PERF-02**: Warm render of a 50 KB single-page template completes in ≤80 ms on a developer machine (single thread)

### Convention Gates

- [ ] **GATE-01**: `check-modular-boundaries.ps1` passes with no violations
- [ ] **GATE-02**: `pre-publish-gate.ps1` passes for all three OSS packages and the Enterprise stub
- [ ] **GATE-03**: `InjectAssemblyHash.ps1` locks the `Muonroi.Pdf.Enterprise` assembly hash in CI

---

## v0.2 Requirements — OSS Hardening (target: M+8)

### Source Generator Fast Path

- **SG-01**: `IMPdfRenderer<TModel>` source generator emits compile-time template implementations — no runtime `IMPdfRendererFactory` API change
- **SG-02**: Source generator warm throughput is ≥3× faster than the runtime `IMPdfRendererFactory` path on equivalent input
- **SG-03**: Opting into the SG path requires no code change on the call site — resolved automatically at compile time when the SG is referenced

### AOT / Trim Safety

- **AOT-01**: No reflection-emit in the render hot path; all types annotated with `[DynamicallyAccessedMembers]` where required
- **AOT-02**: `PublishAot` sample on Alpine renders the full golden snapshot corpus with output identical to the JIT path
- **AOT-03**: Published Alpine AOT container image is <40 MB

### Design System

- **DS-01**: `Muonroi.Pdf.DesignSystem.Default` package ships with: typography scale, color tokens, table styles, and at least three named templates (invoice, receipt, report)
- **DS-02**: All design system templates pass the full `IPdfCssPolicy.DefaultStrict` gate without violations

### Allocation Reduction

- **ALLOC-01**: Hot-path heap allocations reduced by ≥30% vs the v0.1 baseline (measured via BenchmarkDotNet memory diagnostics on the 50 KB single-page template)

---

## v1.0 Requirements — Enterprise Commercial (target: M+12)

### Template Registry

- **REG-01**: `Muonroi.Pdf.Enterprise.Registry` stores templates in Postgres with full version history
- **REG-02**: RBAC enforced on template publish and read operations
- **REG-03**: Audit trail records: who published, from what IP, at what time, and what changed (diff at minimum)

### License Enforcement

- **LIC-01**: `Muonroi.Pdf.Enterprise.License` client validates license on Enterprise startup; service refuses to start without a valid license
- **LIC-02**: License validation does not block or degrade OSS (`Muonroi.Pdf`) operation

### Hot Reload

- **HOT-01**: `Muonroi.Pdf.Enterprise.HotReload` propagates a new template version to all N nodes within ≤5 seconds via Redis pub/sub
- **HOT-02**: Invalidation is tenant-scoped — publishing a template for Tenant A does not invalidate Tenant B's cache

### Canary Rollout

- **CANARY-01**: `Muonroi.Pdf.Enterprise.Canary` rolls out a new template version to a configurable cohort percentage before full rollout
- **CANARY-02**: SSIM diff harness rasterizes old and new PDF output and computes a structural similarity score
- **CANARY-03**: Canary automatically rolls back to the previous version if SSIM score drops below a configurable threshold before 100% rollout is reached

### Web UI Designer

- **DESIGN-01**: `Muonroi.Pdf.Enterprise.Designer` provides a web UI for editing HTML+CSS templates
- **DESIGN-02**: Live preview is pinned to the exact engine version deployed — no version drift between preview and production render
- **DESIGN-03**: Round-trip (edit → preview → publish) completes in <10 s at P95

### Production Cutover

- **TCIS-01**: TCIS.ePort removes DinkToPdf from its dependency graph
- **TCIS-02**: All wkhtmltopdf CVEs removed from the TCIS production vulnerability graph after cutover

### Commercial Adoption

- **COMM-01**: ≥3 paid Enterprise customers signed before v1.0 GA
- **COMM-02**: ARR from Enterprise licenses ≥ $60k at v1.0 GA

---

## Out of Scope

| Feature | Reason |
|---------|--------|
| Browser engine rendering (Chromium, Puppeteer, Playwright, CefSharp) | Introduces native deps, CVE treadmill, sidecar process; hard stakeholder rule |
| Native dependencies (libwkhtmltox, GDI+, Skia native, libgdiplus) | Must run identically on Windows/Linux/Alpine/AOT — native rules out all three |
| JavaScript execution in templates | `<script>` is an XSS/exfil vector in PDF context; templates must be fully resolved before reaching the renderer |
| Outbound network at render time | Opens `file://` SSRF and `https://` exfil paths; blocked in air-gapped environments; `IResourceResolver` is the only asset path |
| `border-collapse: collapse` | Significant algorithmic complexity; deferred to post-v0.1 to protect timeline |
| Flexbox / Grid / float / position:absolute / position:fixed / position:sticky | Each requires a separate layout algorithm; collectively 3-4× the layout engine scope; rejected with structured diagnostic |
| Pixel-match parity with DinkToPdf / wkhtmltopdf | wkhtmltopdf is archived; chasing its rendering bugs locks to a dead target; acceptance bar is CSS 2.1 spec conformance on declared subset |
| `Muonroi.Pdf.AspNetCore` separate package | Unnecessary extra package; DI lives in `Muonroi.Pdf.Extensions` matching `RedisExtensions.cs` pattern |
| SVG filters, SVG animations, SVG `foreignObject` | Separate rendering engine problem; animations meaningless in PDF |
| RTL bidi beyond Unicode default, Arabic/Indic/CJK shaping | Requires HarfBuzz or platform-level text services; pure-managed implementations are incomplete — best-effort Unicode bidi only |
| CSS animations and transitions | Meaningless in a static PDF output format |
| Source generator fast path in v0.1 | Deferred to v0.2; additive with no API break when shipped; reduces v0.1 scope |

---

## Traceability

| Requirement | Phase | Release | Status |
|-------------|-------|---------|--------|
| PKG-01 | Phase 1 | v0.1 | Pending |
| PKG-02 | Phase 6 | v0.1 | Pending |
| PKG-03 | Phase 2 | v0.1 | Pending |
| PKG-04 – PKG-07 | Phase 7 | v0.1 | Pending |
| ABST-01 – ABST-14 | Phase 1 | v0.1 | Pending |
| DI-01 – DI-04 | Phase 6 | v0.1 | Pending |
| PIPE-01 – PIPE-04 | Phase 2 | v0.1 | Pending |
| PIPE-05 – PIPE-06 | Phase 3 | v0.1 | Pending |
| PIPE-07 | Phase 5 | v0.1 | Pending |
| PIPE-08 | Phase 6 | v0.1 | Pending |
| LAYOUT-01 – LAYOUT-07 | Phase 3 | v0.1 | Pending |
| PAGE-01 – PAGE-08 | Phase 3 | v0.1 | Pending |
| FONT-01 – FONT-06 | Phase 4 | v0.1 | Pending |
| IMG-01 – IMG-05 | Phase 4 | v0.1 | Pending |
| SEC-01 – SEC-07 | Phase 5 | v0.1 | Pending |
| DET-01 – DET-03 | Phase 5 | v0.1 | Pending |
| TEL-01 – TEL-05 | Phase 6 | v0.1 | Pending |
| GOV-01 – GOV-03 | Phase 2 | v0.1 | Pending |
| TEST-01 – TEST-04 | Phase 7 | v0.1 | Pending |
| PERF-01 – PERF-02 | Phase 7 | v0.1 | Pending |
| GATE-01 – GATE-03 | Phase 7 | v0.1 | Pending |
| SG-01 – SG-03 | Phase 8 | v0.2 | Pending |
| AOT-01 – AOT-03 | Phase 8 | v0.2 | Pending |
| DS-01 – DS-02 | Phase 8 | v0.2 | Pending |
| ALLOC-01 | Phase 8 | v0.2 | Pending |
| REG-01 – REG-03 | Phase 9 | v1.0 | Pending |
| LIC-01 – LIC-02 | Phase 9 | v1.0 | Pending |
| HOT-01 – HOT-02 | Phase 9 | v1.0 | Pending |
| CANARY-01 – CANARY-03 | Phase 9 | v1.0 | Pending |
| DESIGN-01 – DESIGN-03 | Phase 9 | v1.0 | Pending |
| TCIS-01 – TCIS-02 | Phase 9 | v1.0 | Pending |
| COMM-01 – COMM-02 | Phase 9 | v1.0 | Pending |
| MON-01 – MON-08 | Phase 17 | v1.1 | Pending |

### Phase 17 — Monetization Rail (MON)

- **MON-01**: `Muonroi.Billing.Abstractions` defines `IBillingProvider`, `UsageLineItem`, `IUsageAggregator`, and `PricingPlan` contracts (no payment-SDK dependency).
- **MON-02**: A record-only default `IBillingProvider` records billable events and never calls an external service; provider failures are logged with context (No Silent Catch), never swallowed silently.
- **MON-03**: `IUsageAggregator` rolls per-tenant metered usage (from `ITenantQuotaStore`) into priced `UsageLineItem`s for a billing period via a `PricingPlan`.
- **MON-04**: Per-tier quota limits (incl. `MaxPdfRendersPerDay`) are sourced from the licensed tier rather than hard-coded `int.MaxValue`.
- **MON-05**: control-plane host registers `UseQuotaEnforcement()`; a tenant over its tier limit receives HTTP 429 at the API boundary; the OSS render path is never blocked (SC5).
- **MON-06**: control-plane exposes an invoice-preview endpoint returning the computed amount for a tenant + period from aggregated usage and the `PricingPlan` (replaces `PricingEndpoints` placeholder prices).
- **MON-07**: license-server exposes a subscription + renewal lifecycle (renew endpoint, expiry/grace) so renewal is not manual re-issue only; exposes tier→quota-limit mapping.
- **MON-08**: Full suites green across the three repos; `Muonroi.Pdf` (OSS) byte-identical (no golden re-baseline); no billing reference leaks into the OSS engine.

**Coverage:**
- v0.1 requirements: 86 total (PKG×7, ABST×14, DI×4, PIPE×8, LAYOUT×7, PAGE×8, FONT×6, IMG×5, SEC×7, DET×3, TEL×5, GOV×3, TEST×4, PERF×2, GATE×3)
- v0.2 requirements: 9 total (SG×3, AOT×3, DS×2, ALLOC×1)
- v1.0 requirements: 17 total (REG×3, LIC×2, HOT×2, CANARY×3, DESIGN×3, TCIS×2, COMM×2)
- Unmapped: 0 ✓

---
*Requirements defined: 2026-05-26*
*Last updated: 2026-05-26 after initial derivation from PROJECT.md and feature research*
