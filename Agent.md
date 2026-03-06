# Agent Working Standard

This file defines the unified working rules for Muonroi repositories.

## Scope

- `MuonroiBuildingBlock` (core library)
- `Muonroi.BaseTemplate`
- `Muonroi.Modular.Template`
- `Muonroi.Microservices.Template`
- `Muonroi.Docs` (documentation hub)
- `Muonroi.Ui.Engine` (hybrid UI runtime repo)

## Workspace Layout

- Keep project root (`D:\Personal\Project`) clean. Only these top-level folders are allowed:
1. Repositories: `MuonroiBuildingBlock`, `Muonroi.BaseTemplate`, `Muonroi.Modular.Template`, `Muonroi.Microservices.Template`, `Muonroi.Docs`
2. Local package feeds: `LocalNuget`, `LocalNuGetFeed`
3. Temporary workspace: `_tmp`
- Never create ad-hoc folders at root for debug/verify.
- Generated verification projects must be placed under:
1. `D:\Personal\Project\_tmp\verify-runs\<run-id>`
- Template snapshots/backups must be placed under:
1. `D:\Personal\Project\_tmp\template-snapshots\<snapshot-id>`

## Debug Artifact Convention

- Debug scripts must be stored in:
1. `D:\Personal\Project\_tmp\scripts\debug`
- Runtime logs and captured outputs must be stored in:
1. `D:\Personal\Project\_tmp\logs\<task-id>`
- Intermediate debug results (json/txt/csv) must be stored in:
1. `D:\Personal\Project\_tmp\results\<task-id>`
- Forbidden locations for debug artifacts:
1. Project root (`D:\Personal\Project`)
2. Repository root of any source repo
3. Template root folders
- File naming convention:
1. Scripts: `<task>_<yyyyMMdd_HHmmss>.ps1`
2. Logs: `<task>.out.log`, `<task>.err.log`
3. Data: `<task>.json` / `<task>.txt`
- Cleanup rule:
1. After finishing a task, move useful evidence to `_tmp\results\<task-id>` and remove redundant debug files.

## Core Rules

- No quick workaround. Always do research first, then plan, then implement.
- Done means:
1. Plan is completed.
2. Unit tests pass 100%.
3. New test cases are added for each upgrade behavior.
- Developer-facing API naming must use `M` prefix for Muonroi branding:
1. Classes: `MRepository`, `MQuery`, ...
2. Extension classes/method groups: `M...Extensions`
3. Helper/service abstractions for external developer use.
4. Frontend runtime exports/functions/types should use `M...` prefix.
- Exceptions to `M` prefix:
1. Framework-mandated types (`Program`, ASP.NET handlers, EF migration classes).
2. Third-party contracts/interfaces that must keep original names.

## Git Rules

- Commit by logical scope, per repository.
- Do not rewrite shared history unless explicitly requested.
- Default branches:
1. `MuonroiBuildingBlock`: `develop`
2. Templates/Docs/UI-Engine: `main`

## Version Bump And Local Package Flow

All steps are local-only (no publish to public NuGet).

1. Bump library version and update template package refs:

```powershell
cd D:\Personal\Project\MuonroiBuildingBlock
.\scripts\bump-version.ps1 -Version 1.9.11
```

2. Local package outputs:
1. `D:\Personal\Project\LocalNuget`
2. `D:\Personal\Project\LocalNuGetFeed`

3. Bump template package versions (`.csproj` and `.nuspec`) to same version.

4. Pack each template to local feed:

```powershell
cd D:\Personal\Project\Muonroi.BaseTemplate
dotnet pack .\Muonroi.BaseTemplate.csproj -c Release -o D:\Personal\Project\LocalNuget

cd D:\Personal\Project\Muonroi.Modular.Template
dotnet pack .\Muonroi.Modular.csproj -c Release -o D:\Personal\Project\LocalNuget

cd D:\Personal\Project\Muonroi.Microservices.Template
dotnet pack .\Muonroi.Microservices.csproj -c Release -o D:\Personal\Project\LocalNuget
```

5. Reinstall local templates:

```powershell
dotnet new install D:\Personal\Project\LocalNuget\Muonroi.BaseTemplate.1.9.11.nupkg --force
dotnet new install D:\Personal\Project\LocalNuget\Muonroi.Modular.Template.1.9.11.nupkg --force
dotnet new install D:\Personal\Project\LocalNuget\Muonroi.Microservices.Template.1.9.11.nupkg --force
```

## Generate New Projects And Verify

For each generated project:

1. Create from template (`dotnet new ...`)
2. Run EF scripts:

```powershell
cd <generated-project>
.\scripts\ef.cmd init
.\scripts\ef.cmd update
dotnet restore
dotnet run
```

## License Keys And Tier Setup (Free/Paid/Enterprise)

- Detailed agent runbook:
1. `AGENT_LICENSE_KEY_RUNBOOK.md`

1. Generate master/child key assets:

```powershell
cd D:\Personal\Project\MuonroiBuildingBlock
.\scripts\flow-license-server.ps1 -Organization "Muonroi Local Verify" -NoRunServer
```

2. Run full runtime verification for `Free/Paid/Enterprise` modes on all 3 templates:

```powershell
cd D:\Personal\Project\MuonroiBuildingBlock
.\scripts\flow-license-modes.ps1 -Organization "Muonroi Local Verify"
```

3. Optional run mock server:

```powershell
cd D:\Personal\Project\MuonroiBuildingBlock\tools\MockLicenseServer
dotnet run --project .\MockLicenseServer.csproj
```

4. Use signed local licenses per generated app:
1. `licenses\paid-license.json`
2. `licenses\enterprise-license.json`
3. `licenses\control-plane-public.pem`

5. Configure app (`appsettings` or env vars):
1. `LicenseConfigs:Mode=Offline`
2. `LicenseConfigs:LicenseFilePath=<license-json>`
3. `LicenseConfigs:PublicKeyPath=<public-key-pem>`

## Tier Verification Matrix

1. `Free`:
1. Register/Login/CRUD must still work.
2. Premium endpoints must be blocked.
2. `Paid`:
1. Login returns token.
2. Premium endpoints (for paid scope) return success.
3. `Enterprise`:
1. Login returns token.
2. Enterprise endpoints/features enabled by config and license.

## Runtime Verification Requirements

- Verify by tests and runtime logs:
1. `dotnet test` green.
2. Log contains `[License] Verified tier: ...`
3. API flow `register -> login -> GET /api/v1/Auth/verify-token` succeeds with bearer token header.
4. Login response contains `result.accessToken`.

## Docs Rule

- All new developer/user-facing documents must be added in `Muonroi.Docs` (not `MuonroiBuildingBlock/docs`).
- Suggested locations in `Muonroi.Docs`:
1. `docs/03-guides/*` for feature guides and API references.
2. `docs/04-operations/*` for deployment/runbook/troubleshooting.
3. `docs/06-resources/buildingblock/*` only for mirrored source markdown.
- Keep `MuonroiBuildingBlock/docs` for legacy/internal artifacts only unless explicitly required.
- Template README files must reference `Muonroi.Docs` as source of truth.

---

## Ecosystem Coding Rules (Wrapper-First)

The Muonroi ecosystem enforces a closed-loop model: every internal package depends on Muonroi abstractions, not on raw framework primitives. Violating these rules triggers Roslyn analyzers (MBB001–MBB007) that fail the build.

### 1. DateTime — Always Use `IMDateTimeService`

**Forbidden:**
```csharp
DateTime.UtcNow   // MBB001 violation
DateTime.Now      // MBB001 violation
```

**Required:**
```csharp
// Inject:
private readonly IMDateTimeService _dateTimeService;

// Use:
DateTime utcNow = _dateTimeService.UtcNow();
DateTime now    = _dateTimeService.Now();
```

**Interface** (`Muonroi.Core.Abstractions.Interfaces.IMDateTimeService`):
- `DateTime Now()` — local time
- `DateTime UtcNow()` — UTC time (prefer this)
- `DateTime Today()` / `DateTime UtcToday()`
- `double NowTs()` / `double UtcNowTs()` — Unix timestamps

**Exempt (add inline comment `// MBB001-exempt: ...`):**
- `MDateTimeService.cs` — IS the wrapper implementation
- `LocalClockProvider.cs`, `UtcClockProvider.cs` — ARE clock implementations
- Static extension method classes where DI is impossible — add `// MBB001-exempt: static-class boundary`

### 2. JSON — Always Use `IMJsonSerializeService`

**Forbidden:**
```csharp
JsonSerializer.Serialize(obj)         // MBB002 violation
JsonSerializer.Deserialize<T>(text)   // MBB002 violation
```

**Required:**
```csharp
// Inject:
private readonly IMJsonSerializeService _jsonService;

// Use:
string json = _jsonService.Serialize(obj);
T? result   = _jsonService.Deserialize<T>(json);
```

**Interface** (`Muonroi.Core.Abstractions.Interfaces.IMJsonSerializeService`):
- `string Serialize<T>(T obj)`
- `T? Deserialize<T>(string text)`

**Exempt (add inline comment `// MBB002-exempt: ...`):**
- `MJsonSerializeService.cs` — IS the wrapper implementation
- `JsonSerializer.SerializeToUtf8Bytes()` / `Deserialize<T>(byte[])` — byte-level operations NOT in wrapper — add `// MBB002-exempt: byte-level operation not in wrapper`
- Static classes where DI is impossible — add `// MBB002-exempt: static-class boundary`

### 3. Logging — Always Use `IMLog<T>` (Not `ILogger<T>` Directly)

**Preferred (ecosystem-native):**
```csharp
// Inject:
private readonly IMLog<MyService> _log;

// Use shortcut methods:
_log.Info("Rule {@Rule} fired in {Ms}ms", rule, elapsed);
_log.Warn("Quota warning for tenant {TenantId}", tenantId);
_log.Error(ex, "Operation {Op} failed", op);
_log.Debug("State: {@State}", state);

// Or push a property scope:
using IMLogContextScope scope = _log.BeginProperty("TenantId", tenantId);
```

**Why IMLog<T>:**
- Bridges `ILogger<T>` (zero migration — same backend, same Serilog/OTel/Console providers)
- `{@Object}` destructuring works because Serilog is the backend
- `Info/Warn/Error/Debug` shortcuts are ecosystem-native, shorter than `LogInformation(...)`
- `BeginProperty()` uses `ILogger.BeginScope()` — compatible with all providers that implement `ISupportExternalScope`

**MBB007:** Forbidden to call `Serilog.Context.LogContext.PushProperty()` directly outside `Muonroi.Logging.*` / `Muonroi.Observability.*` — use `IMLogContext.PushProperty()` instead.

### 4. Context Propagation — Always Use `ISystemExecutionContextAccessor`

**Never use:**
```csharp
TenantContext.CurrentTenantId      // legacy static — read-only OK, never write
UserContext.CurrentUserGuid        // legacy static — read-only OK, never write
```

**Required for all new code:**
```csharp
// Inject:
private readonly ISystemExecutionContextAccessor _contextAccessor;

// Use:
ISystemExecutionContext ctx = _contextAccessor.Get();
string? tenantId      = ctx.TenantId;
Guid    userId        = ctx.UserId;
string? correlationId = ctx.CorrelationId;
string? sourceType    = ctx.SourceType;
```

**Setting context at transport boundaries:**
```csharp
// Create a scope that restores previous context on Dispose:
using SystemExecutionContextScope scope = SystemExecutionContextScope.Push(new SystemExecutionContext
{
    TenantId      = resolvedTenantId,
    UserId        = resolvedUserId,
    CorrelationId = correlationId,
    SourceType    = "HTTP"
});
// Optionally mirror to legacy static + push logging scope:
using ContextMirrorScope mirror = ContextMirrorScope.Apply(scope.Context, logScopeFactory);
```

**Transport boundaries that already do this (do NOT add again):**
- `JwtMiddleware` (HTTP)
- `GrpcServerInterceptor` (gRPC)
- `AmqpContextConsumeFilter`, `TenantContextConsumeFilter` (Kafka/MassTransit)
- `JobContextActivatorFilter` (Hangfire)
- `QuartzContextJobListener` (Quartz)

### 5. DbContext — Always Inherit from `MDbContext`

**Forbidden:**
```csharp
public class MyDbContext : DbContext { }   // MBB003 violation
```

**Required:**
```csharp
public class MyDbContext : MDbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options, IMediator mediator,
        ILicenseGuard? licenseGuard = null, ILogger<MyDbContext>? logger = null,
        IMDateTimeService? dateTimeService = null)
        : base(options, mediator, licenseGuard, logger, dateTimeService) { }
}
```

**Benefits:** auto-audit timestamps, soft-delete, multi-tenant query filters, domain event dispatch.

### 6. Repository — Always Inherit from `MRepository<T>`

```csharp
public class MyRepository(MyDbContext db, IAuthenticateInfoContext auth, ILicenseGuard guard, IMDateTimeService dt)
    : MRepository<MyEntity>(db, auth, guard, dt), IMyRepository { }
```

### 7. Tier Enforcement — Always Guard Enterprise/Licensed Features

```csharp
// At DI registration time (Program.cs / extension method):
services.EnsureFeatureOrThrow(LicenseTier.Enterprise, "feature.name");

// Or at runtime:
_licenseGuard.EnsureValid("feature.action", context: entityName);
```

**Tier ladder:** `Free (0)` < `Licensed (1)` < `Enterprise (2)`

Features defined in `FeatureTierMap`. Never register enterprise-only services in Free tier.

### 8. Rule Engine — Use `IRuleExecutionTracer` for Flight Recording

**Never log rule execution to `IMLog<T>` directly** — use the dedicated tracer:

```csharp
// Injected by RuleOrchestrator automatically when registered:
private readonly IRuleExecutionTracer? _tracer;
private readonly ISystemExecutionContextAccessor? _contextAccessor;

// Tracer is no-op when IsEnabled returns false (O(1) Redis EXISTS check):
if (_tracer?.IsEnabled(ctx.TenantId) ?? false)
{
    await _tracer.TraceAsync(new RuleTraceEntry
    {
        Phase      = RuleTracePhase.AfterExecution,
        TenantId   = ctx.TenantId,
        RuleCode   = rule.Code,
        InputFacts = factBag.Snapshot(),
        ...
    }, ct);
}
```

**Toggle per-tenant via API:**
```
POST /api/rule-debug/{tenantId}/enable
POST /api/rule-debug/{tenantId}/disable
GET  /api/rule-debug/{tenantId}/traces
```

### 9. AsyncLocal — Only in `Muonroi.Core.Abstractions.Context`

**MBB004:** `AsyncLocal<T>` usage outside `Muonroi.Core.Abstractions.Context` namespace fails the build.
All concurrency-safe ambient state must be centralized in:
- `SystemExecutionContextHolder` (the single AsyncLocal)
- `SystemExecutionContextScope` (nested-safe push/pop)

### 10. Abstractions Must Not Reference Infrastructure

**MBB005:** `*.Abstractions` packages must contain only contracts (interfaces, records, enums, exceptions).
Forbidden dependencies in abstractions:
- `Microsoft.EntityFrameworkCore`
- `Hangfire.*`
- `Quartz.*`
- `Serilog.*`
- `MassTransit.*`

Move any infrastructure type to the corresponding adapter package (e.g. `Muonroi.Data.EntityFrameworkCore`).

---

## How to Add a New Feature — Step-by-Step

1. **Define contracts in `*.Abstractions`**: interfaces, request/response records, domain events.
2. **Implement in the feature package** — never in abstractions.
3. **Inject via DI**: use primary constructor syntax, inject `IMDateTimeService`, `IMJsonSerializeService`, `IMLog<T>`, `ISystemExecutionContextAccessor` as needed.
4. **Register with tier guard** if the feature is Licensed/Enterprise:
   ```csharp
   services.EnsureFeatureOrThrow(LicenseTier.Licensed, "my.feature");
   services.AddSingleton<IMyFeature, MyFeature>();
   ```
5. **Never use static ambient state** — all context flows through `ISystemExecutionContextAccessor`.
6. **Context at message/job boundaries**: wrap execution in `SystemExecutionContextScope.Push(...)` + `ContextMirrorScope.Apply(...)`.
7. **Tests**: write unit tests per rule/service. Use `SystemExecutionContextScope.Push(...)` in tests to set up context.

---

## Roslyn Analyzer Reference

| Code   | Rule                                                        | Severity |
|--------|-------------------------------------------------------------|----------|
| MBB001 | Forbidden `DateTime.Now/UtcNow` — use `IMDateTimeService`  | Error    |
| MBB002 | Forbidden `JsonSerializer.*` — use `IMJsonSerializeService` | Error    |
| MBB003 | Forbidden `DbContext` inheritance — use `MDbContext`        | Error    |
| MBB004 | Forbidden `AsyncLocal` outside Core.Abstractions.Context   | Error    |
| MBB005 | Abstractions package must not reference infrastructure      | Error    |
| MBB006 | Missing tier guard on infrastructure registration           | Warning  |
| MBB007 | Forbidden `LogContext.PushProperty` — use `IMLogContext`   | Error    |

**To suppress a legitimate exemption**, add an inline comment (NOT `#pragma warning disable`):
```csharp
// MBB001-exempt: static-class boundary — cannot inject IMDateTimeService
// MBB002-exempt: byte-level operation not in wrapper
```

These comment patterns are recognized by the analyzer suppressor and must include the reason.
