# Muonroi Modularization - Execution Plan (Detailed)

## 1. Muc tieu va nguyen tac

Tai lieu nay la ban ke hoach thuc thi chi tiet de tach `Muonroi.BuildingBlock` thanh he package modular co the release duoc theo tung phase, khong de lai "vung xam" chua duoc map.

Nguyen tac bat buoc:

1. Khong big-bang rewrite.
2. Moi phase phai co quality gate ro rang.
3. Khong de package moi reference nguoc ve `Muonroi.BuildingBlock`.
4. Co migration path + backward compatibility co thoi han.
5. Muc tieu la "ship-able architecture", khong chi la tach file.
6. Naming/branding phai duoc chuan hoa, loai bo pattern `M*` khong co y nghia domain.

## 2. Baseline da xac minh (as-of 2026-03-01)

So lieu duoi day duoc lay truc tiep tu workspace hien tai:

- `src/Muonroi.BuildingBlock`: 405 file `.cs` (bao gom `obj`), 401 file source thuc te (loai `obj`).
- So thu muc con de quy: 117.
- `Muonroi.BuildingBlock.csproj`: 150 direct `PackageReference`.
- `project.assets.json`:
  - Top-level dependencies: 150
  - Total resolved libraries: 239
  - Transitive approx: 89
- Output artifact:
  - `Muonroi.BuildingBlock.dll`: ~1.49 MB (debug build local)
  - `Muonroi.BuildingBlock.1.9.13.nupkg`: ~0.98 MB (compressed package)

Hotspots theo line count:

1. `Internal/Services/PermissionService.cs` (1441 lines)
2. `Shared/ControlPlane/EnterpriseControlPlaneService.cs` (671 lines)
3. `External/Extensions/ArchitectureValidationExtensions.cs` (618 lines)
4. `Internal/Infrastructure/Authorize/AuthorizeInternal.cs` (603 lines)
5. `External/InfrastructureExtensions.cs` (519 lines)

Coupling quan trong can pha vo:

- `Muonroi.Tenancy` dang reference truc tiep `Muonroi.BuildingBlock`.
- `Muonroi.RuleEngine.DecisionTable.Web` dang reference truc tiep `Muonroi.BuildingBlock`.

## 3. Gap quan trong trong plan hien tai

Ba tai lieu da tao rat tot cho huong tong the, nhung de thuc thi full thi con 2 gap:

1. Cac module `Shared/*` (License, Policy, ControlPlane, Compliance, Operations, ServerValidation, Rules runtime) chua duoc map day du vao package dich.
2. Chua co gating/rollback strategy theo tung phase de release an toan.

Tai lieu nay bo sung day du 2 diem tren.

## 4. Target package topology (execution version)

### 4.1 Package ecosystem muc tieu

Nhom package theo concern:

1. Core:
   - `Muonroi.Core`
   - `Muonroi.Core.Abstractions`
2. Auth:
   - `Muonroi.Auth`
   - `Muonroi.AuthZ`
3. Data:
   - `Muonroi.Data.Abstractions`
   - `Muonroi.Data.EntityFrameworkCore`
   - `Muonroi.Data.Dapper`
4. Caching:
   - `Muonroi.Caching.Abstractions`
   - `Muonroi.Caching.Memory`
   - `Muonroi.Caching.Redis`
5. Messaging and application flow:
   - `Muonroi.Messaging.Abstractions`
   - `Muonroi.Messaging.MassTransit`
   - `Muonroi.Mediator`
6. Communication:
   - `Muonroi.Grpc`
   - `Muonroi.SignalR`
   - `Muonroi.Bff` (existing, keep)
7. Web:
   - `Muonroi.AspNetCore`
   - `Muonroi.AspNetCore.OpenApi`
8. Infrastructure:
   - `Muonroi.Observability`
   - `Muonroi.BackgroundJobs.Abstractions`
   - `Muonroi.BackgroundJobs.Hangfire`
   - `Muonroi.BackgroundJobs.Quartz`
   - `Muonroi.ServiceDiscovery.Consul`
   - `Muonroi.Kubernetes`
   - `Muonroi.Resilience`
9. Tenancy:
   - `Muonroi.Tenancy.Abstractions`
   - `Muonroi.Tenancy.Core`
   - `Muonroi.Tenancy` (host integration)
10. Governance and enterprise runtime (bo sung de cover full):
   - `Muonroi.Governance` (License + Policy + ControlPlane + Compliance + Operations + ServerValidation)
11. Rule Runtime extensions (bo sung de cover `Shared/Rules`):
   - `Muonroi.RuleEngine.Runtime`
12. Backward compatibility:
   - `Muonroi.BuildingBlock.All` (metapackage)

Ghi chu:

- Rule Engine core packages hien tai van giu nguyen (`Muonroi.RuleEngine.*`, `Muonroi.Rules`).
- Tenancy duoc chuan hoa theo mo hinh abstraction-first de clean dependency graph.
- `Muonroi.Governance` va `Muonroi.RuleEngine.Runtime` la 2 package bo sung de tranh bo sot module.

### 4.2 Dependency rules (bat buoc)

Allowed:

1. Feature package -> `Muonroi.Core` hoac `*.Abstractions`.
2. Implementation package -> abstraction cung concern.
3. Web package -> feature packages de compose.
4. `Muonroi.Tenancy` host package -> `Muonroi.Tenancy.Core` -> `Muonroi.Tenancy.Abstractions`.

Forbidden:

1. `Muonroi.Core*` -> bat ky feature package nao.
2. `*.Abstractions` -> implementation package.
3. Feature A -> Feature B neu khong qua abstraction hop le.
4. Bat ky package moi nao -> `Muonroi.BuildingBlock`.

## 5. Source-to-package extraction matrix

Bang mapping duoi day la pham vi di chuyen code tu `src/Muonroi.BuildingBlock`.

| Source scope | Target package |
|---|---|
| `External/SeedWorks/*`, `External/Exceptions/*`, `External/Helper/*`, `External/JsonConverter/*`, phan truly-generic trong `External/Common/*` | `Muonroi.Core` |
| Interface contracts generic trong `Contract/*`, `External/Interfaces/*` | `Muonroi.Core.Abstractions` |
| `External/BearerToken/*`, `External/OAuth/*`, `External/MAuthInfoContext.cs`, auth middleware lien quan, `Internal/Rules/Login/*`, `Internal/Services/AuthService.cs` | `Muonroi.Auth` |
| `Internal/Services/PermissionService.cs`, `Shared/Authorization/*`, authorize filters and policy glue | `Muonroi.AuthZ` |
| `External/Repositories/*` (contracts), `External/UnitOfWork/*` (contracts) | `Muonroi.Data.Abstractions` |
| `External/Entity/*`, `External/ORMs/*` (EF), `MDbContext`, EF config and relational persistence | `Muonroi.Data.EntityFrameworkCore` |
| `External/ORMs/*` (Dapper) | `Muonroi.Data.Dapper` |
| `External/Caching/*` contracts | `Muonroi.Caching.Abstractions` |
| memory cache implementations | `Muonroi.Caching.Memory` |
| redis/distributed cache implementations (`External/Caching/Distributed/Redis/*`) | `Muonroi.Caching.Redis` |
| `External/Events/*`, `External/InternalEvents/*`, message contracts | `Muonroi.Messaging.Abstractions` |
| `External/Messaging/*` (MassTransit adapters) | `Muonroi.Messaging.MassTransit` |
| `External/Mediator/*`, `Internal/Behaviours/*` | `Muonroi.Mediator` |
| `External/Grpc/*` | `Muonroi.Grpc` |
| `External/SignalR/*` | `Muonroi.SignalR` |
| `External/Controller/*`, `External/Middleware/*` (web-generic), `External/Filters/*`, `External/Cors/*`, `External/Response/*` | `Muonroi.AspNetCore` |
| Swagger/versioning/openapi setup trong `External/Default/*` va openapi extensions | `Muonroi.AspNetCore.OpenApi` |
| `External/Logging/*`, `External/Observability/*`, linked `src/Muonroi.Observability/OtelSetup.cs`, telemetry wiring | `Muonroi.Observability` |
| `External/BackgroundJobs/*` contracts | `Muonroi.BackgroundJobs.Abstractions` |
| Hangfire implementation | `Muonroi.BackgroundJobs.Hangfire` |
| Quartz implementation | `Muonroi.BackgroundJobs.Quartz` |
| `External/Consul/*` | `Muonroi.ServiceDiscovery.Consul` |
| `External/Kubernetes/*` | `Muonroi.Kubernetes` |
| `External/Polly/*`, resilience policies | `Muonroi.Resilience` |
| `External/Tenant/*`, `Shared/Tenancy/*` | `Muonroi.Tenancy` |
| tenancy contracts (`ITenantContext`, resolvers, factories interfaces) | `Muonroi.Tenancy.Abstractions` |
| tenancy runtime core (resolution pipeline, tenant validator core) | `Muonroi.Tenancy.Core` |
| aspnet middleware/DI host integration for tenancy | `Muonroi.Tenancy` |
| `Shared/License/*`, `Shared/Policy/*`, `Shared/ControlPlane/*`, `Shared/Compliance/*`, `Shared/Operations/*`, `Shared/ServerValidation/*` | `Muonroi.Governance` |
| `Shared/Rules/*` | `Muonroi.RuleEngine.Runtime` |
| Legacy convenience and compatibility packaging | `Muonroi.BuildingBlock.All` |

## 6. 12-week implementation roadmap (6 phases)

## Phase 1 (Week 1-2): Foundation and guardrails

Muc tieu:

- Dung khung modular hoa va co co che ngan architectural regression.

Deliverables:

1. Tao projects:
   - `Muonroi.Core`
   - `Muonroi.Core.Abstractions`
   - `Muonroi.Data.Abstractions`
   - `Muonroi.Caching.Abstractions`
   - `Muonroi.Messaging.Abstractions`
   - `Muonroi.BackgroundJobs.Abstractions`
   - `Muonroi.Tenancy.Abstractions`
   - `Muonroi.Tenancy.Core`
2. Bat Central Package Management (`Directory.Packages.props`) neu chua co.
3. Them architecture tests:
   - enforce forbidden references.
   - enforce no package moi reference `Muonroi.BuildingBlock`.
4. Them dependency KPI scripts:
   - direct package count per project
   - resolved library count per project
5. Refactor CI:
   - build and test all test projects (khong chi 2 suites)
   - architecture test bat buoc pass.

Phase 1 gate:

- Solution build xanh.
- Architecture tests pass.
- No new project references to `Muonroi.BuildingBlock`.

## Phase 2 (Week 3-4): Data and Tenancy de-coupling

Muc tieu:

- Tach persistence concern ra khoi monolith.
- Bo coupling `Muonroi.Tenancy -> Muonroi.BuildingBlock`.

Deliverables:

1. Tao `Muonroi.Data.EntityFrameworkCore`, `Muonroi.Data.Dapper`.
2. Move persistence code theo matrix.
3. Tach `Muonroi.Tenancy` theo 3 lop:
   - contracts -> `Muonroi.Tenancy.Abstractions`
   - runtime core -> `Muonroi.Tenancy.Core`
   - host integration -> `Muonroi.Tenancy`
   - remove `ProjectReference` tu `Muonroi.Tenancy*` sang `Muonroi.BuildingBlock`.
4. Them test projects:
   - `Muonroi.Data.EntityFrameworkCore.Tests`
   - `Muonroi.Data.Dapper.Tests`
5. Update samples/docs data registration.

Phase 2 gate:

- `Muonroi.Tenancy*.csproj` khong con reference `Muonroi.BuildingBlock`.
- Data packages pack duoc.
- Regression tests xanh.

## Phase 3 (Week 5-6): Auth, AuthZ, Caching, Resilience

Muc tieu:

- Tach security va caching concern.

Deliverables:

1. Move auth runtime vao `Muonroi.Auth`.
2. Move permission/policy runtime vao `Muonroi.AuthZ`.
3. Tao `Muonroi.Caching.Memory`, `Muonroi.Caching.Redis`.
4. Tao `Muonroi.Resilience`.
5. Remove auth/caching/polly implementations khoi `Muonroi.BuildingBlock`.
6. Them compatibility shims trong package legacy (neu can) voi `[Obsolete]`.

Phase 3 gate:

- Auth/AuthZ co test pass doc lap.
- Caching layer test integration pass (memory + redis).
- Khong con auth/caching implementation code trong `Muonroi.BuildingBlock`.

## Phase 4 (Week 7-8): Messaging, Mediator, Communication, Web

Muc tieu:

- Tach communication stack va app pipeline concerns.

Deliverables:

1. Tao `Muonroi.Messaging.MassTransit`, `Muonroi.Mediator`.
2. Tach `Muonroi.Grpc`, `Muonroi.SignalR`.
3. Tach `Muonroi.AspNetCore`, `Muonroi.AspNetCore.OpenApi`.
4. `Muonroi.Bff` chuyen sang consume package moi thay vi consume monolith.
5. Giai quyet coupling `Muonroi.RuleEngine.DecisionTable.Web -> Muonroi.BuildingBlock`.

Phase 4 gate:

- `Muonroi.RuleEngine.DecisionTable.Web.csproj` khong con reference `Muonroi.BuildingBlock`.
- Web and comm packages co smoke tests.
- Bff tests xanh voi package references moi.

## Phase 5 (Week 9-10): Infrastructure and Governance

Muc tieu:

- Tach cac concern deployment/runtime governance con lai.

Deliverables:

1. Tach `Muonroi.Observability`.
2. Tach `Muonroi.BackgroundJobs.Hangfire`, `Muonroi.BackgroundJobs.Quartz`.
3. Tach `Muonroi.ServiceDiscovery.Consul`, `Muonroi.Kubernetes`.
4. Tao `Muonroi.Governance` cho:
   - licensing
   - policy verification/enforcement
   - control plane
   - compliance export/evidence
   - operations compatibility/slo presets
   - server validation chain
5. Tao `Muonroi.RuleEngine.Runtime` cho `Shared/Rules/*`.

Phase 5 gate:

- Toan bo `Shared/*` da co package dich ro rang.
- Khong con code enterprise/rule-runtime chay trong monolith.
- Governance and runtime tests xanh.

## Phase 6 (Week 11-12): Compatibility, deprecation, release

Muc tieu:

- Dong goi migration day du va chuan bi release.

Deliverables:

1. Tao `Muonroi.BuildingBlock.All` metapackage.
2. Chuyen `Muonroi.BuildingBlock` thanh compatibility facade:
   - references package moi
   - wrappers/type-forwarders cho public API quan trong
   - `[Obsolete]` message ro migration target
3. Hoan tat migration guide theo scenario.
4. Update templates/samples/scripts.
5. Release train:
   - RC1
   - RC2
   - GA

Phase 6 gate:

- Consumers sample migrate thanh cong theo 3 kịch ban:
  - API don gian (Auth + EF + Redis)
  - Event service (Grpc + Kafka + Consul)
  - Worker (Dapper + Hangfire)
- Backward compatibility package build va test pass.

## 7. Backlog chi tiet theo workstream

## 7.1 Architecture and tooling

1. Them test architecture cho allowed/forbidden references.
2. Them script KPI dependency/file-count.
3. Them `dotnet list package --include-transitive` gate theo threshold.
4. Them pack-and-smoke-test workflow cho tung package.

## 7.2 API compatibility

1. Baseline public API cua `Muonroi.BuildingBlock` truoc khi tach.
2. Moi phase generate API diff report.
3. Type forwarding/wrapper cho nhom API critical.
4. Obsolete policy:
   - warning o v2
   - removal o v3 (sau migration window).

## 7.3 Testing strategy

1. Unit tests theo package.
2. Contract tests cho auth, cache, messaging, governance.
3. Integration tests theo scenario app.
4. Non-functional gates:
   - startup time
   - memory footprint
   - restore/build duration.

## 7.4 Naming and branding standardization

1. Ap dung tai lieu `NAMING_AND_BRANDING_STANDARD.md` cho toan bo package moi.
2. Xay dung rename catalog old -> new cho nhom public APIs co prefix `M*`.
3. Phase rename:
   - Phase additive: introduce ten moi + keep alias/wrapper.
   - Phase deprecate: `[Obsolete]` ten cu.
   - Phase major: remove ten cu.
4. Add CI guard:
   - fail neu co public type moi match regex `^M[A-Z]` (ngoai whitelist).
5. Namespace alignment:
   - namespace root phai mirror package name.

## 8. Quality gates and measurable KPIs

KPIs bat buoc track moi phase:

1. So source files trong `Muonroi.BuildingBlock`.
2. Direct dependencies cua `Muonroi.BuildingBlock`.
3. So project dang reference `Muonroi.BuildingBlock`.
4. Build duration cho baseline solution.
5. Test pass rate.
6. So public types con prefix `M*`.

Target end-state:

1. `Muonroi.BuildingBlock` con vai tro facade hoac empty metapackage.
2. Khong project production nao trong `src/` reference truc tiep `Muonroi.BuildingBlock`.
3. Moi package feature co direct dependencies <= 20 (target; co exception duoc ADR phe duyet).
4. Moi package co test project tuong ung.
5. Public API moi khong con `M*` naming anti-pattern.

## 9. Risk register and mitigation

1. Risk: Circular dependency sau khi tach.
   - Mitigation: architecture tests + abstraction-first extraction.
2. Risk: Breaking API cho consumer cu.
   - Mitigation: compatibility facade + wrappers + phased obsolete.
3. Risk: Scope creep do module enterprise phuc tap.
   - Mitigation: gom module enterprise vao `Muonroi.Governance` wave rieng (Phase 5), co owner ro rang.
4. Risk: CI time tang do nhieu package.
   - Mitigation: matrix build + parallel test + caching restore.
5. Risk: Data/auth behavior regression.
   - Mitigation: golden integration tests truoc va sau migration.

## 10. Rollback strategy

Rollback level:

1. Feature rollback:
   - Giu code cu trong legacy facade den khi package moi on dinh.
2. Phase rollback:
   - Neu phase fail gate, release tu branch stable truoc do.
3. Package rollback:
   - Unlist package version loi, pin dependency ve previous version trong metapackage.

## 11. Governance model

Moi package can:

1. Co owner chinh.
2. Co README rieng (purpose, install, quickstart, dependencies).
3. Co changelog rieng (semantic versioning).
4. Co migration note neu co breaking changes.

## 12. Immediate next actions (sprint kickoff)

Checklist kickoff ngay:

1. Freeze baseline metrics vao dashboard (`docs/`).
2. Tao branch `refactor/modularization-phase1`.
3. Scaffold 6 abstraction/core projects trong Phase 1.
4. Add architecture tests va CI gates.
5. Cut PR nho:
   - PR1: scaffolding + CI
   - PR2: Core extraction
   - PR3: Core.Abstractions extraction

---

Tai lieu lien quan:

- `REFACTORING_PLAN.md`
- `REFACTORING_SUMMARY.md`
- `PACKAGE_DEPENDENCY_GRAPH.md`

Tai lieu nay la ban execution de team di vao implementation theo tung phase, co gate, co rollback va co KPI do luong.
