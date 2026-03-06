# Commercial Editions Guide

## 1. Positioning

Muonroi.BuildingBlock follows an OSS + Commercial model:

- OSS core: published under Apache License 2.0 in this repository.
- Commercial editions: paid license governance and enterprise security operations on top of OSS runtime.

This means teams can use the free core immediately, then upgrade only when governance/compliance/risk controls become mandatory.

## 2. What Customers Actually Pay For

Customers are not paying for basic CRUD/auth scaffolding. They pay for:

- Feature-gated premium modules at runtime.
- Signed policy workflow (policy verification, anti-downgrade controls).
- Server validation and audit chain submission with nonce rotation.
- Control-plane operations API (issue/revoke license, tenant assignment, policy bundle lifecycle).
- Stronger anti-tampering and production enforcement posture.
- Commercial lifecycle: license issuance/revocation, SLA process, enterprise onboarding.

## 3. Edition Matrix

| Area | Free | Licensed | Enterprise |
|------|------|----------|------------|
| Core API/Auth/CRUD | Yes | Yes | Yes |
| Premium modules (`multi-tenant`, `advanced-auth`, `rule-engine`, `grpc`, `message-bus`, `distributed-cache`, `audit-trail`, `anti-tampering`) | No | Yes (licensed scope) | Yes |
| Tier 2 signed policy model | No | Optional | Recommended/Default |
| Tier 3 server validation + remote audit | No | Optional | Recommended/Default |
| Future premium capabilities | No | Per-contract | Yes (`AllowedFeatures=["*"]`) |
| Intended use case | Dev/internal/light workload | SaaS/product teams | Regulated/high-risk enterprises |

## 4. Capability Guidance

`AllowedFeatures` now supports both:

- Legacy feature keys (`advanced-auth`, `message-bus`, ...).
- Capability keys (`auth.rbac_plus`, `transport.message_bus`, ...).

Runtime guards resolve both formats through a capability resolver. For paid tiers, core runtime actions
(`api.*`, `db.*`, `http.*`) are mapped to `core.runtime` automatically, so no manual action-key list is required.

Recommended practice:

- Do not hand-edit `license.json` for production.
- Generate capability profiles from a control-plane service to avoid entitlement drift.

## 5. Recommended Packaging

- Free: zero-friction developer experience, no hard security constraints.
- Licensed: module unlock + optional policy controls for growing product teams.
- Enterprise: secure-by-default profile (signed policy required, anti-tampering + server validation enabled) and formal support process.

## 6. Practical Sales Narrative

Use this message consistently:

- Free gives fast adoption and baseline capability.
- Paid editions reduce operational and compliance risk, not developer freedom.
- Enterprise is for organizations that need auditable controls, provable policy enforcement, and centralized governance.
