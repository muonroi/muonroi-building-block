# Muonroi.AspNetCore

> ASP.NET Core integration layer for the Muonroi Building Block: versioned API hosting, JWT bearer auth, license enforcement, CORS, causal-chain propagation, and startup diagnostics — wired up with a handful of extension calls.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.AspNetCore.svg)](https://www.nuget.org/packages/Muonroi.AspNetCore/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

`Muonroi.AspNetCore` is the hosting glue that connects the Muonroi core packages (`Muonroi.Core`, `Muonroi.Tenancy.*`, `Muonroi.Data.EntityFrameworkCore`, `Muonroi.Mediator`, `Muonroi.Governance`, and others) to an ASP.NET Core application. It provides opinionated DI registration helpers, a standardized middleware pipeline, attribute-based permission enforcement, and a pluggable startup diagnostics system that blocks misconfigured apps before they accept traffic.

## Installation

```bash
dotnet add package Muonroi.AspNetCore --prerelease
```

## Quick Start

The minimal setup for a versioned, documented API:

```csharp
using Muonroi.AspNetCore.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Registers API versioning (default v1.0), SwaggerGen, and health checks.
builder.Services.AddBaseApi();

builder.Services.AddControllers();

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.Run();
```

For a full infrastructure setup (JWT, EF, tenancy, CORS, mediator, validation):

```csharp
using Muonroi.AspNetCore.Extensions;
using Muonroi.AspNetCore.Cors;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Wires API versioning, controllers, FluentValidation, license protection,
// multi-level caching, tenant context, quota management, and policy decisions.
// Requires LicenseConfigs:ProjectSeed (16+ chars) in appsettings.
builder.Services.AddInfrastructure(
    builder.Configuration,
    assemblies: typeof(Program).Assembly);

// CORS policy read from "MAllowDomains" config key (comma-separated origins).
builder.Services.AddCors(builder.Configuration);

// JWT bearer authentication (HMAC or RSA, driven by TokenConfigs in appsettings).
builder.Services.AddValidateBearerToken(builder.Configuration);

// Auth + permission services (requires a DbContext that extends MDbContext).
builder.Services.AddAuthServices<MyDbContext, MyPermissionEnum>();

// Autofac service provider (mediator + auth context modules).
builder.AddAutofacConfiguration();

// Startup diagnostics: blocks boot on critical misconfiguration.
builder.Services.AddMuonroiDiagnostics();

WebApplication app = builder.Build();

// License enforcement, exception handling, cookie auth, quota enforcement.
app.UseDefaultMiddleware();
app.UseMuonroiDiagnostics();
app.ConfigureEndpoints();   // Swagger, MapControllers, health endpoints

app.Run();
```

## Features

- **`AddBaseApi()`** — API versioning (Asp.Versioning, default v1.0 with `ReportApiVersions`), `SwaggerGen`, and health checks in one call.
- **`AddInfrastructure()`** — full-stack registration: controllers with `GlobalExceptionFilter` + `RequestLoggingFilter`, `LowerCaseControllerNameConvention`, `FluentValidation`, license protection, multi-level caching, tenant context, quota management, and policy-decision wiring.
- **`AddValidateBearerToken()`** — JWT bearer auth supporting HMAC (`SymmetricSecretKey`) or RSA (`UseRsa=true` + PEM key); per-tenant signing keys; lazy `IssuerSigningKeyResolver`.
- **`AddAuthServices<TDbContext, TPermission>()`** — registers `IAuthService<TPermission, TDbContext>` and `IPermissionService<TPermission>`.
- **`AddPermissionFilter<TPermission>()`** — global MVC `PermissionFilter<TPermission>` for attribute-driven access control.
- **`UseDefaultMiddleware()`** — pipeline order: `TenantContextMiddleware` (when tenancy enabled) → `QuotaEnforcementMiddleware` → `LicenseMiddleware` → `MExceptionMiddleware` → `MCookieAuthMiddleware`.
- **`ConfigureEndpoints()`** — configures Swagger UI, `MapControllers`, `/health`, `/health/live`, `/health/ready`, `/grpc/ready`, `/grpc/live`, and a root redirect to `/swagger`.
- **`AddCors()`** — CORS policy from configuration (`MAllowDomains` config key), with the standard Muonroi header and method set.
- **`AddMuonroiCausalChain()`** + **`MCausalChainDelegatingHandler`** — propagates correlation/causation headers across outbound `HttpClient` calls.
- **`AddMuonroiDiagnostics()` / `UseMuonroiDiagnostics()`** — 6 built-in startup checks (dependency graph, configuration, connectivity, ecosystem registry, license status, tenant config); Critical failures throw and block startup.
- **`AddApplication()`** — lightweight alternative: mediator, object mapper, `IMDateTimeService`, `ISystemExecutionContextAccessor`, camelCase JSON.
- **`SwaggerConfig()`** — Swagger doc with Bearer security definition pre-configured.
- **`AddAutofacConfiguration()`** — plugs Autofac as the service provider factory and registers `MediatorModule` + `AuthContextModule`.
- **`[AuthorizePermissionAttribute]`** / **`[PermissionAttribute<TPermission>]`** / **`[GenericCrudPermissionAttribute]`** — declarative permission annotations for controllers and actions.
- **`[FeatureFlagAttribute]`** — action filter that reads a boolean feature flag from `IConfiguration` and returns 404 when the flag is off.

## Configuration

### Minimum required — `appsettings.json`

```json
{
  "LicenseConfigs": {
    "ProjectSeed": "your-unique-16-char-seed-here"
  }
}
```

`AddInfrastructure` throws `MConfigurationException` at startup when `ProjectSeed` is missing or shorter than 16 characters.

### JWT bearer — `TokenConfigs`

```json
{
  "TokenConfigs": {
    "Issuer": "https://your-issuer",
    "Audience": "your-audience",
    "UseRsa": false,
    "SymmetricSecretKey": "your-hmac-secret-key"
  }
}
```

Set `UseRsa: true` and supply `PrivateKey` (inline PEM) or `PrivateKeyPath` (file path) to switch to RSA signing.

### CORS — `MAllowDomains`

```json
{
  "MAllowDomains": "https://app.example.com,https://staging.example.com"
}
```

### Causal chain

```csharp
services.AddMuonroiCausalChain(options =>
{
    options.ServiceName = "my-service";
});

// Attach to an HttpClient:
services.AddHttpClient<IMyServiceClient, MyServiceClient>()
        .AddHttpMessageHandler<MCausalChainDelegatingHandler>();
```

## API Reference

| Type | Purpose |
|------|---------|
| `ServiceCollectionExtensions.AddBaseApi()` | API versioning + Swagger + health checks |
| `InfrastructureExtensions.AddInfrastructure()` | Full-stack DI registration |
| `InfrastructureExtensions.AddValidateBearerToken()` | JWT bearer auth (HMAC or RSA) |
| `InfrastructureExtensions.AddAuthServices<TDbContext, TPermission>()` | Auth + permission service registration |
| `InfrastructureExtensions.AddPermissionFilter<TPermission>()` | Global permission MVC filter |
| `InfrastructureExtensions.UseDefaultMiddleware()` | Standard middleware pipeline |
| `InfrastructureExtensions.ConfigureEndpoints()` | Swagger UI, controllers, health routes |
| `InfrastructureExtensions.AddAutofacConfiguration()` | Autofac DI factory + mediator/auth modules |
| `ApplicationExtensions.AddApplication()` | Lightweight hosting without full infrastructure |
| `ApplicationExtensions.SwaggerConfig()` | Swagger doc with Bearer security definition |
| `CorsExtensions.AddCors()` | CORS policy from configuration |
| `CausalChainExtensions.AddMuonroiCausalChain()` | Outbound causal-chain header propagation |
| `DiagnosticsExtensions.AddMuonroiDiagnostics()` | Register 6 built-in startup diagnostics |
| `DiagnosticsExtensions.UseMuonroiDiagnostics()` | Run diagnostics; block on Critical failures |
| `IMEcosystemDiagnostic` | Interface for custom startup diagnostics |
| `IAuthService<TPermission, TDbContext>` | Authentication service contract |
| `IPermissionService<TPermission>` | Permission query contract |
| `ILogSanitizer` | Log sanitization contract |
| `MCausalChainDelegatingHandler` | `DelegatingHandler` that injects correlation headers |
| `GlobalExceptionFilter` | MVC exception filter with environment-aware responses |
| `LicenseMiddleware` | Blocks requests when license is invalid |
| `QuotaEnforcementMiddleware` | Enforces per-tenant request quotas |
| `AuthorizePermissionAttribute` | Permission-based action authorization |
| `PermissionAttribute<TPermission>` | Enum-typed permission annotation |
| `FeatureFlagAttribute` | Configuration-driven feature gate |

## Samples

- [Quickstart.AspNetCore](../../samples/Quickstart.AspNetCore/) — minimal `AddBaseApi()` setup: API versioning, Swagger, health checks.
- [Quickstart.AspNetCore.RuleEngine](../../samples/Quickstart.AspNetCore.RuleEngine/) — `AddBaseApi()` combined with the Rule Engine, demonstrating a rule-order controller wired into the Muonroi hosting layer.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Core`](../Muonroi.Core/) — core services (`IMDateTimeService`, `IMLog`, `MException`) used throughout this package
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — shared contracts and guards
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — tenant context and middleware registered by `AddInfrastructure`
- [`Muonroi.Data.EntityFrameworkCore`](../Muonroi.Data.EntityFrameworkCore/) — `MDbContext` required by `AddAuthServices`
- [`Muonroi.Mediator`](../Muonroi.Mediator/) — mediator registered by `AddApplication` and `AddInfrastructure`
- [`Muonroi.Governance`](../Muonroi.Governance/) — policy-decision services wired by `AddInfrastructure`
- [`Muonroi.AspNetCore.OpenApi`](../Muonroi.AspNetCore.OpenApi/) — OpenAPI operation filters used by `SwaggerConfig`

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for the full text.
