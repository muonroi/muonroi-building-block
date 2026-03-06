# Multi-Tenancy Upgrade Plan (Licensed + Enterprise)

## 1. Muc tieu

Nang cap multi-tenancy theo huong `isolation-by-default`, `license-gated`, va dong nhat hanh vi giua HTTP/gRPC/SignalR/JWT.

## 2. Tieu chi pass

- Hoan thanh tat ca hang muc trong plan ben duoi.
- Unit tests lien quan phai pass.
- Co tai lieu huong dan thay doi de project tich hop cap nhat.

## 3. Plan chi tiet va trang thai

### P1. Core data isolation
- [x] Gop query filter tenant + creator trong `MDbContext` (tranh bi ghi de).
- [x] Duy tri tenant index cho entity co `TenantId`.

### P2. Default-deny tenant access (Paid tiers)
- [x] `MGenericController` chuyen sang fail-safe khi multi-tenant bat ma thieu tenant context.
- [x] `SetTenantId` bat buoc tenant context khi multi-tenant bat.

### P3. License gating cho multi-tenant
- [x] Gate feature `multi-tenant` trong controller.
- [x] Gate feature `multi-tenant` trong `TenantContextMiddleware` (tra `403` neu khong co feature).
- [x] Gate feature `multi-tenant` trong `TenantHubFilter`.

### P4. Tenant resolution va consistency
- [x] `DefaultTenantIdResolver` bo sung path resolution.
- [x] `TenantContextMiddleware` bo sung check consistency claim/header va tra `401` cho protected endpoint khi thieu tenant.
- [x] Lam sach `TenantContext` (bo fallback global) de tranh ro tenant qua async flow.

### P5. JWT tenant-key validation path
- [x] Add JWT `IssuerSigningKeyResolver` theo `kid` + `SigningKeysByTenant`.
- [x] Refresh-token flow (`AuthorizeInternal`) cung validate theo `kid`.
- [x] `JwtMiddleware` propagate tenant claim vao `HttpContext.User`.
- [x] `AuthorizeInternal.GenerateAccessToken` include `TenantContext.CurrentTenantId` vao token model.

### P6. gRPC tenant hardening
- [x] `GrpcServerInterceptor` enforce tenant bat buoc khi `MultiTenantEnabled=true`.
- [x] Check mismatch tenant giua claim va metadata header.

### P7. Template wiring update
- [x] Template pipeline bo goi trung `LicenseMiddleware`.
- [x] Chuyen `TenantContextMiddleware` sau `JwtMiddleware` de validate claim/header dung thu tu.
- [x] Dong bo cho 3 templates:
  - `Muonroi.Base.Template`
  - `Muonroi.Modular.Template.Clean`
  - `Muonroi.Microservices.Template.Clean`

## 4. File thay doi chinh

- `src/Muonroi.BuildingBlock/External/Entity/MDbContext.cs`
- `src/Muonroi.BuildingBlock/External/Controller/MGenericController.cs`
- `src/Muonroi.BuildingBlock/External/Tenant/TenantContext.cs`
- `src/Muonroi.BuildingBlock/External/Tenant/DefaultTenantIdResolver.cs`
- `src/Muonroi.BuildingBlock/External/Tenant/TenantContextMiddleware.cs`
- `src/Muonroi.BuildingBlock/External/Tenant/TenantServiceCollectionExtensions.cs`
- `src/Muonroi.BuildingBlock/External/InfrastructureExtensions.cs`
- `src/Muonroi.BuildingBlock/Internal/Infrastructure/Authorize/AuthorizeInternal.cs`
- `src/Muonroi.BuildingBlock/External/Middleware/JwtMiddleware.cs`
- `src/Muonroi.BuildingBlock/External/Grpc/GrpcServerInterceptor.cs`
- `src/Muonroi.BuildingBlock/External/Grpc/GrpcHandler.cs`
- `src/Muonroi.BuildingBlock/External/SignalR/TenantHubFilter.cs`
- `src/Muonroi.BuildingBlock/External/Logging/TenantIdEnricher.cs`
- `tests/Muonroi.BuildingBlock.Test/MGenericControllerTenantIsolationTests.cs`
- `tests/Muonroi.BuildingBlock.Test/TenantContextMiddlewareTests.cs`
- `tests/Muonroi.BuildingBlock.Test/GrpcServerInterceptorTests.cs`
- `tests/Muonroi.BuildingBlock.Test/MDbContextTests.cs`
- `tests/Muonroi.BuildingBlock.Test/MAuthenticateTokenHelperTests.cs`
- `tests/Muonroi.BuildingBlock.Test/DefaultTenantIdResolverTests.cs`
- `tests/Muonroi.BuildingBlock.Test/JwtMiddlewareTests.cs`
- `D:/Personal/Project/Muonroi.Base.Template/src/Muonroi.BaseTemplate.API/Infrastructures/StartupExtensions.cs`
- `D:/Personal/Project/Muonroi.Modular.Template.Clean/src/Host/Muonroi.Modular.Host/Infrastructures/StartupExtensions.cs`
- `D:/Personal/Project/Muonroi.Microservices.Template.Clean/src/Services/Muonroi.Microservices.Catalog/Infrastructures/StartupExtensions.cs`

## 5. Test evidence

Da chay:

```bash
dotnet test tests/Muonroi.BuildingBlock.Test/Muonroi.BuildingBlock.Test.csproj -v minimal
dotnet test tests/Tenancy/Muonroi.Tenancy.Tests.csproj -v minimal
```

Ket qua:
- `Muonroi.BuildingBlock.Test`: Passed `1403/1403`
- `Muonroi.Tenancy.Tests`: Passed `4/4`

Ghi chu:
- `tests/MultiTenancy/MultiTenancy.csproj` hien dang reference project khong ton tai (`src/Infra/Infra.csproj`), nen khong the dung de lam gate CI.
- `tests/Muonroi.BuildingBlock.IntegrationTests` hien fail do test host path setup (`DirectoryNotFoundException`) khong lien quan truc tiep den code thay doi.

## 6. Huong dan migration cho project su dung library

1. Bat multi-tenant:
   - `MultiTenantConfigs:Enabled = true`
   - `TokenConfigs:MultiTenantEnabled = true`
2. Dam bao paid license co feature `multi-tenant`.
3. Dam bao request protected co tenant context hop le (header/claim phai consistent).
4. Neu dung per-tenant signing key:
   - set `TokenConfigs:SigningKeysByTenant`
   - dam bao JWT co `kid` tuong ung tenant.
5. Neu dung template:
   - cap nhat `StartupExtensions.cs` theo thay doi pipeline o P7.
