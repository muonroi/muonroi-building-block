# UI Contract-First Upgrade Plan

## Objective

Build a backend-driven UI contract so frontend apps (Angular, React, MVC) focus on UI/UX rendering while core behavior is owned by backend:

- Menu/action visibility and enable/disable from backend.
- Permission/policy/license-aware rendering rules from backend.
- FE API models generated automatically from backend OpenAPI.
- Shared runtime conventions (auth, tenant, interceptors, error contract) predefined.

Hybrid rule:

- Backend contract/composer/API live in `MuonroiBuildingBlock`.
- Frontend runtime core/adapters live in `Muonroi.Ui.Engine`.

## Research Baseline (Primary Sources)

- ABP CLI UI options (`mvc`, `angular`, `blazor`, `none`):
  https://abp.io/docs/5.1/CLI
- ABP Angular service proxy generation:
  https://abp.io/docs/9.0/framework/ui/angular/service-proxies
- ASP.NET Core OpenAPI in .NET 9 (runtime + build-time generation):
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-9.0
- ASP.NET Core 9 release notes (OpenAPI support):
  https://learn.microsoft.com/en-us/aspnet/core/release-notes/aspnetcore-9.0?view=aspnetcore-9.0
- OpenAPI Generator CLI usage and `typescript-angular` generator:
  https://openapi-generator.tech/docs/usage
  https://openapi-generator.tech/docs/generators/typescript-angular
- OPA REST API (policy decision endpoint patterns):
  https://www.openpolicyagent.org/docs/rest-api
- BFF architecture pattern:
  https://learn.microsoft.com/en-us/azure/architecture/patterns/backends-for-frontends

## Current Muonroi Baseline (Code)

- Permission metadata and tree APIs already exist:
  - `MAuthControllerBase`: `permission-definitions`, `menu-metadata`, `permission-tree`
  - `PermissionService` builds permission tree and menu metadata
- RBAC + PDP integration already exists:
  - `PermissionFilter`, `AuthorizePermissionFilter`, `MPolicyDecisionService`
- OpenAPI/Swagger pipeline exists in infra and templates.

## Phase Plan

## Phase A: UI Contract Foundation

- Add versioned UI manifest contracts (`M` prefix) in building block.
- Define BE-owned rendering flags:
  - `visible`, `enabled`, `hidden`, `disabledReason`
  - `order`, `route`, `icon`, `type`, `parentUiKey`
- Add route normalization helper for deterministic FE routing.

Deliverables:
- New manifest model types.
- Unit tests for contract serialization and route normalization.

## Phase B: Backend Manifest Composer

- Add backend service method to compose a full UI manifest from:
  - Permission definitions
  - User granted permissions
  - Parent-child hierarchy
  - Publish state and order metadata
- Add API endpoint to expose manifest for a user.
- Keep advanced-auth license gate enforced.

Deliverables:
- `GetUiManifest` API.
- PermissionService coverage tests:
  - visibility/enablement behavior
  - parent-visible-on-child behavior
  - disabled reason mapping

## Phase C: OpenAPI Client Generation Pipeline (Cross-platform First)

- Add script-first generation flow with `sh` as primary runtime.
- Generate FE SDKs from OpenAPI for:
  - Angular (typescript-angular)
  - React (typescript-fetch)
  - MVC/BFF support (C# client optional in same script)
- Keep script output deterministic for CI and local.

Deliverables:
- `scripts/generate-ui-clients.sh` in `MuonroiBuildingBlock`.
- Template integration stubs to call this script from CI/dev workflows.

## Phase D: Template UI Selection (Base/Modular/Micro)

- Introduce template option for UI stack:
  - `angular`, `react`, `mvc`, `none`
- Scaffold FE/BFF shell with predefined:
  - auth interceptor
  - tenant/correlation headers
  - standard error handling
  - manifest bootstrap flow

Deliverables:
- Template parameter wiring and scaffold artifacts.
- Smoke tests for generated solution bootstrapping.

## Phase E: Dynamic Rendering Runtime

- Build FE runtime adapters to render from backend manifest:
  - dynamic menu
  - button action gating
  - route guards and component mapping
- Optional JSON-schema based form/table rendering hooks.

Deliverables:
- Runtime packages/modules for Angular/React/MVC shell.
- Behavior tests for hide/disable/show and guard logic.

## Phase F: Governance, Observability, Backward Compatibility

- Add manifest-version and policy-decision telemetry.
- Add contract-diff checks to prevent accidental breaking changes.
- Keep backward compatibility with existing permission APIs.

Deliverables:
- Contract compatibility tests.
- Operational docs and migration notes.

## Done Criteria (Agent Standard)

Done only when all are true:

1. Plan phases tracked and completed.
2. Unit tests pass 100% (including new test cases for added behavior).
3. No quick workaround; implementation is research-backed.
4. Developer-facing types keep `M` prefix.
5. Cross-platform script priority: `.sh` first, PowerShell optional secondary.

## Execution Order for This Upgrade

1. Phase A + B in `MuonroiBuildingBlock` first.
2. Phase C script in `MuonroiBuildingBlock` (with `.sh` primary).
3. Then template integration phases (D/E/F) across:
   - `Muonroi.BaseTemplate`
   - `Muonroi.Modular.Template`
   - `Muonroi.Microservices.Template`

## Progress Snapshot (2026-02-21)

- Phase A: Completed
- Phase B: Completed
- Phase C: Completed (base script in building block)
- Phase D: Completed
- Phase E: Completed (runtime-current manifest endpoint + shell updates)
- Phase F: Completed
- Hybrid Runtime Split: Completed (`Muonroi.Ui.Engine` runtime + template integration + sync scripts)
