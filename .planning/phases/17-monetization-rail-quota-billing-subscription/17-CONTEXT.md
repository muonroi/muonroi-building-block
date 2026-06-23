# Phase 17: Monetization Rail — Enforced Quota + Usage→Billing + Subscription - Context

**Gathered:** 2026-06-21
**Status:** Ready for planning
**Source:** Autonomous discuss (decisions locked by orchestrator per explicit user grant — "chạy autonomous đến done"). Evidence-backed by two cross-repo audits (control-plane + license-server) run 2026-06-21.

<domain>
## Phase Boundary

Close the **monetization gap** that the PDF Enterprise work (Phase 16) deliberately left open and that BOTH downstream repos explicitly deferred. This is **wiring + a thin billing seam on the EXISTING rails**, not new product infrastructure — exactly the open-core thesis ("a second/third product line rides the shared SaaS rails").

**Verified gap (evidence, 2026-06-21):**
- **control-plane:** zero matches for `Billing/Invoice/Payment/Stripe/Subscription` in source. `QuotaEnforcementMiddleware` (HTTP 429 hard-cap) **already exists in building-block** (`src/Muonroi.AspNetCore/Middleware/QuotaEnforcementMiddleware.cs:22-40`) but is **never registered** in the control-plane host (`UseQuotaEnforcement` = 0 matches in `Program.cs`). `PricingEndpoints.cs` exposes Free/Licensed/Enterprise tiers with **placeholder prices** ("$20" / "Contact us"), not tied to usage or subscription.
- **building-block:** PDF metering is **record-only** by design (`EnterprisePdfServiceWrapper.cs:13-14`, "Never blocks … record-only D-02"); `TenantQuota.cs:94-96` `MaxPdfRendersPerDay = int.MaxValue` "(record-only; no hard cap in Phase 16)".
- **license-server:** pure RSA issue/revoke; `09.4-ws-d-license-pdf/PLAN.md:34` defers ***"Billing / metering ARR for PDF tier (separate finance integration)"***; tiers map to feature arrays with **no price**, expiry+revoke+grace exist but **no renewal/subscription** (manual re-issue only).

**Two stubs are already in place** → this phase is mostly *connection*, not greenfield: (1) the hard-cap middleware exists, just unregistered; (2) `PricingEndpoints` already frames tiers.

**Hard boundary (SC5, inviolable):** dependency stays one-way `Enterprise/billing → OSS`. `Muonroi.Pdf` (OSS, Apache-2.0) gets ZERO changes and references nothing under billing/enterprise. Enforcement lives at the **control-plane API boundary / enterprise layer**, never inside `IMPdfService.RenderAsync`.
</domain>

<decisions>
## Implementation Decisions

### Quota enforcement boundary
- **D-01:** **Enforce at the control-plane API boundary**, not in the engine. Register the existing `QuotaEnforcementMiddleware` via `UseQuotaEnforcement()` in the control-plane host; a tenant over its tier limit gets **HTTP 429**. The OSS `IMPdfService.RenderAsync` path stays **NEVER blocked** (SC5). PDF render metering remains record-only inside the engine wrapper; the *enforced* cap is a separate API-layer concern. Per-tier limits are sourced from the **licensed tier** (license-server mapping), replacing hard-coded `int.MaxValue`.

### Billing provider seam
- **D-02:** **Seam + record-only default.** Introduce `IBillingProvider` in a new `Muonroi.Billing.Abstractions` (no payment-SDK dependency). Ship a **record-only** default impl that logs/records billable events and **never calls an external service**. The real payment-processor (Stripe) adapter lives **behind the seam and is DEFERRED** — it must NOT be a build/test dependency this phase. Provider failures are logged with module/operation/context (No Silent Catch), never swallowed.

### Usage aggregation + pricing
- **D-03:** **Period rollup → priced line items → invoice-preview (compute-only).** `IUsageAggregator` rolls per-tenant metered usage (from `ITenantQuotaStore`) into `UsageLineItem`s for a billing period using a `PricingPlan`. control-plane exposes an **invoice-preview** endpoint that returns the computed amount — **no charge is executed** (that's the deferred Stripe adapter). This replaces `PricingEndpoints` placeholder prices with a real `PricingPlan` model.

### Subscription / renewal lifecycle
- **D-04:** **Add subscription + renewal to license-server.** Add a subscription record + a **renew endpoint** + expiry/grace handling so renewal is no longer manual re-issue only. Expose a **tier→quota-limit mapping** consumed by control-plane (D-01) and building-block (D-02/03). Reuse the existing RSA ActivationProof issue/revoke machinery — do not fork crypto.

### Pricing math scope
- **D-05:** **Simple per-unit × tier rate.** Pricing = Σ(metered quantity per dimension × tier unit-rate) + optional flat tier base. **No** proration, tax, multi-currency, or dunning this phase (deferred). Deterministic, unit-testable arithmetic only.

### Claude's Discretion
- Exact package layout (`Muonroi.Billing.Abstractions` + `Muonroi.Billing` default impl, vs extending `Muonroi.Quota`); DI registration shape; where `IUsageAggregator` physically runs (control-plane service vs building-block library consumed by control-plane); endpoint routes/verbs; `PricingPlan` storage (config vs DB) — pick the lowest-friction option consistent with existing `Muonroi.Quota`/`PricingEndpoints` patterns.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase boundary + prior state (don't rebuild)
- `.planning/ROADMAP.md` §"Phase 17: Monetization Rail" — goal, WS-A/B/D scope, SC.
- `.planning/phases/16-pdf-enterprise-governance-controlplane-integration/16-CONTEXT.md` — D-02 record-only metering decision this phase builds on; SC5 boundary statement.

### building-block — the seam + the unregistered middleware (WS-A)
- `src/Muonroi.AspNetCore/Middleware/QuotaEnforcementMiddleware.cs` — the EXISTING hard-cap 429 middleware (lines ~22-40: `CheckQuotaAsync` → 429). D-01 registers it downstream; do not rewrite it.
- `src/Muonroi.Quota.Abstractions/QuotaType.cs` + `TenantQuota.cs` (`MaxPdfRendersPerDay`, `GetLimit`) + `InMemoryTenantQuotaTracker.cs` + `ITenantQuotaStore` — the metering/limit seam D-03/D-04 read; `int.MaxValue` default to replace with tier-sourced limit.
- `src/Muonroi.Quota.Abstractions/QuotaExceededException.cs` — 429 exception already defined.
- `src/Muonroi.Pdf.Enterprise/Metering/EnterprisePdfServiceWrapper.cs` — record-only metering pattern (No Silent Catch); the model the billing record-only provider mirrors.

### control-plane — registration + pricing surface (WS-B)
- `muonroi-control-plane/src/Host/Muonroi.ControlPlane.Host/Program.cs` — host pipeline; add `UseQuotaEnforcement()` (D-01) + register `IBillingProvider`/`IUsageAggregator`.
- `muonroi-control-plane/.../Endpoints/PricingEndpoints.cs` — placeholder Free/Licensed/Enterprise tiers + prices to replace with `PricingPlan` (D-03).
- `muonroi-control-plane/.../Services/Compliance/PdfAuditControlPlaneStore.cs` — Phase-16 adapter pattern (sync bridge, ILogger, AddSingleton) to mirror for billing services.

### license-server — subscription/renewal (WS-D)
- `muonroi-license-server/src/Storage/Entities/LicenseRecord.cs` (`ExpiresAt`, `IsRevoked`, `MaxActivations`, `AllowedFeatures`) + `LicenseServerDbContext.cs` — extend with subscription/renewal.
- `muonroi-license-server/src/Services/ActivationProofService.cs` + `RevocationService.cs` + `HeartbeatService.cs` (24h `RevocationGraceHours`) — lifecycle machinery to reuse.
- `muonroi-license-server/src/Endpoints/KeyEndpoints.cs` — issue/revoke/features endpoints; add `renew` alongside (D-04). `KnownPdfCapabilities.cs` — tier/feature source.

### Memory
- `gsd_cross_repo_execute_phase` — MANDATORY: how to execute a phase spanning building-block + control-plane + license-server (no worktrees, sequential, per-repo `git -C` commits + per-repo test gate).
- `gsd_plan_phase_gates`, `phase16_plan03_pdf_audit_controlplane_store` (control-plane DI/sync-bridge gotchas), `alpha15_template_dep_gotchas`.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets (mostly connection, not greenfield)
- **`QuotaEnforcementMiddleware`** — hard-cap 429 ALREADY built in building-block; D-01 = register + configure per-tier limits.
- **`Muonroi.Quota.Abstractions`** — `QuotaType`, `TenantQuota`, `ITenantQuotaStore`, `QuotaExceededException` already exist; D-03/D-04 read/extend.
- **`PricingEndpoints`** — tier framing already present; D-03 replaces placeholder prices with `PricingPlan`.
- **license-server lifecycle** — issue/revoke/expiry/grace exist; D-04 adds the missing subscription/renewal layer on top.

### Established Patterns
- **Open-core SC5** — one-way dependency; enforcement at API/enterprise layer, never OSS engine.
- **Record-only default + No Silent Catch** — mirror `EnterprisePdfServiceWrapper` for the billing provider.
- **control-plane adapter shape** — `AddSingleton`, `ILogger`, sync-bridge over async stores (per `PdfAuditControlPlaneStore`).
- **Capability/tier convention** — Free/Licensed/Enterprise feature arrays in license-server; extend to carry quota limits, not just feature flags.

### Integration Points
- control-plane `Program.cs`: `UseQuotaEnforcement()` + DI for billing/aggregator.
- building-block: new `Muonroi.Billing.Abstractions` (+ default impl); `TenantQuota` limit sourced from tier mapping.
- license-server: subscription entity + `renew` endpoint + tier→limit map endpoint/claim.
</code_context>

<specifics>
## Specific Ideas

- Thesis: **the billing rail is shared infra** — PDF is just the first product line to bill; rule-engine/storyflow ride the same `IBillingProvider`/`IUsageAggregator`/`PricingPlan`. Keep the seam product-agnostic (keyed by `QuotaType`/metered dimension, not "pdf").
- Keep **enforcement (429)** and **metering (record-only)** as distinct concerns: render path meters silently; API boundary enforces. This preserves SC5 while still capping abuse.
- invoice-preview is **compute-only** this phase — the seam is ready for a Stripe adapter later without re-plumbing.
</specifics>

<deferred>
## Deferred Ideas

- **Live payment-processor (Stripe) adapter** — behind `IBillingProvider`; no external dependency at build/test time this phase.
- **Proration, tax, multi-currency, dunning/retries** — out of D-05's simple per-unit pricing.
- **Render-time billing events** (every render → billing) — high volume; aggregate from existing quota metering instead.
- **Self-service checkout / customer portal UI** — frontend track, not this backend-focused phase.
- **ARR/MRR analytics dashboards** — reporting layer, later.
</deferred>

---

*Phase: 17-monetization-rail-quota-billing-subscription*
*Context gathered: 2026-06-21 (autonomous)*
