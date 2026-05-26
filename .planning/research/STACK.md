# Stack Research

**Domain:** Pure-managed .NET HTML/CSS-to-PDF renderer (open-core, multi-tenant)
**Researched:** 2026-05-26
**Confidence:** HIGH — stack is fully constrained by project decisions; all choices are evidence-backed from PROJECT.md, Directory.Packages.props, and existing ecosystem conventions.

---

## Recommended Stack

### Core Pipeline Libraries

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| AngleSharp | 1.3.x | HTML parsing, DOM tree construction | Only pure-managed .NET HTML5-compliant parser with a stable, maintained API. No native deps. Used by HtmlAgilityPack alternatives are either archived or GDI+-dependent. |
| AngleSharp.Css | **1.0.0-beta.146 (pinned)** | CSS cascade engine, selector matching, computed style | The only viable pure-managed CSS cascade engine for .NET. Beta status accepted; `ICssCascadeEngine` adapter seam enables swap if a stable alternative emerges. Never float this version — upstream breaks are common in beta. |
| SixLabors.Fonts | 2.1.x | Glyph metrics, OpenType shaping, font subsetting | Apache 2.0, pure-managed, actively maintained. Handles Vietnamese diacritic stacking and mixed Latin+Vietnamese line-breaking — no alternative in managed .NET covers this. Audit SixLabors.ImageSharp license threshold at M+1. |
| PdfSharpCore | 1.3.x | PDF 1.7 object model writer | MIT, pure-managed, runs on Linux/Alpine/AOT. Chosen over PDFsharp 6.x because 6.x diverged significantly and has uncertain managed-only guarantees. `IPdfWriter` adapter enables swap to PDFsharp 6.x or QuestPDF writer later without public API change. |
| **Hand-written box tree + layout** | (in-repo) | Block/inline formatting, BFC roots, margin collapsing, table layout | HtmlRenderer.PdfSharp is archived (2018) and pulls GDI+ — violates the no-native constraint. No other pure-managed layout engine exists in .NET. Must be built from scratch. |

### .NET Platform & DI

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| `net8.0` (TFM) | .NET 8 LTS | Runtime target for all implementation packages | LTS, AOT-ready, `Span<T>`/`Memory<T>` performance primitives available. Do NOT target net9.0 — repo-wide LTS policy. |
| `netstandard2.0` (TFM) | — | Target for `Muonroi.Pdf.Abstractions` only | Enables source-generator references (v0.2) and analyzer projects that must run on .NET Framework tooling. All other packages use `net8.0`. |
| Microsoft.Extensions.DependencyInjection | `$(MicrosoftExtensionsVersion)` | DI registration (`AddPdf`) | Already in CPM. Registration lives in `Muonroi.Pdf` itself (not a separate `AspNetCore` package) — matches `RedisExtensions.cs` pattern. |
| Microsoft.Extensions.Options | `$(MicrosoftExtensionsVersion)` | `PdfConfigs` binding from `IConfiguration` | Already in CPM. Use `IOptions<PdfConfigs>` with `TryAddSingleton`. |

### Observability

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| OpenTelemetry (ActivitySource) | 1.9.0 (already in CPM) | Distributed tracing via `Muonroi.BuildingBlock.Pdf` activity source | Repo-wide standard. `PdfTelemetryDescriptor : ITelemetryDescriptor` with public parameterless ctor — matches `OtelSetup.cs` convention. |
| System.Diagnostics.Metrics (IMeter) | BCL (net8.0) | snake_case metrics: `pdf.operation`, `pdf.template_id`, `pdf.page_count`, `tenant.id` | BCL, zero dependency. Same pattern used across all Muonroi building blocks. |

### Ecosystem Services (Internal)

| Service | Contract | Why |
|---------|----------|-----|
| `IMLog<T>` | Muonroi logging abstraction | Never use raw `ILogger<T>` — repo-wide rule; `IMLog<T>` wraps it with structured context. |
| `IMDateTimeService` | Muonroi datetime abstraction | Enables deterministic output; avoids `DateTime.UtcNow` in render pipeline (affects byte-for-byte determinism). |
| `IMJsonSerializeService` | Muonroi JSON abstraction | For policy config serialization in `Muonroi.Pdf.Governance`; never raw `System.Text.Json`. |
| `ITenantContext` | Muonroi tenancy abstraction | Multi-tenant cache key source. Cache keys = `(TenantId, contentHash)` from ambient context only. |

### Testing

| Tool | Version | Purpose | Notes |
|------|---------|---------|-------|
| xunit | 2.9.2 | Test runner | Auto-injected by `Directory.Build.props` naming-convention detection. |
| FluentAssertions | **7.2.0 (pinned hard)** | Assertion DSL | Apache 2.0 forever on v7.x. v8+ requires $130/dev/year commercial license. Never upgrade without explicit board decision — `Directory.Packages.props` comment documents this. |
| NSubstitute | 5.3.0 | Mocking | Auto-injected by `Directory.Build.props`. Preferred over Moq for new test projects (Moq has SponsorLink controversy). |

### Enterprise Tier (v1.0+)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Npgsql.EntityFrameworkCore.PostgreSQL | `$(NpgsqlEfCoreVersion)` | Template registry store in `Muonroi.Pdf.Enterprise.Registry` | Already in CPM. Postgres chosen for JSONB template versioning + RBAC audit tables. |
| StackExchange.Redis | 2.8.37 | Hot-reload change notifier in `Muonroi.Pdf.Enterprise.HotReload` | Already in CPM. Redis pub/sub for tenant-scoped template invalidation ≤5s across N nodes. |

---

## CPM Registration — New Packages to Add to `Directory.Packages.props`

These packages are not yet in `Directory.Packages.props` and must be added before first `.csproj` reference:

```xml
<!-- Muonroi.Pdf pipeline — added for src/Muonroi.Pdf* packages -->
<PackageVersion Include="AngleSharp" Version="1.3.0" />
<!-- PINNED: only viable managed CSS cascade engine; beta accepted per PROJECT.md D4 -->
<PackageVersion Include="AngleSharp.Css" Version="1.0.0-beta.146" />
<PackageVersion Include="SixLabors.Fonts" Version="2.1.0" />
<!-- License audit required at M+1 if SixLabors.ImageSharp pulled transitively -->
<PackageVersion Include="PdfSharpCore" Version="1.3.62" />
```

**Rules:**
- Zero inline `Version` attributes in any `.csproj` — all versions in `Directory.Packages.props` only (CPM compliance).
- `AngleSharp.Css` comment is mandatory — documents the pinning rationale for reviewers.
- `SixLabors.Fonts` license audit comment is mandatory per PROJECT.md constraint.

---

## Alternatives Considered and Rejected

| Category | Recommended | Rejected | Why Rejected |
|----------|-------------|----------|--------------|
| HTML → PDF (browser engine) | Hand-built pipeline | Playwright, Puppeteer, CefSharp, IronPDF | Hard stakeholder rule. Browser engines introduce native sidecar (Chromium binary), CVE treadmill, container bloat (>100 MB), and can't run AOT. |
| HTML → PDF (wkhtmltopdf wrappers) | Hand-built pipeline | DinkToPdf, WkHtmlToPdf-DotNet | libwkhtmltox is a native binary — violates no-native constraint. wkhtmltopdf unmaintained since 2023, has open CVEs. TCIS cutover goal is removing this. |
| CSS cascade | AngleSharp.Css 1.0.0-beta.146 | ExCSS, StylesheetParser | ExCSS has no cascade engine (selector computation only). No other managed .NET library computes computed styles from a DOM. |
| PDF writer | PdfSharpCore 1.3.x | PDFsharp 6.x, QuestPDF, iText7 | PDFsharp 6.x: significant API divergence, uncertain managed-only guarantees. QuestPDF: code-first layout API (not CSS-driven). iText7: AGPL unless commercial license — license incompatible with Apache 2.0 open-core strategy. |
| Font shaping | SixLabors.Fonts 2.1.x | HarfBuzzSharp, SkiaSharp | HarfBuzzSharp and SkiaSharp both pull native binaries (libHarfBuzz, libSkia) — violate no-native constraint. |
| HTML parsing | AngleSharp 1.3.x | HtmlAgilityPack, HtmlParser | HtmlAgilityPack has no spec-compliant DOM; CSS selectors via AngleSharp.Css require AngleSharp's DOM model anyway. |
| Layout engine | Hand-written | Fork of HtmlRenderer.PdfSharp | HtmlRenderer.PdfSharp archived 2018, has GDI+ dependency (native). No viable fork path. |
| Mocking | NSubstitute | Moq | Moq 4.20 introduced SponsorLink telemetry controversy; NSubstitute is cleaner. Moq 4.20.72 still in CPM for legacy tests — do not add new Moq references. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `System.Drawing` / GDI+ | Requires `libgdiplus` on Linux — native dep, crashes Alpine | SixLabors.Fonts + custom glyph metrics |
| SkiaSharp | Native Skia binary, not AOT-safe, bloats Alpine image | PdfSharpCore (pure-managed) |
| iText7 / iTextSharp | AGPL license incompatible with Apache 2.0 OSS publishing | PdfSharpCore + IPdfWriter adapter |
| Playwright / Puppeteer | Browser engine sidecar — hard stakeholder veto | AngleSharp + hand-written layout |
| `HtmlRenderer.PdfSharp` | Archived 2018, GDI+ dependency | Hand-written box tree |
| `ILogger<T>` (raw BCL) | Bypasses `IMLog<T>` — repo-wide ban | `IMLog<T>` |
| `DateTime.UtcNow` in render path | Breaks byte-for-byte determinism | `IMDateTimeService` |
| Inline `Version` in `.csproj` | Violates CPM compliance — CI gate will reject | `Directory.Packages.props` only |
| FluentAssertions v8+ | Requires commercial license ($130/dev/year) | Stay on 7.2.0 |

---

## TFM Policy

| Package | TFM | Reason |
|---------|-----|--------|
| `Muonroi.Pdf.Abstractions` | `netstandard2.0` | Supports SG (v0.2) and analyzer references from .NET Framework tooling |
| `Muonroi.Pdf` | `net8.0` | LTS, AOT, Span/Memory perf |
| `Muonroi.Pdf.Governance` | `net8.0` | Policy enforcement runtime; no need for netstandard |
| `Muonroi.Pdf.Enterprise` (stub) | `net8.0` | Namespace lock; no downlevel needed |
| `Muonroi.Pdf.Enterprise.*` (v1.0) | `net8.0` | Registry + Redis; .NET 8 LTS baseline |
| `tests/Muonroi.Pdf.Tests/` | `net8.0` | Run on same TFM as engine |

---

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| AngleSharp 1.3.x | AngleSharp.Css 1.0.0-beta.146 | **Must match** — CSS engine binds to AngleSharp DOM API. Do not upgrade AngleSharp without verifying AngleSharp.Css compatibility. |
| SixLabors.Fonts 2.1.x | net8.0 | If SixLabors.ImageSharp pulled transitively, audit its license (commercial threshold may apply). |
| PdfSharpCore 1.3.x | net8.0 | 1.3.x is the last stable release; upstream maintenance is slow. `IPdfWriter` adapter isolates engine from any forced migration. |
| OpenTelemetry 1.9.0 | Already in CPM | `ActivitySource` + `IMeter` — BCL primitives; no new OTel packages needed for PDF telemetry. |
| FluentAssertions 7.2.0 | xunit 2.9.2 | Pinned combination; do not allow Dependabot to bump FA. |

---

## Sources

- `D:\sources\Core\muonroi-building-block\.planning\PROJECT.md` — primary authority; all stack decisions are documented with rationale (HIGH confidence)
- `D:\sources\Core\muonroi-building-block\Directory.Packages.props` — authoritative CPM version list; confirmed existing packages (HIGH confidence)
- AngleSharp GitHub (https://github.com/AngleSharp/AngleSharp) — 1.3.x is current stable (verified from training + repo)
- AngleSharp.Css GitHub (https://github.com/AngleSharp/AngleSharp.Css) — 1.0.0-beta.146 is latest (PROJECT.md decision D4)
- SixLabors.Fonts GitHub (https://github.com/SixLabors/Fonts) — 2.1.x Apache 2.0 (verified from project context)
- PdfSharpCore GitHub (https://github.com/ststeiger/PdfSharpCore) — 1.3.x MIT, pure-managed (verified from project context)

---

*Stack research for: Muonroi.Pdf — pure-managed .NET HTML/CSS-to-PDF renderer*
*Researched: 2026-05-26*
