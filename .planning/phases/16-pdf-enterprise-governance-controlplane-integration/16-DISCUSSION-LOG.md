# Phase 16: PDF Enterprise ↔ Governance/ControlPlane Integration - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-20
**Phase:** 16-pdf-enterprise-governance-controlplane-integration
**Areas discussed:** License enforcement policy, Render metering (Quota), Compliance/audit pack, Canary auto-rollback

**Framing established before discussion:** `PHASE-09-CLOSEOUT.md` shows Phase 9 already shipped the
registry, hot-reload, designer, control-plane audit events, SSIM scorer, and license-server
entitlements. Phase 16 = gap-closure (wire + enforce), not rebuild. `IFeatureGate` is still the no-op
`AlwaysAllowFeatureGate`; `Pdf.Enterprise.csproj` does not yet reference `Muonroi.Governance.Enterprise`.

---

## License enforcement policy

| Option | Description | Selected |
|--------|-------------|----------|
| Fail-closed | Throw `FeatureNotLicensedException` for unlicensed `pdf.*`; OSS render unaffected; uses `MEnterpriseFailClosedMatrix` | ✓ |
| Degrade (watermark) | Render enterprise output with an 'UNLICENSED' watermark | |
| Warn-only | Log + allow full run (grace period) | |

**User's choice:** Fail-closed
**Notes:** OSS `IMPdfService.RenderAsync` is never gated (open-core boundary); only registry/designer/
canary add-ons gate. Replaces the no-op `AlwaysAllowFeatureGate` with a real ActivationProof-bound gate.

---

## Render metering (Quota)

| Option | Description | Selected |
|--------|-------------|----------|
| Record-only, per-tenant/per-render | Meter for billing via `Muonroi.Quota`; never blocks production renders | ✓ |
| Enforce hard quota | Block render when tenant exceeds limit | |
| Record + soft-cap warn | Record + warn near/over limit, no block | |

**User's choice:** Record-only, per-tenant/per-render
**Notes:** Page count carried as a metered dimension. Hard enforcement deferred to a later monetization phase.

---

## Compliance / audit pack

| Option | Description | Selected |
|--------|-------------|----------|
| Publish/version events only | Route the existing 6 `pdf.template.*` control-plane audit events into the evidence pack | ✓ |
| Add render-time audit | Log every render into the evidence pack | |
| Defer compliance pack | Push Compliance integration to a later phase | |

**User's choice:** Publish/version events only
**Notes:** Render-time audit is high-volume → deferred (needs retention policy).

---

## Canary auto-rollback

| Option | Description | Selected |
|--------|-------------|----------|
| Control-plane policy automation | Auto-rollback at control-plane when SSIM < threshold (WS-B); engine stays pure | ✓ |
| Defer to ops | Out of scope Phase 16 | |
| Engine-side hook | Engine self-scores + self-selects version | |

**User's choice:** Control-plane policy automation
**Notes:** Closes Phase 9 SC2 (PARTIAL). Engine only scores via existing `SsimScorer`; no operational logic in engine.

---

## Claude's Discretion

- DI seam for supplying `ActivationProof` to `Pdf.Enterprise`; concrete `IFeatureGate` impl class.
- Physical location of the metering hook (Enterprise wrapper around `IMPdfService` vs control-plane render path).
- `Muonroi.Quota` abstraction method shape; control-plane rollback policy mechanics.

## Deferred Ideas

- Render-time compliance audit; hard quota enforcement; designer P95 / hot-reload load-test (ops);
  TCIS cutover follow-ups 9.5b–e; flexbox / rendering-engine work.
