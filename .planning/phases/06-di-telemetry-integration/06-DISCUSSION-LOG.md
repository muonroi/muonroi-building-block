# Phase 6 Discussion Log: DI + Telemetry + Integration

**Date**: 2026-05-27
**Mode**: Headless autonomous — all decisions made by Claude without interactive discussion
**Phase**: 6 of 9

---

## Gray Areas Identified

### 1. MPdfService pipeline orchestration
- **Question**: Should internal pipeline stages (LayoutEngine, FontPipeline, ImagePipeline) be DI-registered services or instantiated directly inside MPdfService?
- **Options**: (A) Register all stages in DI; (B) Constructor-inject adapters, instantiate internals directly
- **Decision**: Option B — adapters injected, internal stages created in MPdfService constructor
- **Reason**: Internal stages are implementation details with no caller-replaceable interface; DI-04 lists only the four adapter interfaces

### 2. Render pipeline order for RenderMultiPageAsync
- **Question**: How to merge multi-fragment HTML into a single PDF?
- **Options**: (A) Native PdfSharpCore merge; (B) Render each to MemoryStream then concat
- **Decision**: Defer to Phase 6 plan execution — verify PdfSharpCoreWriter capabilities from Phase 5; default to MemoryStream concat
- **Reason**: Phase 5 implementation unknown at context capture time

### 3. PdfTelemetryDescriptor location
- **Question**: Should the descriptor live in Abstractions or the implementation package?
- **Options**: (A) Muonroi.Pdf; (B) Muonroi.Pdf.Abstractions
- **Decision**: Abstractions — matching DistributedCacheTelemetryDescriptor precedent
- **Reason**: Telemetry descriptors are discovery tokens, not implementation details

### 4. Tenant ID for telemetry without HTTP context
- **Question**: How to safely access ITenantContext from a singleton MPdfService?
- **Options**: (A) Inject IHttpContextAccessor; (B) Inject IServiceProvider and GetService lazily
- **Decision**: IServiceProvider + GetService<ITenantContext>() with "unknown" fallback
- **Reason**: Direct scoped injection into singleton violates DI scope rules; IServiceProvider is singleton-safe

### 5. PdfConfigs startup validation approach
- **Question**: IOptions Validate() or DataAnnotations on PdfConfigs?
- **Options**: (A) DataAnnotations + ValidateDataAnnotations(); (B) Validate lambda + ValidateOnStart()
- **Decision**: Validate lambda + ValidateOnStart()
- **Reason**: No DataAnnotation attributes needed on the class; lambda is explicit and readable

---

## Deferred Ideas

- IFontResolver default (system font fallback) — post-v0.1
- IMPdfRendererFactory default registration — Phase 8
- Scoped per-tenant IPdfCssPolicy override — Phase 9

---

*Log generated: 2026-05-27 — headless autonomous mode*
