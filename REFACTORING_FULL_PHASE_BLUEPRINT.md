# Muonroi Refactoring - Full Phase Blueprint (No Workaround)

## Scope

Tai lieu nay la ban thuc thi day du cho tat ca phase, theo dung scope "full all phase".

No ket hop:

1. Mapping source code hien tai -> package dich.
2. Work packages theo tung phase.
3. Definition of Done (DoD) tung package.
4. CI/test/release gates de co the ship.
5. Backward-compatibility khong lam vo ecosystem ngay lap tuc.

## A. Baseline lock

Baselines duoc khoa de do tien do:

1. `Muonroi.BuildingBlock` source files: 401 (khong tinh `obj`).
2. Direct package refs: 150.
3. Resolved libraries: 239.
4. Coupling do:
   - `Muonroi.Tenancy` -> `Muonroi.BuildingBlock`
   - `Muonroi.RuleEngine.DecisionTable.Web` -> `Muonroi.BuildingBlock`

Done dieu kien bat buoc:

1. Moi phase phai giam duoc it nhat mot coupling lon.
2. Khong phase nao duoc tang them dependency vao monolith.

## B. Full package ledger

So package muc tieu:

1. `Muonroi.Core`
2. `Muonroi.Core.Abstractions`
3. `Muonroi.Data.Abstractions`
4. `Muonroi.Data.EntityFrameworkCore`
5. `Muonroi.Data.Dapper`
6. `Muonroi.Caching.Abstractions`
7. `Muonroi.Caching.Memory`
8. `Muonroi.Caching.Redis`
9. `Muonroi.Messaging.Abstractions`
10. `Muonroi.Messaging.MassTransit`
11. `Muonroi.Mediator`
12. `Muonroi.Grpc`
13. `Muonroi.SignalR`
14. `Muonroi.AspNetCore`
15. `Muonroi.AspNetCore.OpenApi`
16. `Muonroi.Observability`
17. `Muonroi.BackgroundJobs.Abstractions`
18. `Muonroi.BackgroundJobs.Hangfire`
19. `Muonroi.BackgroundJobs.Quartz`
20. `Muonroi.ServiceDiscovery.Consul`
21. `Muonroi.Kubernetes`
22. `Muonroi.Resilience`
23. `Muonroi.Auth`
24. `Muonroi.AuthZ`
25. `Muonroi.Tenancy.Abstractions`
26. `Muonroi.Tenancy.Core`
27. `Muonroi.Tenancy`
28. `Muonroi.Governance`
29. `Muonroi.RuleEngine.Runtime`
30. `Muonroi.BuildingBlock.All` (metapackage)

Ghi chu:

1. Rule Engine core packages dang ton tai giu nguyen va bo sung `Muonroi.RuleEngine.Runtime`.
2. Tenancy duoc nang cap thanh mo hinh abstraction-first:
   - `Muonroi.Tenancy.Abstractions` (contracts)
   - `Muonroi.Tenancy.Core` (runtime engine)
   - `Muonroi.Tenancy` (host integration/extensions)
3. Bff/Auth/AuthZ la package ton tai, se restructure noi bo.

## C. Phase execution details

## Phase 1 (W1-W2) - Foundation and architecture guardrails

Objective:

1. Tao abstraction-first skeleton cho toan bo ecosystem.
2. Ngay tu dau co anti-regression cho dependency direction.

Work packages:

1. `Muonroi.Core` scaffold + first extraction:
   - move `External/SeedWorks/*`
   - move `External/Exceptions/*`
   - move generic helpers (`External/Helper/*`, `External/JsonConverter/*`, `External/Timing/*`)
2. `Muonroi.Core.Abstractions` scaffold:
   - move generic interfaces (`Contract/Interfaces/*`, subset `External/Interfaces/*` truly generic)
3. Abstractions scaffolding:
   - `Muonroi.Data.Abstractions`
   - `Muonroi.Caching.Abstractions`
   - `Muonroi.Messaging.Abstractions`
   - `Muonroi.BackgroundJobs.Abstractions`
   - `Muonroi.Tenancy.Abstractions`
4. Multi-tenant core scaffold:
   - `Muonroi.Tenancy.Core`
   - migration base contracts tu `External/Tenant/*` vao abstraction package
   - migration runtime-neutral implementation vao `Muonroi.Tenancy.Core`
5. Architecture tests project:
   - rule: no project may reference `Muonroi.BuildingBlock` except temporary legacy adapters.
   - rule: `*.Abstractions` cannot reference implementations.
6. CI upgrade:
   - run full test matrix, not only 2 test projects.
   - add dependency gate script (count top-level package references).

DoD phase 1:

1. Solution build + tests green.
2. Architecture tests green.
3. New core/abstractions packages can pack.
4. No new API break in existing public packages.

## Phase 2 (W3-W4) - Data and tenancy decoupling

Objective:

1. Giam coupling persistence.
2. Cat dependency truc tiep `Muonroi.Tenancy -> Muonroi.BuildingBlock`.

Work packages:

1. `Muonroi.Data.EntityFrameworkCore`:
   - move `External/Entity/*` (except auth-specific entities if needed split later)
   - move EF configs (`External/Entity/EFConfig/*`)
   - move db configurators in `External/Entity/DatabaseConfig/*`
2. `Muonroi.Data.Dapper`:
   - move `External/ORMs/Dapper/*`
3. Tenancy full decouple:
   - contracts -> `Muonroi.Tenancy.Abstractions`
   - runtime core -> `Muonroi.Tenancy.Core`
   - host integration -> `Muonroi.Tenancy`
   - move `External/Tenant/*`, `Shared/Tenancy/*` theo 3 lop tren.
   - remove `ProjectReference` to monolith.
4. Data registration extensions:
   - package-specific `IServiceCollection` extension methods.
5. Tests:
   - add data unit tests and integration smoke tests.

DoD phase 2:

1. `Muonroi.Tenancy*.csproj` khong con reference `Muonroi.BuildingBlock`.
2. Data packages build/test/pack green.
3. Existing consumers still compile via compatibility path.

## Phase 3 (W5-W6) - Auth, AuthZ, Caching, Resilience

Objective:

1. Tach security + cache concerns khoi monolith.
2. Co package composition ro cho API stack thong dung.

Work packages:

1. `Muonroi.Auth` enhancement:
   - move `External/BearerToken/*`, `External/OAuth/*`, `External/MAuthInfoContext.cs`,
   - move login rules in `Internal/Rules/Login/*`,
   - move `Internal/Services/AuthService.cs`.
2. `Muonroi.AuthZ` enhancement:
   - move `Internal/Services/PermissionService.cs`,
   - move authz filters and shared authz contracts (`Shared/Authorization/*`).
3. `Muonroi.Caching.Memory` and `Muonroi.Caching.Redis`:
   - move `External/Caching/Distributed/MultiLevel/*` and `External/Caching/Distributed/Redis/*`.
4. `Muonroi.Resilience`:
   - move `External/Polly/*`, retry policy registration.
5. Compatibility shims:
   - keep old extension methods in monolith forwarding to new packages.

DoD phase 3:

1. Auth/AuthZ tests green in dedicated projects.
2. Caching tests include redis path.
3. No duplicate source of truth between monolith and new packages.

## Phase 4 (W7-W8) - Messaging, mediator, communication, web

Objective:

1. Tach application transport + web composition layer.
2. Cat coupling `DecisionTable.Web -> BuildingBlock`.

Work packages:

1. `Muonroi.Messaging.MassTransit`:
   - move `External/Messaging/*`.
2. `Muonroi.Mediator`:
   - move `External/Mediator/*` and `Internal/Behaviours/*`.
3. `Muonroi.Grpc`:
   - move `External/Grpc/*`.
4. `Muonroi.SignalR`:
   - move `External/SignalR/*`.
5. `Muonroi.AspNetCore`:
   - move `External/Controller/*`, `External/Middleware/*`, `External/Filters/*`, `External/Cors/*`, `External/Response/*`.
6. `Muonroi.AspNetCore.OpenApi`:
   - move `External/Default/*` and swagger/versioning wiring.
7. Refactor `Muonroi.Bff` and `Muonroi.RuleEngine.DecisionTable.Web` to depend on new packages.

DoD phase 4:

1. `Muonroi.RuleEngine.DecisionTable.Web` khong con reference monolith.
2. Web and comm smoke tests green.
3. Messaging integration tests (RabbitMQ/Kafka stubs or contract tests) green.

## Phase 5 (W9-W10) - Infrastructure, governance, runtime

Objective:

1. Tach toan bo domain enterprise va runtime governance con lai.
2. Khong de module "Shared/*" lang nhang trong monolith.

Work packages:

1. `Muonroi.Observability`:
   - move logging/otel code (`External/Logging/*`, `External/Observability/*`, linked `OtelSetup.cs`).
2. Background jobs:
   - `Muonroi.BackgroundJobs.Hangfire`
   - `Muonroi.BackgroundJobs.Quartz`
   - base contracts in `Muonroi.BackgroundJobs.Abstractions`
3. Infra adapters:
   - `Muonroi.ServiceDiscovery.Consul` (`External/Consul/*`)
   - `Muonroi.Kubernetes` (`External/Kubernetes/*`)
4. `Muonroi.Governance`:
   - move `Shared/License/*`
   - move `Shared/Policy/*`
   - move `Shared/ControlPlane/*`
   - move `Shared/Compliance/*`
   - move `Shared/Operations/*`
   - move `Shared/ServerValidation/*`
5. `Muonroi.RuleEngine.Runtime`:
   - move `Shared/Rules/*` runtime orchestration.
6. Rule engine package alignment:
   - verify all rule runtime dependencies chay qua `Muonroi.RuleEngine.Abstractions`
   - host adapters nam o `Muonroi.RuleEngine.Runtime`

DoD phase 5:

1. Folder `Shared/*` trong monolith con 0 source production file.
2. Governance and runtime packages co tests va docs rieng.
3. End-to-end enterprise scenario tests xanh.

## Phase 6 (W11-W12) - Compatibility hardening and release

Objective:

1. Chuyen monolith thanh compatibility facade va release duoc.

Work packages:

1. Tao `Muonroi.BuildingBlock.All` metapackage.
2. Convert `Muonroi.BuildingBlock`:
   - facade + type forwarding where possible.
   - keep minimal compatibility extension methods.
3. Obsolete strategy:
   - obsolete warning with specific replacement package.
4. Migration assets:
   - cookbook by scenario.
   - package selection matrix.
   - before/after code snippets.
5. Release:
   - RC1 -> RC2 -> GA with changelog va migration notes.

DoD phase 6:

1. 3 sample applications migrate thanh cong:
   - API: Auth + EF + Redis + AspNetCore
   - Event service: Grpc + MassTransit + Consul + Observability
   - Worker: Dapper + Hangfire + Resilience
2. Backward compatibility package build/test/pass.
3. Documentation complete and publish-ready.

## D. Package-level Definition of Done

Moi package duoc danh dau hoan tat khi dat du 12 tieu chi:

1. Co `README.md` package.
2. Co changelog section.
3. Co semantic version entry.
4. Co unit tests.
5. Co integration/smoke tests neu package co IO.
6. Co DI registration extensions.
7. Khong reference `Muonroi.BuildingBlock`.
8. Khong co circular project references.
9. Public API reviewed.
10. Deprecation mapping from old namespace documented.
11. Pack succeeds (`dotnet pack`).
12. Sample usage compile duoc.

## E. CI and quality gates (mandatory)

Pipeline gates them vao CI:

1. `dotnet restore/build/test` full solution.
2. Architecture test gate.
3. Dependency budget gate:
   - fail if package vuot ngưong direct dependencies da set.
4. API diff gate:
   - fail if breaking changes chua co migration annotation.
5. Packaging gate:
   - all packable projects must pack successfully.

## F. Breaking change policy

1. Breaking changes chi duoc merge neu:
   - co migration note.
   - co obsolete path (neu practical).
2. Versioning:
   - major for breaking,
   - minor for additive,
   - patch for fixes.
3. `Muonroi.BuildingBlock` duy tri compatibility window it nhat 2 major releases.

## G. Risk and mitigation matrix

1. Circular dependencies:
   - Mitigation: enforce architecture tests from phase 1.
2. Consumer regression:
   - Mitigation: maintain facade + sample migration tests.
3. Team velocity drop due to many projects:
   - Mitigation: template/script scaffolding, parallel streams.
4. Hidden coupling in static helpers:
   - Mitigation: explicit source scan and namespace remap checklist.
5. Test flakiness due to infra integrations:
   - Mitigation: contract tests + deterministic test doubles + optional integration stage.

## H. Command checklist per phase

Core commands:

1. `dotnet sln Muonroi.BuildingBlock.sln add <new-project.csproj>`
2. `dotnet test Muonroi.BuildingBlock.sln --nologo -v minimal`
3. `dotnet pack Muonroi.BuildingBlock.sln -c Release`
4. `dotnet list <project>.csproj package --include-transitive`

Validation commands:

1. Architecture test runner command (to be added in phase 1).
2. Dependency budget checker script command (to be added in phase 1).
3. API diff checker command (to be added in phase 2).

## I. Tracking board template

Tracking columns:

1. `Backlog`
2. `In Progress`
3. `In Review`
4. `Blocked`
5. `Done`

Ticket template fields:

1. Package
2. Source mapping
3. Dependencies added/removed
4. Tests added/updated
5. Docs updated
6. Migration impact
7. Rollback plan

## J. Final acceptance criteria for whole program

Program considered complete only if all criteria below are met:

1. `Muonroi.BuildingBlock` khong con chua business implementation chinh.
2. All new packages pack and publish-ready.
3. No src project depends on monolith except compatibility facade.
4. Full solution tests green.
5. Migration guide covers all major consumer scenarios.
6. CI gates enforce architecture and dependency budgets.
7. Release notes and deprecation path complete.
