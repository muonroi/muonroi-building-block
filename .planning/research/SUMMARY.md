# Project Research Summary

**Project:** Muonroi.Pdf — Pure-Managed HTML/CSS-to-PDF Renderer
**Domain:** Open-core .NET library; HTML/CSS → PDF pipeline, multi-tenant SaaS
**Researched:** 2026-05-26
**Confidence:** HIGH

## Executive Summary

Muonroi.Pdf is a pure-managed, AOT-compatible .NET library that converts HTML+CSS to PDF without any native binary dependencies (no GDI+, no Chromium, no libwkhtmltox). The core premise — validated by a survey of every available .NET managed library — is that no existing solution covers this combination: deterministic output, Alpine/AOT compatibility, CSS policy enforcement, and Vietnamese first-class text support. The hand-built pipeline (AngleSharp parse → AngleSharp.Css cascade → policy gate → custom box tree/layout → PdfSharpCore write) is the only viable architecture given the no-native constraint.

The recommended approach is a three-wave delivery: v0.1 ships the OSS core engine with all P1 features (layout, tables, images, fonts, pagination, Vietnamese, deterministic output, governance); v0.2 adds the source-generator fast path (≥3× throughput), AOT trim-safe mode, and a default design system once OSS adoption validates the core; v1.0 adds the commercial Enterprise tier (Postgres template registry, Redis hot-reload, SSIM canary, web designer) gated on ≥3 paying prospects. The open-core Apache 2.0 strategy means the OSS engine is the commercial moat, not a loss leader.

The dominant risks are: (1) the hand-written box tree implementing CSS 2.1 layout incorrectly and locking bugs into golden snapshots, (2) AngleSharp.Css beta API breaking without warning because it is the only managed CSS cascade engine available, and (3) the deterministic-output guarantee silently broken by timestamps or random PDF object IDs in PdfSharpCore. All three risks are mitigated by the adapter seam architecture — every third-party dependency is hidden behind a swappable interface — and by writing golden tests against the W3C CSS 2.1 conformance suite rather than against the engine's own output.

## Key Findings

### Recommended Stack

The stack is fully constrained by the no-native-dependencies rule and the existing CPM (`Directory.Packages.props`) baseline. No alternative managed HTML5 parser, CSS cascade engine, or pure-managed PDF writer exists in .NET — the chosen stack is the only viable one.

**Core technologies:**
- **AngleSharp 1.3.x** — HTML5 DOM parsing; only spec-compliant pure-managed parser available
- **AngleSharp.Css 1.0.0-beta.146 (pinned)** — CSS cascade engine; only managed option; beta accepted and isolated behind `ICssCascadeEngine` adapter seam
- **SixLabors.Fonts 2.1.x** — OpenType glyph metrics, subsetting, Vietnamese diacritic stacking; Apache 2.0; no native alternative
- **PdfSharpCore 1.3.x (MIT)** — PDF 1.7 object model writer; pure-managed, Linux/Alpine/AOT safe; isolated behind `IPdfWriter` adapter
- **Hand-written box tree + layout engine** — no viable managed layout library exists; built from CSS 2.1 spec
- **net8.0 / netstandard2.0** — net8.0 for all engine packages; netstandard2.0 for `Abstractions` only (supports Roslyn SG references)
- **OpenTelemetry 1.9.0 + BCL Metrics** — ActivitySource + IMeter; repo-wide standard via `PdfTelemetryDescriptor`

Four new packages needed in `Directory.Packages.props`: `AngleSharp`, `AngleSharp.Css` (with pinning comment), `SixLabors.Fonts` (with license audit comment), `PdfSharpCore`.

### Expected Features

**Must have (table stakes — v0.1 launch):**
- HTML parsing + CSS cascade + block/inline layout with margin collapsing
- Table rendering (colspan/rowspan, `border-collapse: separate`)
- Image embedding (PNG, JPEG, base64 data URIs)
- `@page` rules, page breaks (before/after/inside), repeated headers/footers, page counters
- `@font-face` font embedding + Vietnamese diacritic stacking (SixLabors.Fonts)
- Deterministic byte-for-byte PDF output
- `PdfConfigs.Limits` security defaults (8 MB HTML, 256 DOM depth, 100k elements, 25 MP images, 1000 pages, 15s render, 32 fonts)
- PDF 1.7 hardened writer (JS/Launch/OpenAction/EmbeddedFile rejected)
- `AddPdf()` DI registration + OpenTelemetry instrumentation
- ≥40 golden snapshot tests + ≥10 Vietnamese snapshots

**Should have (v0.2 after OSS validation):**
- Source generator compile-time fast path (≥3× warm throughput, `IMPdfRendererFactory` seam already in v0.1)
- AOT-friendly mode + trim-safe annotations (Alpine container <40 MB)
- `Muonroi.Pdf.DesignSystem.Default` (invoice/receipt/report starter templates)
- ≥30% allocation reduction on hot path

**Defer to v1.0 Enterprise (need ≥3 paying prospects):**
- `Muonroi.Pdf.Enterprise.Registry` — Postgres template store, RBAC, audit trail
- `Muonroi.Pdf.Enterprise.HotReload` — Redis pub/sub, ≤5s cross-node propagation
- `Muonroi.Pdf.Enterprise.Canary` — SSIM rasterized diff, auto-rollback
- `Muonroi.Pdf.Enterprise.Designer` — web UI with live preview, <10s P95 round-trip

**Intentional anti-features (never implement):**
- Browser engine rendering (Chromium/Puppeteer) — hard stakeholder veto, native deps
- JavaScript execution — XSS/exfil vector in PDF context
- Outbound HTTP at render time — SSRF/exfil; `IResourceResolver` is bytes-only by design
- `border-collapse: collapse`, flexbox, grid, float, absolute positioning — deferred or out of declared scope

### Architecture Approach

The pipeline is a strict left-to-right gate sequence: pre-parse limits → HTML parse (`IHtmlParser`) → CSS cascade (`ICssCascadeEngine`) → policy gate (`IPdfCssPolicy`) → box tree → resource/font/image resolution → layout engine → PDF write (`IPdfWriter`). Each stage fails with a typed exception rather than silently degrading. All third-party library surfaces are hidden behind adapter interfaces in `Muonroi.Pdf.Abstractions` (`netstandard2.0`); the engine in `Muonroi.Pdf` (`net8.0`) holds only `Internal/` implementations. Multi-tenant cache keys derive exclusively from ambient `ITenantContext` — never from caller-supplied strings. Stream output (not `byte[]`) is the primary API to avoid LOH pressure on large documents.

**Major components:**
1. `Muonroi.Pdf.Abstractions` (netstandard2.0) — all public contracts, adapter interfaces, `PdfPolicyLimits`, `PdfRenderOptions`, `PdfRenderResult`
2. `Muonroi.Pdf` (net8.0) — AngleSharp adapters, hand-written box tree + layout engine, PdfSharpCore writer, `AddPdf()` DI registration, `PdfTelemetryDescriptor`
3. `Muonroi.Pdf.Governance` (net8.0) — `DefaultStrictPolicy` with signed config via `PolicyVerifier`
4. `Muonroi.Pdf.Enterprise` (net8.0, v0.1 stub) — namespace lock; Registry/HotReload/Canary/Designer at v1.0
5. `tests/Muonroi.Pdf.Tests` — xunit + FluentAssertions 7.2.0 (pinned, Apache 2.0) + NSubstitute; golden corpus + Vietnamese corpus

### Critical Pitfalls

1. **Box model misimplementation locks bugs into golden snapshots** — write golden tests against W3C CSS 2.1 conformance suite outputs, not against own rendering; implement BFC root detection before margin collapsing
2. **AngleSharp.Css beta API breaks without semver signal** — `ICssCascadeEngine` adapter seam must be a genuine interface from day 1, not a leaky wrapper; write cascade boundary integration tests (computed value assertions), not only final PDF output tests
3. **Deterministic output silently broken by timestamps/random object IDs** — set PDF document ID to SHA-256 of input HTML, fix creation date to epoch, strip producer timestamp in `IPdfWriter`; add determinism canary CI step (render same HTML twice, assert `bytes1.SequenceEqual(bytes2)`)
4. **SSRF via permissive `IResourceResolver`** — default must be `ThrowingResourceResolver`; never ship a convenience HTTP resolver; add Roslyn analyzer/CI check blocking `HttpClient` inside any `IResourceResolver` implementation
5. **Cross-tenant cache poisoning from caller-supplied cache keys** — `PdfCacheKey(TenantId, ContentHash)` must be a sealed internal type; `ITenantContext` resolved from DI ambient scope only, never from `PdfRenderOptions`
6. **Vietnamese diacritic silent fallback to replacement glyphs** — validate registered fonts at startup against Vietnamese Unicode block (U+1E00–U+1EFF); build Vietnamese golden corpus only with verified fonts (Noto Serif, Be Vietnam Pro)
7. **SixLabors.ImageSharp license threshold** — perform audit at M+1 before v0.1 NuGet publish; `IImageDecoder` seam enables swap to `StbImageSharp` (MIT, no threshold)

## Implications for Roadmap

### Phase 1: Abstractions + Contracts (Foundation)
**Rationale:** All downstream packages depend on `Muonroi.Pdf.Abstractions`. Ship contracts first so Phase 2+ work can proceed in parallel and adapter seams are locked before any implementation starts.
**Delivers:** `IMPdfService`, `IMPdfRenderer<T>`, `IMPdfRendererFactory`, all adapter interfaces (`IHtmlParser`, `ICssCascadeEngine`, `IImageDecoder`, `IPdfWriter`, `IFontResolver`, `IResourceResolver`, `IPdfCssPolicy`), `PdfPolicyLimits` (Strict + Relaxed presets), `PdfRenderOptions`, `PdfRenderResult`, CPM entries in `Directory.Packages.props`
**Addresses:** All P1 features (the contracts that gate every subsequent feature)
**Avoids:** Pitfall #2 (adapter seam locked before any AngleSharp.Css call is written), Pitfall #4 (bytes-only resolver contract defined before default impl), Pitfall #5 (sealed `PdfCacheKey` type defined before any cache logic)

### Phase 2: Core Pipeline — Parse, Cascade, Policy Gate
**Rationale:** HTML parse + CSS cascade + policy gate must be complete before any layout work begins; the policy gate is the security boundary that all layout correctness depends on.
**Delivers:** `AngleSharpHtmlParser`, `AngleSharpCssCascade`, `DefaultStrictPolicy` (Governance package), `ThrowingResourceResolver` as default, all CSS subset rejection with structured diagnostics
**Uses:** AngleSharp 1.3.x, AngleSharp.Css 1.0.0-beta.146, `Muonroi.Pdf.Governance`
**Implements:** Parse + cascade + policy gate stages of the pipeline
**Avoids:** Pitfall #9 (CSS policy built as strict allowlist from day 1), Pitfall #4 (ThrowingResourceResolver is the default, not convenience HTTP resolver)

### Phase 3: Box Tree + Layout Engine
**Rationale:** Most complex implementation work; must come after policy gate is solid so layout never runs on rejected CSS; W3C CSS 2.1 conformance test fixtures drive development.
**Delivers:** Block/inline layout with margin collapsing, BFC roots, IFC; table layout (colspan/rowspan, border-separate); `@page` + page breaks + headers/footers; `counter(pages)` two-pass support; pagination
**Addresses:** Block/inline layout (P1), table rendering (P1), `@page` + pagination (P1)
**Avoids:** Pitfall #1 (golden tests written against W3C conformance suite, not own output; BFC root detection first)

### Phase 4: Resource Resolution + Font + Image Pipeline
**Rationale:** Layout engine needs font metrics (SixLabors.Fonts) and decoded image pixels before it can compute line widths and embedded image sizes; these are prerequisites for correct layout.
**Delivers:** `IFontResolver` integration + SixLabors.Fonts subsetting + Vietnamese diacritic stacking; `IImageDecoder` (PNG/JPEG/data: URI); `IResourceResolver` with scheme allowlist; startup font validation against Vietnamese Unicode block
**Addresses:** Font embedding (P1), Vietnamese (P1), image embedding (P1)
**Avoids:** Pitfall #6 (startup validation before Vietnamese golden corpus committed), Pitfall #7 (SixLabors.ImageSharp license audit M+1 checkpoint)

### Phase 5: PDF Writer + Determinism + Security Hardening
**Rationale:** PDF write is the final pipeline stage; determinism and security hardening must be baked in before any golden corpus is committed or any NuGet package is published.
**Delivers:** `PdfSharpCoreWriter` with PDF 1.7, deterministic IDs (SHA-256 of input HTML), epoch creation date, JS/Launch/OpenAction/EmbeddedFile rejection; determinism canary CI step; cross-platform hash comparison (Windows + Linux CI matrix)
**Addresses:** Deterministic output (P1), PDF 1.7 hardened writer (P1)
**Avoids:** Pitfall #3 (determinism canary catches non-determinism before golden corpus is established), Pitfall #8 (IPdfWriter is genuinely swappable — verified by stub adapter integration test)

### Phase 6: DI Registration + Telemetry + `AddPdf()`
**Rationale:** Once all pipeline components are wired, register them under the public DI surface and expose the telemetry descriptor so the engine is ready for production observability.
**Delivers:** `AddPdf(IServiceCollection, IConfiguration)` with `TryAddSingleton` for all adapters; `PdfTelemetryDescriptor` (ActivitySource `Muonroi.BuildingBlock.Pdf`, IMeter, all spans and snake_case metrics); `PdfConfigs.Limits` bound from `IConfiguration`; multi-tenant cache with ambient `ITenantContext`
**Addresses:** DI registration (P1), OpenTelemetry instrumentation (P1), multi-tenant cache isolation
**Avoids:** Pitfall #5 (cache key sealed type wired to ambient ITenantContext, not PdfRenderOptions)

### Phase 7: Golden Snapshot Corpus + CI Gates
**Rationale:** Lock regressions before v0.1 publish; these tests are the sole regression guarantee for a deterministic renderer.
**Delivers:** ≥40 golden snapshot tests (block/inline/table/image/font coverage); ≥10 Vietnamese diacritic snapshots; determinism canary (render × 2, byte-equal assert); cross-platform CI matrix; `KNOWN-DEVIATIONS.md`; security smoke tests (file:// SSRF → PdfSecurityException, display:flex → PdfPolicyException, cross-tenant cache isolation)
**Addresses:** Test coverage (P1 quality gate)
**Avoids:** Pitfall #1 (golden corpus not committed until layout is verified against W3C suite), Pitfall #6 (Vietnamese snapshots not committed until startup font validation passes)

### Phase 8: v0.2 — Source Generator + AOT + DesignSystem
**Rationale:** Add only after OSS adoption metrics validate the core (download velocity, GitHub stars, issues). Source generator requires `IMPdfRendererFactory` seam already shipped in v0.1 — additive, no API break.
**Delivers:** Roslyn incremental source generator for `IMPdfRenderer<TModel>` (≥3× warm throughput); AOT/trim-safe annotations (validated via `PublishAot` sample, Alpine container <40 MB); `Muonroi.Pdf.DesignSystem.Default` (invoice/receipt/report templates, typography scale, color tokens); ≥30% hot-path allocation reduction
**Addresses:** P2 features
**Avoids:** Pitfall #10 (second FTE hired at M+3, source generator spike prototyped at M+4)

### Phase 9: v1.0 Enterprise — Registry, HotReload, Canary, Designer
**Rationale:** Defer until ≥3 Enterprise prospects confirm intent to pay. All Enterprise features sit on top of the stable OSS engine.
**Delivers:** `Muonroi.Pdf.Enterprise.Registry` (Postgres JSONB, RBAC, audit trail); `Muonroi.Pdf.Enterprise.HotReload` (Redis pub/sub, ≤5s tenant-scoped invalidation); `Muonroi.Pdf.Enterprise.Canary` (rasterize + SSIM diff, auto-rollback); `Muonroi.Pdf.Enterprise.Designer` (Blazor/React web UI, live preview <10s P95); `Muonroi.Pdf.Enterprise.License` (gates startup)
**Addresses:** P3 features

### Phase Ordering Rationale

- Phases 1→2→3→4→5 follow strict pipeline dependency: contracts before implementations, policy gate before layout, layout before font/image metrics, layout before PDF write.
- Phase 6 (DI) waits until all pipeline components are built — `AddPdf()` registers singletons that must exist.
- Phase 7 (golden corpus) is intentionally last in v0.1 — snapshots must not be committed until layout correctness is verified and determinism is enforced; committing golden tests against a buggy layout locks in bugs.
- Phase 8 requires the `IMPdfRendererFactory` seam from Phase 6; no API break because the seam ships in v0.1 with a runtime implementation.
- Phase 9 requires a stable, published OSS engine as its rendering backend — justified by the M+7 FTE ramp-up date in PROJECT.md.

### Research Flags

Phases needing deeper research during planning:
- **Phase 3 (Box Tree + Layout):** CSS 2.1 margin collapsing edge cases (12+ scenarios), BFC root promotion rules, two-pass `counter(pages)` design — study W3C spec sections §8.3.1, §9.4.1, §12.4 before implementation sprint begins
- **Phase 4 (Font Pipeline):** SixLabors.Fonts subsetting API surface, glyph metric caching strategy for Vietnamese — needs spike in M+2 timeframe
- **Phase 8 (Source Generator):** Roslyn incremental generator patterns, `ISourceGenerator` vs `IIncrementalGenerator` trade-offs — prototype spike needed at M+4

Phases with standard/well-documented patterns (can skip dedicated research):
- **Phase 1 (Abstractions):** Interface design from PROJECT.md is already authoritative; no additional research needed
- **Phase 2 (Parse + Cascade):** AngleSharp + AngleSharp.Css APIs are stable for integration pattern; adapter seam pattern is proven from existing `Muonroi.Caching.Redis`
- **Phase 6 (DI + Telemetry):** `RedisExtensions.cs` + `OtelSetup.cs` are the proven templates; copy-adapt, don't redesign

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Fully constrained by no-native rule; confirmed against Directory.Packages.props and PROJECT.md; alternatives exhaustively rejected |
| Features | HIGH | Derived directly from PROJECT.md requirements, key decisions (D1–D20), and out-of-scope declarations; no speculation |
| Architecture | HIGH | Grounded in actual `src/Muonroi.Pdf.Abstractions/` interface files and `RedisExtensions.cs` DI pattern; read 2026-05-26 |
| Pitfalls | HIGH | CSS 2.1 layout failure modes, PDF determinism, SSRF, beta dependency management — all well-documented domain failure patterns |

**Overall confidence:** HIGH

### Gaps to Address

- **SixLabors.ImageSharp transitive pull:** Whether `SixLabors.Fonts 2.1.x` pulls `SixLabors.ImageSharp` as a transitive dependency is not confirmed — run `dotnet list package --include-transitive` after first project reference and trigger the license audit if it appears
- **AngleSharp.Css beta.146 trim-safety:** Trim-safe annotation status of `AngleSharp.Css` beta is unverified — must be confirmed before v0.2 AOT work; fallback is isolating the cascade stage behind an AOT-hostile seam boundary
- **PdfSharpCore coordinate system Y-axis inversion:** Implementation detail confirmed in domain knowledge but must be verified against actual PdfSharpCore API during Phase 5 spike to avoid layout ↔ PDF coordinate mapping errors
- **`counter(pages)` two-pass design:** Two-pass layout for total-page-count footer requires the layout engine to support a re-entry point — this architectural choice must be made in Phase 3 design, not retrofitted

## Sources

### Primary (HIGH confidence)
- `D:\sources\Core\muonroi-building-block\.planning\PROJECT.md` — authoritative specification (decisions D1–D20, requirements v0.1/v0.2/v1.0, out-of-scope declarations)
- `D:\sources\Core\muonroi-building-block\Directory.Packages.props` — authoritative CPM version list; confirmed existing packages
- `D:\sources\Core\muonroi-building-block\src\Muonroi.Pdf.Abstractions\` — actual interface definitions read 2026-05-26
- `D:\sources\Core\muonroi-building-block\src\Muonroi.Caching.Redis\Redis\RedisExtensions.cs` — DI + telemetry pattern reference

### Secondary (MEDIUM confidence)
- AngleSharp GitHub — 1.3.x current stable; AngleSharp.Css latest beta is 1.0.0-beta.146
- SixLabors.Fonts GitHub — 2.1.x Apache 2.0; subsetting API confirmed from documentation
- PdfSharpCore GitHub — 1.3.x MIT, pure-managed; maintenance status monitored
- W3C CSS 2.1 specification §8.3.1, §9.4.1, §12.4 — margin collapsing, BFC, counter rules

### Tertiary (informational)
- wkhtmltopdf archived status (December 2023) — confirms TCIS migration urgency
- IronPDF/Syncfusion system requirements — native dependency status confirms competitive gap
- OWASP PDF security guidance — SSRF, JS injection patterns

---
*Research completed: 2026-05-26*
*Ready for roadmap: yes*
