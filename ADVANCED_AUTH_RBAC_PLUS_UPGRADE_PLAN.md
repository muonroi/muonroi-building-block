# Advanced Auth (RBAC+) Upgrade Plan (Licensed + Enterprise)

## 1. Muc tieu

Nang cap RBAC+ theo huong:
- `deny-by-default`
- `tenant-consistent authorization`
- `license-gated by feature advanced-auth`
- `cache-consistent sau moi thay doi role/permission`

Muc tieu la dua he thong hien tai tien gan hon cac nen tang RBAC+ manh tren thi truong, nhung van giu tuong thich cho cac template hien co.

## 2. Benchmark RBAC+ thi truong (baseline doi chieu)

### 2.1. Keycloak Authorization Services
- Ho tro resource + scope + policy, permission-based decision.
- Ho tro positive/negative logic, decision strategy.
- Tai lieu: https://www.keycloak.org/docs/latest/authorization_services/

### 2.2. Auth0 RBAC / FGA
- Core RBAC theo API permission.
- FGA theo mo hinh relationship (Zanzibar-like) cho fine-grained authorization.
- Tai lieu:
  - https://auth0.com/docs/manage-users/access-control/rbac
  - https://auth0.com/docs/fine-grained-authorization

### 2.3. OpenFGA (Zanzibar-inspired)
- Relationship-based model, typed relation, evaluate access theo tuple graph.
- Ho tro mo hinh phan quyen theo role relation, object relation, condition.
- Tai lieu:
  - https://openfga.dev/docs/modeling/getting-started
  - https://openfga.dev/docs/modeling/roles-and-permissions

### 2.4. Casbin
- Ho tro RBAC with domains (multi-tenant aware), ABAC va policy model linh hoat.
- Tai lieu:
  - https://casbin.org/docs/overview/
  - https://casbin.org/docs/rbac-with-domains

### 2.5. OPA
- Policy-as-code tap trung, evaluate deny/allow theo rule.
- Phu hop cho centralized authorization trong microservices.
- Tai lieu: https://www.openpolicyagent.org/docs

### 2.6. Microsoft Entra RBAC
- Ho tro role assignment theo scope, privileged role management.
- Tai lieu: https://learn.microsoft.com/en-us/entra/identity/role-based-access-control/

## 3. Doi chieu he thong hien tai (Muonroi.BuildingBlock)

### 3.1. Diem manh hien co
- Da co 2 luong authz:
  - Enum-bitmask qua `PermissionFilter<TPermission>`
  - Dynamic permission key qua `AuthorizePermissionFilter<TDbContext>`
- Da co role, permission, user-role, role-permission va audit log.
- Da co cache da tang (`IMultiLevelCacheService`) cho user permissions.
- Da co middleware va tenant context cho multi-tenant.

### 3.2. Diem yeu / rui ro can nang cap
- Chua gate feature `advanced-auth` cho RBAC filter va auth-management endpoint.
- Chua co semantics ro rang `ALL/ANY` khi endpoint can nhieu permission.
- Co nguy co stale permission cache sau khi assign/revoke role-permission.
- Cache key permission chua duoc chuan hoa giua cac luong auth.
- Dynamic permission query chua loc day du role/user soft-delete trong 1 so path.
- Mot so nhanh co xu huong fail-open (neu service cache khong co).

## 4. Gap Matrix (baseline -> he thong hien tai)

| Nang luc | Baseline thi truong | Hien tai | Gap | Huong nang cap |
|---|---|---|---|---|
| License gating cho RBAC+ | Co feature gate ro rang | Chua day du | Cao | Gate `advanced-auth` tai filter + auth-management |
| Multi-permission semantics | Co ALL/ANY/strategy ro rang | Chua ro rang | Cao | Them `PermissionMatchMode` cho attribute + filter |
| Cache consistency | Invalidation theo event thay doi role/permission | TTL-based, chua invalidation day du | Cao | Invalidate cache ngay sau mutation |
| Tenant-consistent authz | Domain/tenant-scoped checks chat | Co nhung chua dong nhat cac path | Trung binh-Cao | Them tenant consistency check trong dynamic filter |
| Deny-by-default | Mat dinh tu choi khi phu thuoc authz loi | Co nhung con fail-open branch | Cao | Bo fail-open, fallback DB check |
| Maturity roadmap | ReBAC/ABAC/PDP tap trung | Chua co | Trung binh | Dua vao phase tiep theo (P2/P3) |

## 5. Plan nang cap chi tiet

### P0. Baseline + design lock
- [x] Hoan tat benchmark va chot gap matrix.
- [x] Chot schema va contract cho `PermissionMatchMode`.
- [x] Chot cache-key strategy thong nhat cho user-permissions.

### P1. Enforcement hardening (RBAC+ gate + deny-by-default)
- [x] Gate feature `advanced-auth` trong:
  - `PermissionFilter<TPermission>`
  - `AuthorizePermissionFilter<TDbContext>`
  - `MAuthControllerBase` cac endpoint quan tri role/permission/user authz
  - `MGenericController` khi bat `GenericCrudPermissionAttribute`
- [x] Hardening deny-by-default:
  - bo branch fail-open khi khong co cache service
  - fallback query DB an toan

### P2. Authorization semantics + tenant consistency
- [x] Them `PermissionMatchMode` (`Any`, `All`) cho:
  - `PermissionAttribute<TPermission>`
  - `AuthorizePermissionAttribute`
- [x] Cap nhat filter de evaluate dung semantics `Any/All`.
- [x] Them tenant consistency check (claim/header/context mismatch -> deny).

### P3. Cache consistency va invalidation
- [x] Chuan hoa cache key user permission (`rbac:user_permissions:*`).
- [x] Invalidate cache sau cac mutation:
  - assign role -> user
  - assign/remove permission -> role
  - delete role
- [x] Xoa key legacy de tranh stale data.

### P4. Unit tests + docs + migration note
- [x] Bo sung unit tests cho:
  - feature gate `advanced-auth`
  - semantics `Any/All`
  - tenant mismatch deny
  - cache invalidation sau mutation
  - fallback DB check (khong fail-open)
- [x] Chay test suite va fix fail.
- [x] Cap nhat tai lieu upgrade + migration note.

## 6. Tieu chi pass

- Hoan thanh tat ca hang muc P1..P4.
- Unit tests lien quan RBAC+ pass.
- Co tai lieu upgrade de team template/service co the ap dung ngay.

## 7. Roadmap sau dot nay (phase tiep theo)

- P5: ReBAC theo relation tuple (OpenFGA-like model bridge).
- P6: ABAC condition (time/location/device/risk score).
- P7: Central PDP mode (OPA/Policy service) cho microservices.
- P8: SoD va JIT elevation cho enterprise compliance.
