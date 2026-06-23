# Phase 16: PDF Enterprise ↔ Governance/ControlPlane Integration - Context

**Gathered:** 2026-06-20
**Status:** Ready for planning

<domain>
## Phase Boundary

Turn the **thin v1.0 Enterprise stubs (Phase 9) into real enforcement** by wiring
`Muonroi.Pdf.Enterprise` onto the shared Muonroi enterprise rails — instead of building any
PDF-specific infrastructure. Concretely: a real license gate bound to the governance
`ActivationProof`, per-tenant render metering via `Muonroi.Quota`, PDF publish/version events into
the `Compliance` evidence pack, and control-plane auto-rollback for the canary.

**This is gap-closure on Phase 9, NOT a rebuild.** Phase 9 already shipped (see
`PHASE-09-CLOSEOUT.md`): the template registry (11 REST endpoints + maker-checker), SignalR
hot-reload, 6 control-plane audit events, `SsimScorer` + canary score endpoint, the PDF Designer
package, and license-server `KnownPdfCapabilities` + grant/revoke. Phase 16 does NOT re-implement
any of these — it binds the building-block side to them and closes the enforcement gaps.

**Hard boundary (SC5, inviolable):** the dependency is one-way `Enterprise → OSS`. `Muonroi.Pdf`
(OSS, Apache-2.0) gets ZERO changes and references nothing under `*.Enterprise`. All work lands in
`Muonroi.Pdf.Enterprise` (+ `Muonroi.Governance.Enterprise` reuse), control-plane, and license-server.
</domain>

<decisions>
## Implementation Decisions

### License enforcement policy
- **D-01:** **Fail-closed.** Replace the no-op `AlwaysAllowFeatureGate` with a real `IFeatureGate`
  bound to the governance `ActivationProof` + `MEnterpriseFailClosedMatrix`. For an enterprise
  capability (`pdf.designer` / `pdf.registry` / `pdf.canary`) that is not licensed,
  `EnsureFeatureOrThrow` throws `FeatureNotLicensedException`. **Open-core boundary is explicit:** a
  plain OSS render via `IMPdfService.RenderAsync` (no enterprise feature) is **NEVER** gated — only
  the registry/designer/canary add-ons are. The OSS engine stays free and untouched.

### Render metering (Quota)
- **D-02:** **Record-only, per-tenant / per-render.** Meter PDF render usage through `Muonroi.Quota`
  for billing/analytics; it **never blocks a production render** (no hard cap in this phase).
  Granularity: one metered event per render, tagged with tenant id, carrying page count as a metered
  dimension. Hard-quota enforcement (blocking) is deferred (see Deferred Ideas).

### Compliance / audit pack
- **D-03:** **Publish/version events only.** Feed the existing 6 control-plane audit events
  (`pdf.template.{created,updated,submitted,approved,rejected,activated}`) into the `Compliance`
  evidence pack. **Render-time audit is deferred** (high volume; needs its own retention policy).

### Canary auto-rollback
- **D-04:** **Automate at the control-plane policy layer (WS-B).** When a canary's SSIM score is
  below the configured threshold, control-plane triggers automatic rollback before 100% traffic —
  closing Phase 9 SC2 (currently PARTIAL). The engine stays pure: it only *scores* via the existing
  `SsimScorer`; no rollback/operational logic is pulled into the engine or OSS.

### Claude's Discretion
- Exact DI seam for providing the `ActivationProof` to `Pdf.Enterprise` (host-supplied via
  `EnterpriseGovernanceServiceExtensions`), the precise `IFeatureGate` implementation class, where the
  metering hook physically sits (an Enterprise service wrapper around `IMPdfService` vs the
  control-plane render path), and the `Muonroi.Quota` abstraction method shape.
- Control-plane rollback policy mechanics (threshold config source, traffic-shift steps).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase boundary + prior-phase state (don't rebuild)
- `.planning/ROADMAP.md` §"Phase 16: PDF Enterprise ↔ Governance/ControlPlane Integration" — goal, WS-A..D scope, SC.
- `.planning/PHASE-09-CLOSEOUT.md` — EXACT list of what Phase 9 shipped per repo + the SC1–SC5 status table (SC2 canary = PARTIAL, hot-reload = INFRA-READY). The "don't re-implement" inventory.

### Building-block — the stubs to upgrade (WS-A)
- `src/Muonroi.Pdf.Enterprise/Muonroi.Pdf.Enterprise.csproj` — currently references ONLY `Muonroi.Pdf`; must add a reference to `Muonroi.Governance.Enterprise` (Enterprise→Enterprise is allowed; never let OSS reference it).
- `src/Muonroi.Pdf.Enterprise/IFeatureGate.cs` + `AlwaysAllowFeatureGate.cs` + `FeatureNotLicensedException.cs` + `CapabilityKeys.cs` — the gate contract + no-op to replace + the `pdf.*` keys.

### Governance/license rails to reuse (the real machinery)
- `src/Muonroi.Governance.Enterprise/License/LicenseActivator.cs` + `MEnterpriseFailClosedMatrix.cs` + `LicenseHeartbeatService.cs` + `MEnterpriseSecurityProfile.cs` — ActivationProof verification + fail-closed matrix the real `IFeatureGate` binds to.
- `src/Muonroi.Governance.Enterprise/EnterpriseGovernanceServiceExtensions.cs` — DI registration pattern to extend for the PDF gate.
- `src/Muonroi.Governance.Enterprise/Compliance/MComplianceEvidencePackService.cs` + `IMComplianceEvidencePackService.cs` + `MComplianceContracts.cs` — evidence-pack seam for D-03.
- `src/Muonroi.Quota.Abstractions/` — the metering seam for D-02.

### Control-plane + license-server (Phase 9 deliverables to extend)
- control-plane: `/api/v1/control-plane/pdf-templates/*` (11 endpoints), `PdfTemplateRegistryService`, `SignalRPdfTemplateChangeNotifier`, the 6 `pdf.template.*` audit events, `POST /api/canary/pdf/score` — D-03 source events + D-04 rollback host.
- license-server: `KnownPdfCapabilities` + PDF grant/revoke (WS-D, shipped 9.4) — the entitlement source the gate verifies against.

### Memory
- `pdf_phase15_radial_affine`, `gsd_plan_phase_gates` (workflow gotchas).
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Muonroi.Governance.Enterprise.License`** — full ActivationProof/anti-tamper/heartbeat/fail-closed stack already exists; the PDF gate is a thin binding, not new crypto.
- **`Muonroi.Quota.Abstractions`** — metering seam; D-02 records through it.
- **`Compliance` evidence pack service** — D-03 routes the existing control-plane audit events into it.
- **`SsimScorer` + control-plane `/api/canary/pdf/score`** (Phase 9) — D-04 reuses the score; only adds rollback policy at control-plane.
- **`SignalRPdfTemplateChangeNotifier` / `RuleSetChangeHub`** — hot-reload already wired; no change needed.

### Established Patterns
- **Capability key convention** `<domain>.<feature>` (`pdf.designer/registry/canary`) — mirror `core.runtime`, `auth.rbac_plus`.
- **One-way Enterprise→OSS (SC5)** — the architectural invariant; all binding happens in `*.Enterprise`.
- **Fail-closed matrix** (`MEnterpriseFailClosedMatrix`) is the canonical "what happens when unlicensed" pattern — D-01 plugs `pdf.*` into it rather than inventing behavior.

### Integration Points
- `Pdf.Enterprise.csproj` → add `ProjectReference` to `Muonroi.Governance.Enterprise`.
- New `IFeatureGate` impl (replacing `AlwaysAllowFeatureGate`) bound to ActivationProof, registered via the governance DI extensions.
- Metering hook in an Enterprise-side wrapper around `IMPdfService` (or control-plane render path) — NOT in the OSS engine.
- Compliance + rollback extensions live in control-plane (WS-B), consuming events/scores already emitted.
</code_context>

<specifics>
## Specific Ideas

- The whole thesis: **"second product line riding the EXISTING open-core SaaS rails"** (ROADMAP Phase 9 framing) — Phase 16 deepens that, it does not fork new infra.
- Keep `pdf.*` capabilities as first-class entries in the SAME ActivationProof a tenant already holds for rule-engine/governance — one license, one anti-tamper, one heartbeat.
</specifics>

<deferred>
## Deferred Ideas

- **Render-time compliance audit** (every render → evidence pack) — high volume; needs retention policy. Deferred from D-03.
- **Hard quota enforcement** (block render over limit) — D-02 is record-only for now; enforcement is a later monetization phase.
- **Designer P95 / hot-reload production load-test** (SC1/SC3 measurement) — operational validation, not engineering scope.
- **Cross-service TCIS cutover follow-ups 9.5b–e** (`download`/`eeir`/`fullcontainerdelivery`/`common`) — separate from this phase (per PHASE-09-CLOSEOUT).
- **Flexbox / rendering-engine work** — separate rendering track.

</deferred>

---

*Phase: 16-pdf-enterprise-governance-controlplane-integration*
*Context gathered: 2026-06-20*
