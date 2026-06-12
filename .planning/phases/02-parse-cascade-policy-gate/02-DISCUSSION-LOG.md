# Phase 2 Discussion Log: Parse + Cascade + Policy Gate

**Date**: 2026-05-26
**Mode**: Headless autonomous — no interactive discussion

---

## Areas Reviewed

### Area 1: Package layout

**Question**: Where do `AngleSharpHtmlParser`, `AngleSharpCascadeEngine`, and `DefaultStrictPolicy` implementations live?

**Options considered**:
- A) `Muonroi.Pdf.Governance` — co-locate parsing, cascade, and policy as one governance concern
- B) `Muonroi.Pdf` — put adapters in the main implementation package, policy in Governance

**Selection**: A — `Muonroi.Pdf.Governance`

**Notes**: ROADMAP Phase 2 goal explicitly names `Muonroi.Pdf.Governance`. `Muonroi.Pdf` stays empty until Phase 6 DI wiring.

---

### Area 2: DOM inspection for DefaultStrictPolicy

**Question**: How does `DefaultStrictPolicy` inspect individual CSS property values without polluting the `IPdfDocumentContext` public contract?

**Options considered**:
- A) Extend `IPdfDocumentContext` with CSS inspection methods (changes Abstractions public contract)
- B) Internal cast to `AngleSharpStyledDocument` within Governance assembly
- C) Separate `IStyleInspector` interface passed alongside `IPdfDocumentContext`

**Selection**: B — internal cast (same-assembly coupling is intentional and contained)

**Notes**: Option A pollutes the public contract with AngleSharp-specific semantics. Option C adds interface proliferation. Option B is the correct pragmatic choice for Phase 2; a formal `IStyleInspector` can be extracted later.

---

### Area 3: PolicyViolation structured fields

**Question**: GOV-02 requires `property name, rejected value, CSS selector, suggested alternative` in each violation. Current `PolicyViolation` only has `RuleId` + `Message`.

**Options considered**:
- A) Encode structured info in `Message` string (parsing-hostile)
- B) Extend `PolicyViolation` record with nullable optional fields (additive, non-breaking)
- C) Create a `CssPolicyViolation` subtype in Governance

**Selection**: B — additive extension in Abstractions

**Notes**: Option A makes programmatic consumption impossible. Option C creates a parallel type hierarchy. Option B is non-breaking and provides structured access to all callers.

---

### Area 4: Exception types gap

**Question**: PIPE-01/PIPE-02 require typed exceptions. Phase 1 didn't define any. Where should they live?

**Options considered**:
- A) Define exception types in `Muonroi.Pdf.Abstractions` (all callers can catch)
- B) Define in `Muonroi.Pdf.Governance` (only Governance-aware callers can catch)

**Selection**: A — exceptions in Abstractions

**Notes**: Callers of `IMPdfService.RenderAsync` need to catch `PdfInputLimitException` and `PdfPolicyException` without taking a Governance assembly reference. Abstractions is the correct layer.

---

### Area 5: GOV-03 signed policy implementation

**Question**: How to implement policy config signing without tight-coupling policy classes?

**Options considered**:
- A) Build signing verification into `DefaultStrictPolicy` itself
- B) `SignedPdfCssPolicyDecorator` that wraps any `IPdfCssPolicy`
- C) Verify signature at DI registration time only

**Selection**: B — decorator pattern

**Notes**: Option A makes signing a concern of every policy implementation. Option C doesn't protect runtime policy swaps. Option B is the canonical decorator pattern: `DefaultStrictPolicy` stays signing-unaware; Phase 6 DI applies the decorator when `PdfConfigs.RequirePolicySignature = true`.

---

## Deferred Ideas

- Formal `IStyleInspector` interface to replace the internal cast — deferred to post-Phase 2 if needed
- `IAngleSharpDocumentContext` internal interface as formalized seam — deferred; internal cast is sufficient for Phase 2

## Claude Discretion Items

- Added `PdfConfigs.RequirePolicySignature : bool = false` to Abstractions (additive, not in original Phase 1 scope) to support GOV-03
- Added exception types to Abstractions (gap from Phase 1) to ensure callers can catch structured errors without implementation assembly references
- Staged `Muonroi.Pdf.Governance` csproj creation as Phase 2 step 1 (no existing csproj found)
