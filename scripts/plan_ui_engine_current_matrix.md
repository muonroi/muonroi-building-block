# UI Engine Current Test Plan

## Goal
Validate why `GET /api/v1/Auth/ui-engine/current` returns `401` and separate:
- global auth/session issue
- endpoint-specific policy/claim issue
- tenant-header related issue

## Scope
- Endpoint under test:
  - `GET /api/v1/Auth/ui-engine/current`
- Control endpoints:
  - `GET /api/v1/Auth/users` (authenticated + advanced auth)
  - `GET /api/v1/Auth/profile/current` (authenticated + current-user claim resolution)
  - `GET /api/v1/Auth/verify-token` (sanity only, allow-anonymous)
  - `GET /api/v1/Auth/ui-engine/contract-info` (anonymous baseline)
  - `GET /api/v1/Auth/ui-engine/schema-hash` (anonymous baseline)

## Test Matrix
Run each case for:
- New registered user token
- Seeded admin token (`admin/sysadmin`)
- Header variants: no tenant, `x-tenant-id`

Cases:
1. No token -> `ui-engine/current`
2. Bearer only -> `ui-engine/current`
3. Bearer + tenant -> `ui-engine/current`
4. Session/cookie only -> `ui-engine/current`
5. Bearer only -> `/Auth/users`
6. Bearer + tenant -> `/Auth/users`
7. Bearer only -> `/Auth/profile/current`
8. Bearer + tenant -> `/Auth/profile/current`
9. Anonymous -> `/Auth/ui-engine/contract-info`
10. Anonymous -> `/Auth/ui-engine/schema-hash`

## Evaluation Rules
- In multi-tenant mode (`MultiTenantConfigs.Enabled = true`), expect:
  - Bearer without `x-tenant-id` => `401`
  - Bearer with `x-tenant-id` => should pass auth pipeline if library is healthy
- If `/Auth/users` and `/Auth/profile/current` are both `401` while token is present:
  - classify as **global auth/session pipeline issue** before `ui-engine/current`.
- If `/Auth/users` is `200` but `ui-engine/current` is `401`:
  - classify as **ui-engine/current specific issue** (policy/claim resolution).
- `verify-token` result is informational only (allow-anonymous endpoint).

## Current Finding (2026-03-03)
- `401` root cause was **library auth validator**, not permission assignment.
- After fixing `Muonroi.Data.EntityFrameworkCore/Auth/DefaultRefreshTokenValidator`:
  - `ui-engine/current` and `/Auth/users` return `200` when Bearer + `x-tenant-id` are provided.
  - Assigning Admin role changes permission claim (`0 -> 260607`) but is not required to avoid `401`.

## Automation Script
Use:
- `scripts/flow-ui-engine-current-matrix.ps1`
- `scripts/flow-ui-engine-current-matrix.cmd`

Example:

```powershell
.\scripts\flow-ui-engine-current-matrix.ps1 `
  -ProjectPath "D:\sources\Core\_tmp\rule_engine_toolkit_matrix_20260303_1530\PairMultiTenantE2E\src\PairMultiTenantE2E.API\PairMultiTenantE2E.API.csproj" `
  -ActivationProofPath "D:\sources\Core\_tmp\flow_license_modes_20260303_160719\PairMultiTenantE2E.API\enterprise\activation_proof.json" `
  -LicenseMode Enterprise
```

## Expected Outputs
- Evidence JSON with:
  - JWT claims extracted from access token
  - status/body per matrix case
  - automatic diagnosis (`Analysis.SuspectedIssue`)
  - stdout/stderr log paths
