# Feature Research

**Domain:** HTML/CSS-to-PDF renderer, open-core .NET library
**Researched:** 2026-05-26
**Confidence:** HIGH — derived directly from PROJECT.md requirements, key decisions, and out-of-scope declarations

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features any HTML-to-PDF library must have. Missing these = non-starter evaluation failure.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| HTML parsing | Core contract — renderer accepts HTML input | LOW | AngleSharp 1.3.x; adapter seam `IHtmlParser` |
| CSS cascade + inheritance | Required to render styled documents correctly | MEDIUM | AngleSharp.Css 1.0.0-beta.146 pinned; adapter seam `ICssCascadeEngine` |
| Block/inline layout with margin collapsing | Box model basics; every HTML document uses it | HIGH | Hand-written layout engine; BFC roots; no GDI+ dep |
| Table rendering (colspan, rowspan) | Invoices, reports, and data exports all use tables | HIGH | `display:table`, `border-collapse: separate` only in v0.1 |
| Image embedding (PNG, JPEG, base64 data URIs) | Documents routinely include logos, charts | MEDIUM | `IResourceResolver` bytes-only; adapter seam `IImageDecoder` |
| `@page` margins and standard page sizes | A4/A5/Letter/Legal are baseline expectations | MEDIUM | Full `@page` rule support; `box-sizing` applied to page box |
| Page breaks (before/after/inside) | Multi-page documents require controlled pagination | MEDIUM | `page-break-before`, `page-break-after`, `page-break-inside` |
| Repeated page headers and footers | Enterprise documents (invoices, contracts) require this | MEDIUM | Rendered per-page from `@page` margin boxes |
| Page numbering (`counter(page)`, `counter(pages)`) | Standard expectation for any multi-page doc | LOW | CSS counter support; hooks into pagination pass |
| Font embedding (`@font-face`, TTF/OTF) | Brand fonts, Vietnamese glyphs require embedded fonts | MEDIUM | `IFontResolver` bytes-only; SixLabors.Fonts for subsetting |
| DI registration (`AddPdf()`) | .NET teams expect IServiceCollection extension method | LOW | Co-located in `Muonroi.Pdf.Extensions`; `TryAddSingleton` |
| Stream output (not byte[]) | Large documents → large heap allocations if byte[] | LOW | `Stream destination` overload + `PdfRenderResult.Content : Stream` |
| Security resource limits | Production systems require protection against malicious input | MEDIUM | `PdfConfigs.Limits`: 8 MB HTML, 256 DOM depth, 100k elements, 25 MP images, 1000 pages, 15s render, 32 fonts |
| OpenTelemetry instrumentation | .NET teams instrument everything via OTel | LOW | `PdfTelemetryDescriptor`; activity source `Muonroi.BuildingBlock.Pdf`; snake_case metrics |

### Differentiators (Competitive Advantage)

Features that make Muonroi.Pdf the choice over IronPDF, Syncfusion, or a Puppeteer wrapper.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Zero native dependencies (pure managed) | Runs on Alpine/AOT without sidecar or P/Invoke — wkhtmltopdf alternatives all have this problem | HIGH | No GDI+, no libwkhtmltox, no Skia native; Windows/Linux/Alpine identical |
| AOT-compatible, trim-safe (v0.2) | Alpine container <40 MB; startup latency eliminated for FaaS/serverless | HIGH | No reflection-emit in hot path; trim-safe annotations; validated via `PublishAot` sample |
| Deterministic byte-for-byte output | CI/test diffs, cache deduplication, audit trails require identical bytes across runs | MEDIUM | No timestamp, no random object IDs; enforced in `IPdfWriter` default impl |
| CSS policy enforcement + signed configs | Governance layer blocks unsupported CSS at policy gate, not silent ignore | MEDIUM | `IPdfCssPolicy.DefaultStrict`; `Muonroi.Governance.Policy.PolicyVerifier` signs configs |
| Vietnamese diacritic stacking + mixed Latin+Vietnamese line-breaking | Vietnamese content is first-class — not best-effort | HIGH | SixLabors.Fonts shaping; ≥10 Vietnamese golden snapshots required |
| Multi-tenant cache with ambient context | Prevents cross-tenant cache poisoning by design | LOW | Cache keys from `(ITenantContext.TenantId, contentHash)` — never caller-supplied strings |
| Source generator compile-time fast path (v0.2) | ≥3× warm throughput vs runtime path; significant for high-volume invoice/report generation | HIGH | Additive — no breaking change to public API; `IMPdfRendererFactory` seam ships in v0.1 |
| Structured rejection diagnostics | Unsupported CSS features produce actionable errors, not silent fallback | LOW | flex/grid/float/absolute rejected with structured diagnostics |
| Adapter seams for all third-party deps | Any dep can be swapped in one class; insulates teams from upstream breakage | LOW | `IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter` in Abstractions |
| PDF 1.7 hardened writer | JS/Launch/OpenAction/EmbeddedFile rejected; linearization off; deterministic IDs | LOW | Security baseline baked into default `IPdfWriter` — not an option, always on |
| Enterprise template registry with RBAC + audit trail (v1.0) | Teams where "who published this template" matters — compliance, regulated industries | HIGH | Postgres store, version history, audit trail in `Muonroi.Pdf.Enterprise.Registry` |
| Canary rollout with SSIM PDF diff harness (v1.0) | Template changes can be rolled out to a cohort, diff'd against previous version, auto-rolled back | HIGH | Rasterize + SSIM comparison; automatic rollback before 100% rollout |
| Redis-backed hot reload across N nodes (v1.0) | Template publish live ≤5s across cluster without restart | MEDIUM | Tenant-scoped invalidation in `Muonroi.Pdf.Enterprise.HotReload` |
| Web UI template designer with live preview (v1.0) | Non-engineers can edit templates; round-trip edit→preview→publish <10s P95 | HIGH | `Muonroi.Pdf.Enterprise.Designer` |
| Pre-built design system (v0.2) | Reduces time-to-first-invoice from hours to minutes | MEDIUM | `Muonroi.Pdf.DesignSystem.Default`: typography scale, color tokens, table styles, invoice/receipt/report templates |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Browser engine rendering (Chromium, Puppeteer) | Full CSS/JS fidelity out of the box | Introduces native deps, CVE treadmill, sidecar process, 100MB+ binary — violates no-native constraint; stakeholder hard rule | Hand-written layout for CSS 2.1 declared subset; structured rejection for unsupported features |
| JavaScript execution | Dynamic templates with computed values | `<script>` is an XSS/exfil vector in PDF context; templates should be fully resolved before render | Pre-substitute placeholders upstream before passing HTML to renderer |
| Outbound HTTP at render time (src/href/@import resolution) | "Just let the engine fetch the asset" | Opens file:// SSRF + https:// exfil paths; impossible to audit in policy; blocked by air-gapped environments | `IResourceResolver` bytes-only contract; caller provides bytes, engine never dereferences URIs |
| `border-collapse: collapse` | Standard CSS table border model | Significant algorithmic complexity; adds scope risk to v0.1 timeline | `border-collapse: separate` in v0.1; collapse deferred to future version |
| Flexbox / Grid / float / position absolute | Modern layout primitives | Each requires a separate layout algorithm; collectively 3-4× the layout engine scope | Policy rejects with structured diagnostic; document alternatives using block/table layout |
| Pixel-match parity with DinkToPdf/wkhtmltopdf | "Looks the same as before" migration path | wkhtmltopdf is archived; its rendering bugs are not a spec; chasing perceptual match locks to a dead target | Acceptance bar is CSS 2.1 spec conformance on declared subset; KNOWN-DEVIATIONS.md documents gaps |
| Separate `Muonroi.Pdf.AspNetCore` package | Mirrors ASP.NET Core's extension package pattern | Unnecessary extra package for DI extensions that don't need ASP.NET Core framework deps | DI registration lives in `Muonroi.Pdf.Extensions` co-located with the engine; matches `RedisExtensions.cs` |
| SVG filters/animations/foreignObject | Rich vector graphics in PDFs | SVG rendering is a separate rendering engine problem; animations are meaningless in PDF | Static SVG rasterization via `IImageDecoder` is acceptable path; filters/animations rejected |
| RTL bidi / Arabic / Indic / CJK shaping | Global audience | Correct shaping requires HarfBuzz or platform-level text services; pure-managed implementations are incomplete | Best-effort Unicode bidi only; document limitation in KNOWN-DEVIATIONS.md |

---

## Feature Dependencies

```
[HTML Parsing (IHtmlParser)]
    └──required by──> [CSS Cascade (ICssCascadeEngine)]
                           └──required by──> [Policy Gate (IPdfCssPolicy)]
                                                  └──required by──> [Box Tree + Layout]
                                                                         └──required by──> [PDF Writer (IPdfWriter)]

[@font-face resolution (IFontResolver)]
    └──required by──> [Font Embedding + Subsetting]
                           └──required by──> [Vietnamese Diacritic Stacking]

[Image Decoding (IImageDecoder)]
    └──required by──> [Image Embedding in PDF]

[@page rule parsing]
    └──required by──> [Page Break Logic]
                           └──required by──> [Repeated Headers/Footers]
                                                  └──required by──> [Page Numbering (counter)]

[IMPdfRendererFactory (v0.1 runtime)]
    └──enabled by──> [Source Generator fast path (v0.2)] — additive, no API change

[Template Registry (Enterprise, v1.0)]
    └──required by──> [RBAC + Audit Trail]
    └──required by──> [Hot Reload (Redis notifier)]
    └──required by──> [Canary Rollout + SSIM diff]
    └──required by──> [Web UI Designer]

[Deterministic Output]
    └──enables──> [SSIM PDF diff harness] — diff is only meaningful when output is byte-for-byte stable

[Multi-tenant cache (ITenantContext ambient)]
    └──required by──> [Hot Reload tenant-scoped invalidation]
```

### Dependency Notes

- **CSS cascade requires HTML parsing:** The DOM must be built before cascade can resolve inherited properties. Both must be complete before the policy gate runs.
- **Policy gate must precede layout:** Layout must not run on rejected CSS — structured errors surface at policy, not deep in the box tree.
- **Font resolution required for Vietnamese:** SixLabors.Fonts needs the TTF/OTF bytes from `IFontResolver` before it can compute glyph metrics and diacritic stacking.
- **Deterministic output enables SSIM diff:** The canary diff harness compares rasterized PDFs; pixel-level SSIM comparison is only meaningful when the baseline and candidate differ only in intentional changes, not in timestamps or random object IDs.
- **Source generator (v0.2) requires `IMPdfRendererFactory` in v0.1:** The factory interface ships in v0.1 with the runtime implementation; v0.2 adds the compile-time implementation behind the same interface — no API break.
- **Enterprise features require OSS engine:** Registry, hot reload, canary, and designer all sit on top of the core render pipeline. OSS engine must be stable before Enterprise build-out starts (justifies the M+7 FTE ramp).

---

## MVP Definition

### Launch With (v0.1 — OSS Engine, M+5)

Minimum to validate the core premise: "pure-managed HTML+CSS→PDF with policy enforcement."

- [x] `Muonroi.Pdf.Abstractions` — public contracts (`IMPdfService`, `IMPdfRenderer<T>`, `IMPdfRendererFactory`, all adapter interfaces)
- [x] `Muonroi.Pdf` engine — full AngleSharp→cascade→policy→layout→PdfSharpCore pipeline
- [x] `Muonroi.Pdf.Governance` — `IPdfCssPolicy.DefaultStrict` + signed policy configs
- [x] Block/inline layout with margin collapsing and BFC roots — why essential: without this, no real document renders
- [x] Table rendering (colspan/rowspan, border-separate) — why essential: invoices and reports are the primary use case
- [x] Image embedding (PNG, JPEG, base64) — why essential: logos and charts appear in almost every document
- [x] `@page` + page breaks + headers/footers + page counters — why essential: multi-page pagination is core contract
- [x] `@font-face` + Vietnamese diacritic stacking — why essential: Vietnamese is a first-class target audience; shipping without it invalidates the differentiator
- [x] Deterministic output — why essential: CI snapshot tests are meaningless without it
- [x] `PdfConfigs.Limits` security defaults — why essential: production deployments require enforceable resource limits on day one
- [x] PDF 1.7 hardened writer — why essential: PDF injection attacks are real; security baseline cannot be a later add-on
- [x] OpenTelemetry instrumentation — why essential: .NET teams won't adopt without observability integration
- [x] `AddPdf()` DI registration — why essential: discoverable entry point; matches ecosystem conventions
- [x] ≥40 golden snapshot tests + ≥10 Vietnamese snapshots — why essential: only way to detect regressions against deterministic output
- [x] `KNOWN-DEVIATIONS.md` — why essential: sets honest expectations; prevents support tickets about intentional gaps

### Add After Validation (v0.2 — OSS Hardening, M+8)

Add once OSS adoption metrics validate the core premise (download velocity, GitHub stars, issues filed).

- [ ] Source generator fast path (`IMPdfRenderer<TModel>`) — trigger: warm throughput becomes a reported bottleneck; adds ≥3× throughput with no API change
- [ ] AOT-friendly mode + trim-safe annotations — trigger: FaaS/serverless adopters report cold start latency issues
- [ ] `Muonroi.Pdf.DesignSystem.Default` — trigger: OSS users ask for starter templates; reduces friction for new adopters
- [ ] ≥30% allocation reduction on hot path — trigger: GC pressure reported in high-throughput scenarios

### Future Consideration (v1.0 — Enterprise Commercial, M+12)

Defer until ≥3 Enterprise prospects express intent to pay.

- [ ] `Muonroi.Pdf.Enterprise.Registry` (Postgres template store, RBAC, audit trail) — defer: requires ops infrastructure; no OSS user needs it
- [ ] `Muonroi.Pdf.Enterprise.License` — defer: gates Enterprise startup; needed only when commercial tier goes live
- [ ] `Muonroi.Pdf.Enterprise.HotReload` (Redis, ≤5s propagation) — defer: only valuable in multi-node deployments; no OSS user has N nodes
- [ ] `Muonroi.Pdf.Enterprise.Canary` (SSIM diff, auto-rollback) — defer: requires SSIM harness + Registry to be meaningful
- [ ] `Muonroi.Pdf.Enterprise.Designer` (web UI editor, live preview) — defer: highest complexity; only justified by paying customers

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Block/inline layout engine | HIGH | HIGH | P1 |
| CSS cascade + policy gate | HIGH | MEDIUM | P1 |
| Table rendering (colspan/rowspan) | HIGH | HIGH | P1 |
| `@page` + page breaks + headers/footers | HIGH | MEDIUM | P1 |
| Image embedding | HIGH | MEDIUM | P1 |
| `@font-face` + font subsetting | HIGH | MEDIUM | P1 |
| Vietnamese diacritic stacking | HIGH | HIGH | P1 |
| Deterministic output | HIGH | LOW | P1 |
| Security limits (`PdfConfigs.Limits`) | HIGH | LOW | P1 |
| PDF 1.7 hardened writer | HIGH | LOW | P1 |
| `AddPdf()` DI + telemetry | MEDIUM | LOW | P1 |
| Golden snapshot test suite | HIGH | MEDIUM | P1 |
| Source generator fast path | MEDIUM | HIGH | P2 |
| AOT-friendly mode | MEDIUM | HIGH | P2 |
| `DesignSystem.Default` templates | MEDIUM | MEDIUM | P2 |
| ≥30% allocation reduction | MEDIUM | MEDIUM | P2 |
| Enterprise template registry (RBAC/audit) | HIGH (Enterprise) | HIGH | P3 |
| Hot reload (Redis, ≤5s) | HIGH (Enterprise) | MEDIUM | P3 |
| Canary rollout + SSIM diff | HIGH (Enterprise) | HIGH | P3 |
| Web UI designer | HIGH (Enterprise) | HIGH | P3 |

**Priority key:**
- P1: Must have for v0.1 launch — without these, the core value proposition fails
- P2: Should have; add in v0.2 once core is validated
- P3: Nice to have; deferred to v1.0 Enterprise

---

## Competitor Feature Analysis

| Feature | DinkToPdf/wkhtmltopdf | IronPDF | Syncfusion HTML→PDF | Muonroi.Pdf |
|---------|----------------------|---------|---------------------|-------------|
| Pure managed, no native | No (libwkhtmltox) | No (Chromium) | No (GDI+) | Yes — design goal |
| Alpine/AOT compatible | No | No | No | Yes (v0.2 AOT) |
| CSS 2.1 conformance | wkhtmltopdf-spec (quirky) | Chromium-spec | Partial | Declared subset, ≥95% pass |
| Deterministic output | No | No | No | Yes — enforced |
| CSS policy enforcement | No | No | No | Yes — Governance layer |
| Vietnamese shaping | Best-effort | Browser-dependent | Best-effort | First-class (SixLabors.Fonts) |
| JavaScript execution | Yes | Yes | Partial | Rejected by policy (intentional) |
| Outbound network at render | Yes | Yes | Yes | Rejected by policy (intentional) |
| OpenTelemetry instrumentation | No | No | No | Yes — `PdfTelemetryDescriptor` |
| Multi-tenant cache | No | No | No | Yes — ambient `ITenantContext` |
| Template governance (RBAC, audit) | No | No | No | Yes — Enterprise v1.0 |
| Canary rollout | No | No | No | Yes — Enterprise v1.0 |
| License model | LGPL/archived | Commercial | Commercial | Apache 2.0 OSS + Enterprise commercial |
| Maintenance status | Archived 2023 | Active | Active | Active (this project) |

**Takeaway:** The competitive moat is the pure-managed + deterministic + governance combination. No existing library offers all three. IronPDF and Syncfusion compete on CSS fidelity (browser-engine parity) — Muonroi.Pdf competes on operational properties (deployability, auditability, security).

---

## Sources

- `PROJECT.md` (v0.1 requirements, v0.2 requirements, v1.0 requirements, out-of-scope declarations, key decisions) — HIGH confidence, authoritative project specification
- Key decisions table (PROJECT.md) — rationale for anti-features (JS rejection, network rejection, browser-engine rejection) — HIGH confidence
- wkhtmltopdf archived status: confirmed archived December 2023 per project GitHub; no longer receiving CVE patches
- IronPDF/Syncfusion: native dependency status confirmed from their published system requirements (Chromium runtime for IronPDF, GDI+ for Syncfusion)

---
*Feature research for: Muonroi.Pdf — governed HTML/CSS-to-PDF renderer for .NET*
*Researched: 2026-05-26*
