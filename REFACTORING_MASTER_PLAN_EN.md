# Muonroi BuildingBlock Modularization Master Plan (English)

## 1. Executive Goal

Refactor `Muonroi.BuildingBlock` from a monolithic "god project" into a modular, independently versioned package ecosystem with:

1. clear separation of concerns,
2. strict dependency direction,
3. migration-safe backward compatibility,
4. naming/branding normalization (remove uncontrolled `M*` prefix usage).

## 2. Current Baseline (Locked)

Baseline metrics (captured from current repository state):

1. `src/Muonroi.BuildingBlock`: 401 production `.cs` files (excluding `obj`).
2. Direct package references: 150.
3. Resolved libraries: 239.
4. Critical coupling:
   - `Muonroi.Tenancy` -> `Muonroi.BuildingBlock`
   - `Muonroi.RuleEngine.DecisionTable.Web` -> `Muonroi.BuildingBlock`

This baseline is the reference for phase-by-phase KPI tracking.

## 3. Target Architecture

## 3.1 Foundation

1. `Muonroi.Core`
2. `Muonroi.Core.Abstractions`

## 3.2 Tenancy (explicit abstraction-first)

1. `Muonroi.Tenancy.Abstractions`
2. `Muonroi.Tenancy.Core`
3. `Muonroi.Tenancy` (host integration package)

## 3.3 Auth and Access

1. `Muonroi.Auth`
2. `Muonroi.AuthZ`

## 3.4 Data

1. `Muonroi.Data.Abstractions`
2. `Muonroi.Data.EntityFrameworkCore`
3. `Muonroi.Data.Dapper`

## 3.5 Caching

1. `Muonroi.Caching.Abstractions`
2. `Muonroi.Caching.Memory`
3. `Muonroi.Caching.Redis`

## 3.6 Messaging and App Flow

1. `Muonroi.Messaging.Abstractions`
2. `Muonroi.Messaging.MassTransit`
3. `Muonroi.Mediator`

## 3.7 Communication

1. `Muonroi.Grpc`
2. `Muonroi.SignalR`
3. `Muonroi.Bff` (already separated, keep and rewire dependencies)

## 3.8 Web

1. `Muonroi.AspNetCore`
2. `Muonroi.AspNetCore.OpenApi`

## 3.9 Infrastructure

1. `Muonroi.Observability`
2. `Muonroi.BackgroundJobs.Abstractions`
3. `Muonroi.BackgroundJobs.Hangfire`
4. `Muonroi.BackgroundJobs.Quartz`
5. `Muonroi.ServiceDiscovery.Consul`
6. `Muonroi.Kubernetes`
7. `Muonroi.Resilience`

## 3.10 Governance and Rules Runtime

1. `Muonroi.Governance` (License, Policy, ControlPlane, Compliance, Operations, ServerValidation)
2. `Muonroi.RuleEngine.Runtime` (runtime rule orchestration currently in `Shared/Rules`)

## 3.11 Compatibility

1. `Muonroi.BuildingBlock.All` metapackage
2. `Muonroi.BuildingBlock` compatibility facade during migration window

## 4. Dependency Rules (Hard Constraints)

Allowed:

1. Feature packages -> `Muonroi.Core` and relevant `*.Abstractions`.
2. Implementation package -> same-concern abstraction package.
3. `Muonroi.Tenancy` -> `Muonroi.Tenancy.Core` -> `Muonroi.Tenancy.Abstractions`.
4. Web composition packages can depend on feature packages.

Forbidden:

1. `Muonroi.Core*` -> feature packages.
2. `*.Abstractions` -> implementation packages.
3. New packages -> `Muonroi.BuildingBlock`.
4. Cross-feature coupling without shared abstractions.

## 5. Naming and Branding Standard (Mandatory)

## 5.1 Package and Namespace

1. Package naming: `Muonroi.<Capability>[.<Implementation>]`.
2. Namespace root must mirror package root.
3. No ambiguous bucket names like `Common` or `Utils` without ADR approval.

## 5.2 Type Naming

1. Public types must be domain-descriptive, no symbolic prefix naming.
2. New public type names must not match `^M[A-Z]`.
3. Interfaces use `I*`.
4. DTOs use `*Request` / `*Response`.
5. Options use `*Options` or `*Settings` consistently per package.

## 5.3 Legacy `M*` Migration Strategy

Three-stage migration:

1. Additive stage: introduce new type names.
2. Deprecation stage: old names marked `[Obsolete]` with migration message.
3. Removal stage: remove legacy names in next major release.

Representative rename catalog:

1. `MDbContext` -> `MuonroiDbContext`
2. `MAuthInfoContext` -> `AuthInfoContext`
3. `MAuthenticateInfoContext` -> `AuthenticateInfoContext`
4. `MResponse` -> `ApiResponse`
5. `MVoidMethodResult` -> `VoidResult`
6. `MGenericController` -> `GenericCrudController`

## 6. Source-to-Package Extraction Matrix

Primary extraction map from `src/Muonroi.BuildingBlock`:

1. `External/SeedWorks/*`, `External/Exceptions/*`, generic helpers -> `Muonroi.Core`
2. generic interfaces in `Contract/*`, `External/Interfaces/*` -> `Muonroi.Core.Abstractions`
3. `External/Tenant/*`, `Shared/Tenancy/*` -> tenancy tri-layer packages
4. `External/BearerToken/*`, `External/OAuth/*`, auth rules/services -> `Muonroi.Auth`
5. `Internal/Services/PermissionService.cs`, `Shared/Authorization/*` -> `Muonroi.AuthZ`
6. EF-related persistence -> `Muonroi.Data.EntityFrameworkCore`
7. Dapper-related persistence -> `Muonroi.Data.Dapper`
8. distributed and memory caching -> `Muonroi.Caching.*`
9. `External/Messaging/*`, events/internal events -> `Muonroi.Messaging.*`
10. mediator + behaviors -> `Muonroi.Mediator`
11. gRPC/SignalR modules -> communication packages
12. controllers/middleware/filters/response/cors -> `Muonroi.AspNetCore`
13. swagger/versioning setup -> `Muonroi.AspNetCore.OpenApi`
14. logging/otel -> `Muonroi.Observability`
15. background jobs -> `Muonroi.BackgroundJobs.*`
16. consul/k8s/polly -> infra packages
17. `Shared/License|Policy|ControlPlane|Compliance|Operations|ServerValidation` -> `Muonroi.Governance`
18. `Shared/Rules/*` -> `Muonroi.RuleEngine.Runtime`

## 7. 12-Week Execution Roadmap (6 Phases)

## Phase 1 (Week 1-2): Foundation + Guardrails

Deliver:

1. Create foundational/abstraction projects.
2. Add architecture tests (dependency direction).
3. Add CI gates for dependency budget and forbidden references.
4. Start naming policy enforcement for new APIs.

Exit Criteria:

1. All tests green.
2. Architecture checks green.
3. New packages pack successfully.

## Phase 2 (Week 3-4): Data + Tenancy Decoupling

Deliver:

1. Build `Muonroi.Data.*` packages and move persistence concerns.
2. Introduce `Muonroi.Tenancy.Abstractions` and `Muonroi.Tenancy.Core`.
3. Remove `Muonroi.Tenancy*` dependency on monolith.

Exit Criteria:

1. No tenancy project references monolith.
2. Data/Tenancy packages build and test independently.

## Phase 3 (Week 5-6): Auth/AuthZ + Caching + Resilience

Deliver:

1. Move auth and authorization runtime to dedicated packages.
2. Split caching into abstraction + providers.
3. Extract resilience policies.
4. Add compatibility shims and `[Obsolete]` notices.

Exit Criteria:

1. Dedicated auth/authz/caching tests pass.
2. Legacy compatibility still compiles.

## Phase 4 (Week 7-8): Messaging + Communication + Web

Deliver:

1. Extract MassTransit and mediator runtime.
2. Extract gRPC and SignalR.
3. Extract ASP.NET Core and OpenAPI layers.
4. Remove monolith coupling from BFF and DecisionTable.Web.

Exit Criteria:

1. `Muonroi.RuleEngine.DecisionTable.Web` no longer references monolith.
2. Communication/web smoke tests pass.

## Phase 5 (Week 9-10): Infrastructure + Governance + Rule Runtime

Deliver:

1. Extract observability, jobs, service discovery, k8s modules.
2. Extract governance modules into `Muonroi.Governance`.
3. Extract runtime rule orchestration into `Muonroi.RuleEngine.Runtime`.

Exit Criteria:

1. No production `Shared/*` business runtime remains in monolith.
2. Governance/runtime packages have dedicated tests and docs.

## Phase 6 (Week 11-12): Compatibility + Release

Deliver:

1. Create `Muonroi.BuildingBlock.All` metapackage.
2. Convert `Muonroi.BuildingBlock` into compatibility facade.
3. Publish migration cookbook and scenario-based upgrade paths.
4. Release train: RC1 -> RC2 -> GA.

Exit Criteria:

1. Three sample migration scenarios pass end-to-end:
   - API (Auth + EF + Redis + AspNetCore)
   - Event service (Grpc + Messaging + Consul + Observability)
   - Worker (Dapper + Hangfire + Resilience)
2. Compatibility tests pass.

## 8. CI / Quality Gates

Mandatory gates:

1. Full solution restore/build/test.
2. Architecture dependency gate.
3. Package dependency budget gate.
4. API diff gate for breaking changes.
5. Naming gate (`^M[A-Z]` on new public APIs).
6. Packaging gate (`dotnet pack` for all packable projects).

## 9. KPI Dashboard

Track per phase:

1. Remaining production files in monolith.
2. Direct dependencies in monolith.
3. Number of src projects referencing monolith.
4. Build/test duration trend.
5. Count of public legacy `M*` types.

End-state targets:

1. Monolith is compatibility-only.
2. No production package depends on monolith.
3. New public APIs contain no uncontrolled `M*` naming.
4. All modular packages are release-ready.

## 10. Risk and Rollback

Top risks:

1. Hidden circular dependencies.
2. Consumer breaking changes.
3. Scope creep in governance modules.
4. CI duration growth.

Rollback strategy:

1. Feature rollback via compatibility facade.
2. Phase rollback to last stable release branch.
3. Package rollback by unlisting faulty versions and pinning previous versions in metapackage.

## 11. Definition of Done (Program Level)

Program is complete only when:

1. Modular package topology is implemented and validated.
2. Naming/branding standards are enforced in CI.
3. Monolith is compatibility-only with documented deprecation path.
4. Migration guides and sample upgrades are published and verified.

