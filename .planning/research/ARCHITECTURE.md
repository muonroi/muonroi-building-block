# Architecture Research

**Domain:** Pure-managed HTML/CSS-to-PDF rendering library (.NET open-core)
**Researched:** 2026-05-26
**Confidence:** HIGH — derived from actual `src/Muonroi.Pdf.Abstractions/` code, `Muonroi.Caching.Redis/RedisExtensions.cs` DI pattern, and PROJECT.md constraints. No training-data speculation.

---

## Standard Architecture

### System Overview

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                            Caller Layer                                        │
│  IMPdfService.RenderAsync()   IMPdfRenderer<T>.RenderAsync()                  │
│  (direct HTML)                (typed model via IMPdfRendererFactory)           │
└──────────────────────────────────────┬─────────────────────────────────────────┘
                                       │
┌──────────────────────────────────────▼─────────────────────────────────────────┐
│                          Render Pipeline (Muonroi.Pdf)                         │
│                                                                                │
│  ┌──────────────┐   ┌──────────────┐   ┌───────────────┐   ┌───────────────┐  │
│  │  IHtmlParser │──▶│ICssCascade   │──▶│ IPdfCssPolicy │──▶│  Box Tree     │  │
│  │  (AngleSharp)│   │  Engine      │   │ (policy gate) │   │  Builder      │  │
│  │              │   │ (AngleSharp  │   │               │   │  (hand-written│  │
│  └──────────────┘   │  .Css)       │   └───────────────┘   │  layout)      │  │
│                     └──────────────┘                        └───────┬───────┘  │
│                                                                     │          │
│  ┌──────────────┐   ┌──────────────┐   ┌───────────────┐           │          │
│  │ IFontResolver│   │IResourceResolver│ │ IImageDecoder │           │          │
│  │ (bytes-only) │   │ (bytes-only)  │  │ (bytes-only)  │           │          │
│  └──────┬───────┘   └──────┬────────┘  └──────┬────────┘           │          │
│         │                  │                   │                    ▼          │
│         └──────────────────┴───────────────────┴──────────▶ Layout Engine     │
│                                                             (block/inline/     │
│                                                              table/pagination) │
│                                                                     │          │
│                                                          ┌──────────▼───────┐  │
│                                                          │  IPdfWriter      │  │
│                                                          │ (PdfSharpCore)   │  │
│                                                          └──────────┬───────┘  │
└─────────────────────────────────────────────────────────────────────┼──────────┘
                                                                      │
                                                               Stream destination
                                                               (caller-owned)
```

### Pipeline Stages and Gate Conditions

| Stage | Implementation | Gate Condition | Fails With |
|-------|---------------|----------------|------------|
| Pre-parse limits | `PdfPolicyLimits` | `MaxHtmlBytes` (2 MiB strict / 8 MiB relaxed) | `PdfRenderException` |
| HTML parse | `IHtmlParser` → AngleSharp | `MaxDomDepth` 256, `MaxElementCount` 50k | `PdfParseException` |
| CSS cascade | `ICssCascadeEngine` → AngleSharp.Css 1.0.0-beta.146 (pinned) | `MaxStylesheetBytes` 512 KiB, `MaxSelectorsPerSheet` 10k | `PdfCascadeException` |
| Policy gate | `IPdfCssPolicy.ValidateAsync()` | CSS subset rules (no flex/grid/absolute/JS/SVG filters) | `PolicyValidationResult` with structured diagnostics |
| Resource resolve | `IResourceResolver` (bytes-only) | `MaxEmbeddedResourceBytes` 8 MiB; scheme allowlist | `ResourceResolvedException` |
| Image decode | `IImageDecoder` (bytes-only) | `MaxImagePixels` 25 MP | `ImageDecodeException` |
| Font resolve | `IFontResolver` (bytes-only) | `MaxFontFileBytes` 4 MiB; TTF/OTF only | `FontResolveException` |
| Layout | Hand-written box tree | `MaxPages` 1000; `RenderTimeout` 15s | `LayoutException` |
| PDF write | `IPdfWriter` → PdfSharpCore | v1.7 pinned; JS/Launch/OpenAction/EmbeddedFile rejected | `PdfWriteException` |

---

## Package Structure

```
muonroi-building-block/
├── src/
│   ├── Muonroi.Pdf.Abstractions/          # netstandard2.0 — public contracts only
│   │   ├── IMPdfService.cs                # Primary render entry point
│   │   ├── IMPdfRenderer.cs               # IMPdfRenderer<T> + IMPdfRendererFactory
│   │   ├── IFontResolver.cs               # FontRequest, FontWeight, FontStyle
│   │   ├── IResourceResolver.cs           # ResourceResult (bytes + ContentType)
│   │   ├── PdfRenderOptions.cs            # Per-call options (page size, margins, policy override)
│   │   ├── PdfRenderResult.cs             # PageCount, ByteCount, Elapsed, TemplateHash, PolicyId
│   │   ├── PdfPageSize.cs / PdfMargins.cs / PdfOrientation.cs / PdfHeaderFooter.cs
│   │   └── Policy/
│   │       ├── IPdfCssPolicy.cs           # + IPdfDocumentContext (opaque DOM context)
│   │       ├── PdfPolicyLimits.cs         # All numerical limits; Strict + Relaxed presets
│   │       └── PolicyValidationResult.cs
│   │
│   ├── Muonroi.Pdf/                       # net8.0 — engine + DI
│   │   ├── Extensions/
│   │   │   └── PdfExtensions.cs           # AddPdf(IServiceCollection, IConfiguration)
│   │   ├── Internal/
│   │   │   ├── AngleSharpHtmlParser.cs    # IHtmlParser impl
│   │   │   ├── AngleSharpCssCascade.cs    # ICssCascadeEngine impl
│   │   │   ├── BoxTree/                   # Hand-written block/inline/table tree
│   │   │   ├── Layout/                    # Layout engine (BFC, margin collapse, pagination)
│   │   │   ├── PdfSharpCoreWriter.cs      # IPdfWriter impl
│   │   │   └── PdfTelemetryDescriptor.cs  # ITelemetryDescriptor impl
│   │   └── PdfService.cs                  # IMPdfService impl (singleton)
│   │
│   ├── Muonroi.Pdf.Governance/            # net8.0 — policy enforcement
│   │   └── DefaultStrictPolicy.cs         # IPdfCssPolicy with signed config via PolicyVerifier
│   │
│   └── Muonroi.Pdf.Enterprise/            # net8.0 — commercial stub (v0.1: namespace lock only)
│       └── AssemblyInfo.cs                # <IsCommercialPackage>true</IsCommercialPackage>
│
├── tests/
│   └── Muonroi.Pdf.Tests/                 # xunit + FluentAssertions 7.2.0 + NSubstitute
│       ├── Golden/                        # ≥40 snapshot tests (block/inline/table/image/font)
│       └── Vietnamese/                    # ≥10 Vietnamese diacritic snapshots
│
└── samples/
    ├── Quickstart.Pdf/                    # Minimal AddPdf() usage
    └── Muonroi.Pdf.Sample/               # Invoice/receipt/report templates
```

### Why This Structure

- **`Muonroi.Pdf.Abstractions` on `netstandard2.0`:** Allows v0.2 Roslyn source generators (`netstandard2.0` is the only TFM analyzers can reference) and downstream analyzer projects to reference contracts without pulling in the engine. This is the same reason `Muonroi.Caching.Abstractions` is separated from `Muonroi.Caching.Redis`.
- **`Internal/` namespace for engine internals:** Prevents callers from depending on `AngleSharpHtmlParser`, `PdfSharpCoreWriter`, etc. All third-party library dependencies are hidden behind the adapter interfaces. If AngleSharp.Css drops the beta, swap `AngleSharpCssCascade.cs` — callers see nothing.
- **No `Muonroi.Pdf.AspNetCore` package:** DI lives in `Muonroi.Pdf` (namespace `Muonroi.Pdf.Extensions`), matching `Muonroi.Caching.Redis/Redis/RedisExtensions.cs`. One fewer package removes a publish/version surface.

---

## Architectural Patterns

### Pattern 1: Bytes-Only Resolver Seam

**What:** Every external resource (fonts, images, CSS background URLs) enters the engine exclusively as `ReadOnlyMemory<byte>`. The resolver contract (`IFontResolver`, `IResourceResolver`) never exposes a file path, URI string, or stream the engine itself opens.

**Why use it:** Closes `file://` SSRF and `http(s)://` exfiltration paths at the design-time contract level. An attacker who controls the HTML cannot make the engine dereference `url(file:///etc/passwd)` because `IResourceResolver.ResolveAsync()` receives a `Uri` and returns `ResourceResult?` — the engine's built-in implementation rejects non-data: schemes unless the policy explicitly opts in.

**Implementation:**

```csharp
// IResourceResolver — engine never opens the URI itself
ValueTask<ResourceResult?> ResolveAsync(Uri uri, string? contentTypeHint, CancellationToken ct);

// ResourceResult is bytes + ContentType — no stream the engine forwards elsewhere
public sealed record ResourceResult(ReadOnlyMemory<byte> Bytes, string ContentType);
```

**When to use:** Always. There is no opt-out. Even in trusted-template scenarios, resolvers must return bytes; the policy controls which schemes are allowed, not the engine's resolver dispatch.

### Pattern 2: Policy Gate Before Layout

**What:** After HTML parse and CSS cascade, but before the box tree is built, `IPdfCssPolicy.ValidateAsync()` receives an `IPdfDocumentContext` with aggregate counts (element count, max depth, stylesheet bytes, source HTML bytes) and returns a `PolicyValidationResult` describing which CSS properties/features are accepted, rejected, or warned.

**Why use it:** Layout is expensive (box tree allocation, BFC roots, pagination). Rejecting unsupported CSS (flex, grid, absolute positioning, `border-collapse: collapse`) early — with structured diagnostic messages — is cheaper than silently ignoring them in layout and producing a wrong output.

**Implementation:**

```csharp
public interface IPdfDocumentContext
{
    int ElementCount { get; }
    int MaxDepth { get; }
    long TotalStylesheetBytes { get; }
    long SourceHtmlBytes { get; }
}

// DefaultStrictPolicy (Muonroi.Pdf.Governance) rejects: flex, grid,
// position:absolute/fixed/sticky, float, JS, SVG filters, @import
```

**When to use:** Register `DefaultStrictPolicy` from `Muonroi.Pdf.Governance` for all untrusted templates. Override `PdfRenderOptions.Policy` per-call for trusted internal templates that need `PdfPolicyLimits.Relaxed`.

### Pattern 3: Adapter Seam for Third-Party Dependencies

**What:** The four third-party libraries with non-trivial API surfaces — AngleSharp (parse), AngleSharp.Css (cascade), SixLabors image decode, PdfSharpCore (write) — are each hidden behind a one-class adapter implementing an interface defined in `Muonroi.Pdf.Abstractions`.

**Why use it:** AngleSharp.Css is pinned at `1.0.0-beta.146` because it is the only viable managed CSS cascade engine for .NET. When a stable release ships (or an alternative emerges), the swap is `AngleSharpCssCascade.cs` only — callers and tests are unaffected. Same for `IPdfWriter`: if PdfSharpCore stagnates, swap to upstream PDFsharp 6.x without API breakage.

**Adapter seam table:**

| Interface | In-process impl (v0.1) | Swap trigger |
|-----------|------------------------|--------------|
| `IHtmlParser` | `AngleSharpHtmlParser` wraps AngleSharp 1.3.x | AngleSharp major version break |
| `ICssCascadeEngine` | `AngleSharpCssCascade` wraps AngleSharp.Css beta.146 | Beta stabilises, API changes |
| `IImageDecoder` | Built-in PNG/JPEG/data: URI decoder | SixLabors.ImageSharp license threshold |
| `IPdfWriter` | `PdfSharpCoreWriter` wraps PdfSharpCore 1.3.x | PdfSharpCore stagnates |

### Pattern 4: Ambient Tenant Context — Never Caller-Supplied Cache Keys

**What:** Every internal cache in the rendering engine is keyed on `(ITenantContext.TenantId, contentHash)` where `TenantContext` is resolved from DI ambient context — not a parameter the caller passes.

**Why use it:** This is the same invariant enforced in `Muonroi.Caching.Redis` (`DistributedCacheKeyBuilder.NormalizeTenantId(TenantContext.CurrentTenantId)`). A caller who passes a `templateId` in `PdfRenderOptions` does so for telemetry tagging only — that string never reaches the cache key. This closes cross-tenant cache poisoning.

**Implementation:**

```csharp
// PdfRenderOptions — TemplateId is telemetry-only
public string? TemplateId { get; init; }  // NOT used for cache key

// Engine internal:
string cacheKey = $"{tenantContext.TenantId}:{contentHash}";
// contentHash = SHA256(html + options fingerprint), computed internally
```

**Important:** `PdfRenderOptions` documents this explicitly: "Tenant context flows ambiently via `ITenantContext` resolved from DI — do not thread a tenant identifier through this options record."

### Pattern 5: DI Registration — `TryAddSingleton` Co-located in Engine Package

**What:** `AddPdf(IServiceCollection, IConfiguration)` lives in `Muonroi.Pdf.Extensions` (inside `Muonroi.Pdf`), not in a separate `Muonroi.Pdf.AspNetCore` package. Registration uses `TryAddSingleton` so the host can substitute any adapter before calling `AddPdf`.

**Why use it:** Mirrors `RedisExtensions.cs` exactly. Fewer packages = fewer NuGet surface areas to version. `TryAddSingleton` is idempotent: tests can pre-register mock implementations before calling `AddPdf` and the engine respects them.

**Implementation:**

```csharp
public static IServiceCollection AddPdf(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.TryAddSingleton<IHtmlParser, AngleSharpHtmlParser>();
    services.TryAddSingleton<ICssCascadeEngine, AngleSharpCssCascade>();
    services.TryAddSingleton<IImageDecoder, DefaultImageDecoder>();
    services.TryAddSingleton<IPdfWriter, PdfSharpCoreWriter>();
    services.TryAddSingleton<IPdfCssPolicy, DefaultStrictPolicy>();  // from Muonroi.Pdf.Governance
    services.TryAddSingleton<IFontResolver, NullFontResolver>();      // caller replaces
    services.TryAddSingleton<IResourceResolver, DataUriOnlyResolver>();
    services.TryAddSingleton<IMPdfRendererFactory, RuntimePdfRendererFactory>();
    services.TryAddSingleton<IMPdfService, PdfService>();
    return services;
}
```

### Pattern 6: Stream Output, Not `byte[]`

**What:** `IMPdfService.RenderAsync()` writes to a caller-owned `Stream destination` and returns `PdfRenderResult` (metadata only). The `RenderToBytesAsync()` overload exists for convenience but is documented as "Prefer the Stream overload for production paths."

**Why use it:** Multi-page invoices and reports can be 10–100 MB. Materialising them as `byte[]` on the LOH causes GC pressure in high-throughput services. Writing to a `FileStream`, `PipeWriter`, or `MemoryStream` the caller controls keeps the engine allocation-neutral for the document body.

---

## Data Flow

### Single-Document Render

```
Caller: IMPdfService.RenderAsync(html, destinationStream, options, ct)
    │
    ▼
[Pre-gate] PdfPolicyLimits.MaxHtmlBytes check → ArgumentException if exceeded
    │
    ▼
[Parse] IHtmlParser.ParseAsync(html) → IDocument
    │
    ▼
[Cascade] ICssCascadeEngine.ApplyAsync(document, options.UserStyleSheet) → styled DOM
    │
    ▼
[Policy gate] IPdfCssPolicy.ValidateAsync(IPdfDocumentContext)
    │           PolicyValidationResult.IsAccepted == false → PdfPolicyException
    ▼
[Box tree] BoxTreeBuilder.Build(styledDom) → BoxNode tree
    │
    ▼
[Resource/font resolution] (concurrent, bounded by limits)
    IResourceResolver.ResolveAsync() → ResourceResult?
    IFontResolver.ResolveAsync()     → ReadOnlyMemory<byte>?
    IImageDecoder.DecodeAsync()      → DecodedImage
    │
    ▼
[Layout] LayoutEngine.Layout(boxTree, pageConstraints) → LayoutResult
    │       PageBreakOptimiser, BFCRoot, MarginCollapser
    ▼
[Write] IPdfWriter.WriteAsync(layoutResult, destinationStream)
    │       PDF 1.7, deterministic IDs, no timestamp, JS/EmbeddedFile rejected
    ▼
PdfRenderResult(pageCount, byteCount, elapsed, templateHash, policyId)
```

### Typed Renderer Flow (v0.1 runtime, v0.2 source-generated)

```
Caller: IMPdfRendererFactory.Get<TModel>(templateId)
    │                                                
    ▼                                                
RuntimePdfRendererFactory → looks up registered template by templateId
    │
    ▼
IMPdfRenderer<TModel>.RenderAsync(model, destination, options, ct)
    │
    ▼
[Template expansion] Scriban/equivalent renders model → HTML string
    │
    ▼
IMPdfService.RenderAsync(html, destination, options, ct)  ← same pipeline
```

In v0.2, the source generator emits a class implementing `IMPdfRenderer<TModel>` that replaces Scriban expansion with a compile-time string interpolation — same interface, same downstream pipeline, no API breakage.

---

## Component Responsibilities

| Component | Responsibility | Thread Safety |
|-----------|----------------|---------------|
| `IMPdfService` (PdfService) | Orchestrates the render pipeline per call | Singleton-safe; per-call state via local variables |
| `IHtmlParser` (AngleSharpHtmlParser) | HTML5 parsing → DOM; enforces MaxDomDepth + MaxElementCount | Stateless; singleton-safe |
| `ICssCascadeEngine` (AngleSharpCssCascade) | CSS cascade: specificity, inheritance, computed values | Stateless per call; singleton-safe |
| `IPdfCssPolicy` (DefaultStrictPolicy) | Validates post-cascade document against CSS subset rules | Stateless; singleton-safe |
| `BoxTreeBuilder` | Translates styled DOM to box tree (block/inline/table containers) | Stateless; per-call allocation |
| `LayoutEngine` | Computes positions, handles page breaks, BFC roots, margin collapse | Stateless; per-call allocation |
| `IPdfWriter` (PdfSharpCoreWriter) | Serialises layout result to PDF 1.7 binary; hardened defaults | Stateless; per-call allocation |
| `IFontResolver` | Maps font-face declarations to bytes; implementations cache internally | Implementation-defined |
| `IResourceResolver` | Maps resource URIs to bytes; default is data:-only | Stateless (default) |
| `IImageDecoder` | Decodes PNG/JPEG/data: URI bytes to pixel data | Stateless |
| `IMPdfRendererFactory` | Registry of `IMPdfRenderer<T>` keyed by templateId | Singleton; immutable after startup |
| `PdfTelemetryDescriptor` | Exposes ActivitySource + IMeter; parameterless ctor required | Singleton |

---

## Security Architecture

### Threat Model and Mitigations

| Threat | Attack Vector | Mitigation |
|--------|--------------|------------|
| SSRF via `url()` | `background-image: url(file:///etc/passwd)` in template | `IResourceResolver` is bytes-only; engine never calls `File.Open()`. `AllowFileScheme = false` (default). |
| HTTP exfiltration | `<img src="https://attacker.com/beacon">` | `IResourceResolver` default rejects non-data: schemes. Policy's `AllowedSchemes` must explicitly permit. |
| DoS via DOM bomb | Nested `<div>` 100,000 deep with CSS selectors | `MaxDomDepth = 256`, `MaxElementCount = 50,000`, `MaxSelectorsPerSheet = 10,000` at pre-layout gate. |
| Memory bomb via image | 1×1 PNG that decompresses to 4 GB | `MaxImagePixels = 25,000,000` (25 MP); `MaxEmbeddedResourceBytes = 8 MiB` for the compressed bytes. |
| JavaScript execution | `<script>` in template | Policy gate rejects `<script>` elements; PDF writer rejects `/JavaScript` action dictionaries. |
| PDF action injection | Template injects `/OpenAction` or `/Launch` into output | `IPdfWriter` impl rejects these in PDF object graph — not just at parse time. |
| Path traversal via @font-face | `@font-face { src: url(file:///etc/fonts/Arial.ttf) }` | `IFontResolver` returns bytes; never receives a path. Engine has no `File.ReadAllBytes()` on CSS `url()` values. |
| Cross-tenant cache poisoning | Caller passes forged `TemplateId` to hit another tenant's cached layout | Cache keys derived from ambient `ITenantContext.TenantId`, never `PdfRenderOptions.TemplateId`. |
| Render timeout / CPU exhaustion | Deeply nested table with colspan/rowspan | `RenderTimeout = 15s` (strict) via `CancellationTokenSource`; layout aborts on token. |

### PDF Hardening Defaults (IPdfWriter)

```
PDF Version:       1.7 (pinned — never negotiate down)
Linearization:     off (deterministic output)
Object IDs:        deterministic (content-hash-seeded, no GUIDs, no DateTime)
Timestamps:        stripped (byte-for-byte reproducibility)
Forbidden actions: /JavaScript, /Launch, /OpenAction, /EmbeddedFile, /URI (outbound)
Encryption:        off by default (Enterprise tier can enable)
```

---

## Scaling Considerations

This is an in-process library, not a service. Scaling is about call concurrency and memory, not network topology.

| Scale | Render concurrency | Architecture adjustment |
|-------|-------------------|------------------------|
| 1–50 RPS | 1–8 parallel renders | Default `PdfService` singleton; `SemaphoreSlim` on CPU-bound layout stage optional |
| 50–200 RPS | 8–32 parallel renders | Add `ObjectPool<LayoutContext>` to reuse per-call allocations; enable `ArrayPool<byte>` in writer |
| 200+ RPS | Horizontal scale-out | Deploy multiple instances; engine is stateless per call. Redis-backed template cache (Enterprise HotReload) replaces per-instance warm-up cost. |

**First bottleneck:** AngleSharp parsing and CSS cascade. These are single-threaded per call but the engine is designed for concurrent calls. Profile with `System.Diagnostics.Metrics` (`pdf.operation` histogram) before adding parallelism inside a call.

**Second bottleneck:** Font subsetting via SixLabors.Fonts. Vietnamese diacritic stacking + subsetting is CPU-heavy for large documents. Cache subsetted font bytes keyed on `(TenantId, fontFamily+weight+style, glyphSet)` — this cache belongs in `IFontResolver` implementations, not in the engine.

**AOT (v0.2):** `PublishAot` on Alpine targets `<40 MB` container. No reflection-emit in the hot path is enforced from v0.1 by design (no `Activator.CreateInstance` on render-critical paths). Source generator fast path eliminates Scriban at runtime entirely.

---

## Anti-Patterns

### Anti-Pattern 1: Passing Tenant Context Through Options

**What people do:**
```csharp
// WRONG — caller threads tenant ID through options
await pdfService.RenderAsync(html, stream, new PdfRenderOptions { TenantId = user.TenantId });
```

**Why it's wrong:** `PdfRenderOptions` has no `TenantId` property by design. If a caller adds one via subclassing or extension, the cache key logic will ignore it — the engine reads `ITenantContext` from DI ambient context. Passing tenant IDs through call parameters opens the path to tenant ID spoofing.

**Do this instead:** Ensure `ITenantContext` is populated before the render call (ASP.NET Core middleware sets it; background jobs set it via `TenantContext.SetCurrent()`).

### Anti-Pattern 2: Using `RenderToBytesAsync` in Hot Paths

**What people do:**
```csharp
// Allocates a MemoryStream, copies to byte[], tosses MemoryStream — all LOH if >85 KB
var (bytes, _) = await pdfService.RenderToBytesAsync(html, options);
await response.Body.WriteAsync(bytes);
```

**Why it's wrong:** For a 200-page invoice (2–5 MB), this materialises the entire document on the LOH, triggering Gen2 GC. At 50 RPS that is 100–250 MB of LOH pressure per second.

**Do this instead:**
```csharp
// Write directly to response body — zero copy
await pdfService.RenderAsync(html, response.Body, options, ct);
```

### Anti-Pattern 3: Registering a Custom Policy That Allows `<script>`

**What people do:** Override `IPdfCssPolicy` to allow JavaScript for "dynamic headers" in templates.

**Why it's wrong:** The PDF writer's `/JavaScript` rejection is a separate layer, but allowing `<script>` in the policy gate signals intent to execute JS during render — the engine has no JS runtime. The result is silently dropped scripts with no diagnostic, not dynamic headers.

**Do this instead:** Pre-substitute all dynamic values server-side before calling `RenderAsync`. Templates are HTML+CSS with values already resolved upstream. This is a deliberate design choice (D2 in PROJECT.md: "templates are HTML+CSS with placeholders pre-substituted upstream").

### Anti-Pattern 4: Inline `Version` in `.csproj`

**What people do:**
```xml
<!-- WRONG — violates CPM (Central Package Management) -->
<PackageReference Include="AngleSharp" Version="1.3.0" />
```

**Why it's wrong:** The repo uses `Directory.Packages.props` for all version declarations. An inline `Version` attribute silently bypasses CPM and can introduce a version conflict that is invisible in the dependency graph.

**Do this instead:** Add to `Directory.Packages.props` only, reference without `Version` in the csproj.

### Anti-Pattern 5: Forking HtmlRenderer.PdfSharp

**What people do:** Fork the archived `HtmlRenderer.PdfSharp` library as a starting point for the layout engine.

**Why it's wrong:** `HtmlRenderer.PdfSharp` has GDI+ dependencies (violates the no-native constraint) and was archived in 2018. Its layout model predates CSS 2.1 box formatting context. Any fork immediately inherits 8 years of unpatched issues and a native dependency that breaks Alpine/AOT.

**Do this instead:** Hand-write the box tree and layout engine from the CSS 2.1 specification. This is the decision recorded in PROJECT.md (D1).

---

## Integration Points

### Internal Ecosystem Services

| Service | Integration | Pattern |
|---------|-------------|---------|
| `IMLog<T>` | `PdfService` uses for structured error/warn/info logging | Constructor-injected; no raw `ILogger<T>` |
| `IMDateTimeService` | Telemetry timestamps | Constructor-injected; no `DateTime.UtcNow` |
| `IMJsonSerializeService` | Template registry serialisation (Enterprise only) | Constructor-injected; no `JsonSerializer` |
| `ITenantContext` | Ambient tenant scoping for cache keys | Resolved from `IServiceProvider` per call |
| `Muonroi.Governance.Policy.PolicyVerifier` | Signs/verifies `DefaultStrictPolicy` configs | `Muonroi.Pdf.Governance` depends on `Muonroi.Governance` |

### Enterprise Integration (v1.0)

| Service | Integration | Pattern |
|---------|-------------|---------|
| PostgreSQL | Template registry (version history, RBAC, audit trail) | `Muonroi.Pdf.Enterprise.Registry` via EF Core |
| Redis | Hot-reload change notifier; tenant-scoped invalidation ≤5s | `Muonroi.Pdf.Enterprise.HotReload` via `IMCacheService` |
| License server | Gates `Enterprise.*` startup | `Muonroi.Pdf.Enterprise.License` via `Muonroi.Governance.Enterprise` |
| Web Designer | Live preview via engine version pin; round-trip <10s P95 | `Muonroi.Pdf.Enterprise.Designer` (Blazor or React) |

### External Package Boundaries

| Package | OSS/Commercial | NuGet audience |
|---------|---------------|----------------|
| `Muonroi.Pdf.Abstractions` | Apache 2.0 | Any .NET project referencing contracts |
| `Muonroi.Pdf` | Apache 2.0 | .NET apps wanting the full engine |
| `Muonroi.Pdf.Governance` | Apache 2.0 | Apps needing `DefaultStrictPolicy` |
| `Muonroi.Pdf.Enterprise.*` | Commercial | Paying enterprise customers |
| `Muonroi.BuildingBlock.All` | Apache 2.0 | Includes `Pdf`, `Abstractions`, `Governance` |

---

## Telemetry Architecture

```
PdfTelemetryDescriptor : ITelemetryDescriptor
    ActivitySource: "Muonroi.BuildingBlock.Pdf"
    Meter:          "Muonroi.BuildingBlock.Pdf"

Activities (spans):
    pdf.render          — full pipeline; tags: pdf.template_id, pdf.page_count, tenant.id
    pdf.parse           — IHtmlParser stage
    pdf.cascade         — ICssCascadeEngine stage
    pdf.policy_gate     — IPdfCssPolicy.ValidateAsync
    pdf.layout          — LayoutEngine
    pdf.write           — IPdfWriter

Metrics (snake_case):
    pdf.operation       — histogram (duration ms); dimensions: tenant.id, pdf.template_id, status
    pdf.page_count      — histogram (pages per render)
    pdf.bytes_written   — histogram (bytes)
    pdf.policy_rejected — counter; dimension: policy.id, rejection.reason
```

All metric names follow the ecosystem-wide snake_case convention established in `OtelSetup.cs`.

---

## Sources

All findings derived from evidence in this repository:

- `src/Muonroi.Pdf.Abstractions/` — actual interface definitions (read 2026-05-26)
- `src/Muonroi.Pdf.Abstractions/Policy/PdfPolicyLimits.cs` — actual security limit values
- `src/Muonroi.Caching.Redis/Redis/RedisExtensions.cs` — DI registration + telemetry pattern reference
- `.planning/PROJECT.md` — authoritative decisions (D1–D20), constraints, requirements
- CSS 2.1 specification (box formatting context, cascade, margin collapse) — well-established standard

---
*Architecture research for: Muonroi.Pdf — pure-managed HTML/CSS-to-PDF renderer*
*Researched: 2026-05-26*
*Confidence: HIGH — grounded in actual source files, not training-data speculation*
