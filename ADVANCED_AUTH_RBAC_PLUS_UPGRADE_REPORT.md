# Advanced Auth (RBAC+) Upgrade Report

## 1. Scope da thuc hien

Dot nay da implement cac hang muc P1..P4 trong plan:
- License gate `advanced-auth`
- Authorization semantics `Any/All`
- Tenant consistency hardening
- Cache key standardization + invalidation
- Unit tests + test evidence

## 2. Nang cap chinh da ap dung

### 2.1. License gating cho RBAC+
- Gate `advanced-auth` trong:
  - `PermissionFilter<TPermission>`
  - `AuthorizePermissionFilter<TDbContext>`
  - `MAuthControllerBase` cac endpoint quan tri role/permission/user authz
  - `PermissionService<TPermission, TDbContext>` (service layer)
  - `MGenericController` khi dung `GenericCrudPermissionAttribute`

### 2.2. Permission semantics Any/All
- Them `PermissionMatchMode`:
  - `Any`
  - `All`
- Mo rong attribute:
  - `PermissionAttribute<TPermission>(..., PermissionMatchMode)`
  - `AuthorizePermissionAttribute(..., PermissionMatchMode)`
- Filter da evaluate theo quy tac:
  - Tat ca permission `All` phai dung
  - Nhom `Any` (neu co) phai trung it nhat 1

### 2.3. Tenant consistency hardening
- Dynamic permission filter deny khi `tenant_id` claim khac `TenantContext.CurrentTenantId`.

### 2.4. Cache consistency + key strategy
- Chuan hoa key qua `RbacCacheKeys`:
  - `rbac:user_permissions:entity:{guid}`
  - `rbac:user_permissions:id:{long}`
- Backward compatibility:
  - Co fallback doc key legacy `user_permissions:{id}`
- Invalidation sau mutation:
  - assign role -> user
  - assign/remove permission -> role
  - delete role
  - delete user

### 2.5. Deny-by-default hardening
- `MGenericController.CheckPermissionAsync` bo fail-open khi cache service khong co.
- Fallback query DB an toan thay vi cho phep mac dinh.
- Dynamic permission query bo sung loc user/role soft-delete.

## 3. File da thay doi (chinh)

- `ADVANCED_AUTH_RBAC_PLUS_UPGRADE_PLAN.md`
- `src/Muonroi.BuildingBlock/External/Common/Enums/PermissionMatchMode.cs`
- `src/Muonroi.BuildingBlock/External/Common/RbacCacheKeys.cs`
- `src/Muonroi.BuildingBlock/External/Attributes/PermissionAttribute.cs`
- `src/Muonroi.BuildingBlock/External/Attributes/AuthorizePermissionAttribute.cs`
- `src/Muonroi.BuildingBlock/External/Controller/ActionFilters/PermissionFilter.cs`
- `src/Muonroi.BuildingBlock/External/Controller/ActionFilters/AuthorizePermissionFilter.cs`
- `src/Muonroi.BuildingBlock/External/Controller/MGenericController.cs`
- `src/Muonroi.BuildingBlock/External/Controller/MAuthControllerBase.cs`
- `src/Muonroi.BuildingBlock/Internal/Infrastructure/Authorize/AuthorizeInternal.cs`
- `src/Muonroi.BuildingBlock/Internal/Services/PermissionService.cs`
- `tests/Muonroi.BuildingBlock.Test/PermissionAttributesTests.cs`
- `tests/Muonroi.BuildingBlock.Test/PermissionFilterTests.cs`
- `tests/Muonroi.BuildingBlock.Test/AuthorizePermissionFilterTests.cs`
- `tests/Muonroi.BuildingBlock.Test/PermissionServiceRbacPlusTests.cs`
- `tests/Muonroi.BuildingBlock.Test/MGenericControllerPermissionTests.cs`

## 4. Test evidence

Da chay:

```bash
dotnet test tests/Muonroi.BuildingBlock.Test/Muonroi.BuildingBlock.Test.csproj -v minimal
```

Ket qua:
- Passed `1416/1416`
- Failed `0`

## 5. Ghi chu migration

- Neu endpoint dang dung nhieu `PermissionAttribute`/`AuthorizePermissionAttribute`, can chot ro `Any` hay `All`.
- He thong da uu tien key cache moi `rbac:user_permissions:*`; key cu van duoc fallback/deletion trong qua trinh chuyen doi.
- Cac endpoint RBAC+/permission management se yeu cau license co feature `advanced-auth`.
