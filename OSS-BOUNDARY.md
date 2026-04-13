# OSS / Commercial Package Boundary

## Rule
- OSS packages MUST NOT depend on Commercial packages.
- Verified by: `scripts/check-modular-boundaries.ps1`

## Design Intent - Why Enterprise Source Is In This Public Repo

This repository uses a BUSL-style (Business Source License) open-core model:
source code is publicly visible but usage of Commercial packages requires a
valid commercial license. This is intentional, not an oversight.

Why source is public:
- Transparency builds trust: customers can audit security and data-handling code before purchasing.
- Forkability is a feature: OSS components (Core, RuleEngine, Tenancy) are freely forkable under Apache 2.0.
- The real monetization moat is the SaaS Control Plane (private repos: `muonroi-control-plane`, `muonroi-license-server`), not the package source code.

What protects Commercial revenue:
1. RSA-signed ActivationProof - tier and capabilities are server-signed; you cannot forge them without the License Server private key.
2. Assembly hash verification - `CodeIntegrityVerifier` checks that all Muonroi.* assemblies in the process match the hashes approved at activation time. A fork that removes `EnsureFeatureOrThrow()` calls will fail integrity verification.
3. Heartbeat + grace period - revoked licenses degrade to Free tier after the configured grace period (24h Enterprise / 72h Licensed).
4. SaaS lock-in - hot-reload, approval workflows, canary rollout, and audit trail all require the private `muonroi-control-plane` service. These cannot be replicated by forking OSS packages.

What to do if you find a legitimate fork that bypasses commercial features:
Report it at https://github.com/muonroi/muonroi-building-block/security/advisories/new.
Per our Commercial License Agreement, production use without a valid license key is
a license violation regardless of source visibility.

---

## OSS Packages (Apache 2.0 - public NuGet)
- Muonroi.Core.Abstractions
- Muonroi.Core
- Muonroi.Governance.Abstractions        (Phase 0.2 creates this)
- Muonroi.Governance
- Muonroi.Tenancy.Abstractions
- Muonroi.Tenancy.Core
- Muonroi.Tenancy
- Muonroi.RuleEngine.Abstractions
- Muonroi.RuleEngine.Core
- Muonroi.RuleEngine.SourceGenerators
- Muonroi.RuleEngine.Testing
- Muonroi.RuleEngine.DecisionTable
- Muonroi.RuleEngine.NRules
- Muonroi.RuleEngine.CEP
- Muonroi.Data.Abstractions
- Muonroi.Data.Dapper
- Muonroi.Data.EntityFrameworkCore
- Muonroi.Caching.Abstractions
- Muonroi.Caching.Memory
- Muonroi.Auth
- Muonroi.AspNetCore
- Muonroi.AspNetCore.OpenApi
- Muonroi.Http
- Muonroi.Resilience
- Muonroi.Mapper
- Muonroi.Mediator
- Muonroi.Messaging.Abstractions
- Muonroi.Observability
- Muonroi.BackgroundJobs.Abstractions
- Muonroi.BuildingBlock.Shared
- Muonroi.Logging
- Muonroi.Logging.Abstractions

## Commercial Packages (Muonroi Commercial License - private feed)
- Muonroi.Governance.Enterprise           (Phase 0.2 creates this)
- Muonroi.AuthZ
- Muonroi.Caching.Redis
- Muonroi.Messaging.MassTransit
- Muonroi.BackgroundJobs.Hangfire
- Muonroi.BackgroundJobs.Quartz
- Muonroi.SignalR
- Muonroi.Grpc
- Muonroi.Secrets
- Muonroi.Bff
- Muonroi.ServiceDiscovery.Consul
- Muonroi.RuleEngine.Runtime.Web
- Muonroi.RuleEngine.DecisionTable.Web
- Muonroi.UiEngine.Catalog
