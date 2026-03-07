# Agent Working Standard

This file defines the unified working rules for all Muonroi repositories.

## Communication Rule

- **Reply to the user in Vietnamese.**
- **Write plan files, code comments, and documentation in English.**

---

## Scope — 4-Repo Ecosystem

| Repo | Visibility | Purpose |
|------|-----------|---------|
| `muonroi-building-block` | Public | .NET library packages (OSS + Commercial NuGet) |
| `muonroi-ui-engine` | Public | TypeScript UI component libraries (OSS + Commercial npm) |
| `muonroi-control-plane` | Private | Rule Engine SaaS API + operator dashboard (deployed service) |
| `muonroi-license-server` | Private | License issuance and activation server (deployed service) |

Legacy repos still active:
- `Muonroi.BaseTemplate`, `Muonroi.Modular.Template`, `Muonroi.Microservices.Template`
- `Muonroi.Docs`

---

## Ecosystem Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        MUONROI OPEN-CORE ECOSYSTEM                          │
│                                                                             │
│  PUBLIC (Apache 2.0 OSS + Commercial dual-license)                          │
│                                                                             │
│  ┌──────────────────────────────┐  ┌──────────────────────────────────────┐ │
│  │  muonroi-building-block      │  │  muonroi-ui-engine                   │ │
│  │  (.NET library ecosystem)    │  │  (TypeScript UI library ecosystem)   │ │
│  │                              │  │                                      │ │
│  │  OSS → NuGet.org:            │  │  OSS → npm:                          │ │
│  │  • Core.Abstractions         │  │  • @muonroi/m-ui-engine-core         │ │
│  │  • RuleEngine.Abstractions   │  │  • @muonroi/m-ui-engine-react        │ │
│  │  • RuleEngine.Runtime        │  │  • @muonroi/m-ui-engine-angular      │ │
│  │  • RuleEngine.DecisionTable  │  │  • @muonroi/m-ui-engine-primeng      │ │
│  │  • RuleEngine.SourceGen      │  │                                      │ │
│  │  • Governance.Abstractions   │  │  Commercial → npm registry:          │ │
│  │  • Governance (slim)         │  │  • @muonroi/m-ui-engine-rule-comp.   │ │
│  │  • Tenancy, Messaging, ...   │  │  • @muonroi/m-ui-engine-signalr      │ │
│  │                              │  │  • @muonroi/m-ui-engine-sync         │ │
│  │  Commercial → GitHub Pkgs:   │  │                                      │ │
│  │  • Governance.Enterprise     │  │  Key: mu-decision-table (Lit)        │ │
│  │  • RuleEngine.Runtime.Web    │  │    FEEL editor, undo/redo,           │ │
│  │  • DecisionTable.Web         │  │    version diff, history             │ │
│  │  • AspNetCore, ...           │  │                                      │ │
│  └──────────────┬───────────────┘  └──────────────────────┬───────────────┘ │
│                 │ NuGet refs                               │ npm refs        │
│  PRIVATE (deployed services)                               │                 │
│  ┌──────────────▼──────────────────────────────────────▼──────────────────┐ │
│  │                    muonroi-control-plane                                │ │
│  │  Backend (ASP.NET 8):               Frontend (React+TS+SWR):           │ │
│  │  • RuleSet CRUD + Approval          • Dashboard (Vite)                 │ │
│  │  • Canary rollout                   • Pages: Rules, Canary, Audit,     │ │
│  │  • Audit trail (RSA-signed)           DecisionTable, Tenants, Info     │ │
│  │  • Decision Table CRUD (Postgres)   • SignalR real-time                │ │
│  │  • SignalR hub (hot-reload)         • Monaco editor (FEEL)             │ │
│  │  • JWT auth                         • mu-decision-table widget         │ │
│  │  • Postgres + Redis                                                     │ │
│  └──────────────────────────────┬───────────────────────────────────────────┘ │
│                                 │ HTTP (activation)                           │
│  ┌──────────────────────────────▼───────────────────────────────────────────┐ │
│  │                    muonroi-license-server                                │ │
│  │  • Issue / revoke license keys  MRR-{24-byte base64url}                 │ │
│  │  • Generate ActivationProof (RSA-signed, offline-verifiable)             │ │
│  │  • Tenant + seat quotas, expiry, feature flags                           │ │
│  │  • Postgres + EF Core migrations                                         │ │
│  └──────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Workspace Layout

Root: **the common parent directory of all repos** — differs per machine, never hardcode it.

**Detect workspace root at runtime:**
```shell
# Bash / Git Bash:
workspace=$(dirname "$(git rev-parse --show-toplevel)")
# PowerShell:
$workspace = Split-Path (git rev-parse --show-toplevel) -Parent
```

Allowed top-level folders under `<workspace-root>/`:
1. Repos: `muonroi-building-block`, `muonroi-ui-engine`, `muonroi-control-plane`, `muonroi-license-server`, `Muonroi.BaseTemplate`, `Muonroi.Modular.Template`, `Muonroi.Microservices.Template`
2. Docs: `Docs/muonroi-docs/`
3. Local package feeds: `LocalNuget`, `LocalNuGetFeed`
4. Temporary workspace: `_tmp`
5. Legacy/snapshot: `GodProject`

Rules:
- Never create ad-hoc folders at root for debug/verify.
- Generated verification projects → `<workspace-root>/_tmp/verify-runs/<run-id>`
- Template snapshots → `<workspace-root>/_tmp/template-snapshots/<snapshot-id>`

---

## Debug Artifact Convention

- Debug scripts → `<workspace-root>/_tmp/scripts/debug`
- Runtime logs → `<workspace-root>/_tmp/logs/<task-id>`
- Intermediate results (json/txt/csv) → `<workspace-root>/_tmp/results/<task-id>`
- Forbidden artifact locations: repo roots, project root, template folders
- File naming: scripts `<task>_<yyyyMMdd_HHmmss>.ps1`; logs `<task>.out.log`
- Cleanup: after task, move evidence to `_tmp/results/<task-id>`, remove debug files.

---

## Core Rules

- No quick workaround. Research first → plan → implement.
- **Done means:**
  1. Plan completed.
  2. Unit tests pass 100%.
  3. New test cases added for each upgraded behavior.
- Developer-facing API naming must use `M` prefix:
  1. Classes: `MRepository`, `MQuery`, ...
  2. Extension classes/method groups: `M...Extensions`
  3. Helper/service abstractions for external developer use.
  4. Frontend runtime exports/functions/types: `M...` prefix.
- Exceptions to `M` prefix:
  1. Framework-mandated types (`Program`, ASP.NET handlers, EF migration classes).
  2. Third-party contracts/interfaces that must keep original names.

---

## OSS Boundary Rules

See `muonroi-building-block/OSS-BOUNDARY.md` for the full package list.

**Core rule**: OSS packages MUST NOT reference Commercial packages.
Commercial packages MAY reference OSS packages.

Enforced by:
- `muonroi-building-block/scripts/check-modular-boundaries.ps1` (CI gate)
- Roslyn analyzers MBB001–MBB007 (compile-time)
- `IsCommercialPackage` MSBuild property selects license file

---

## Git Rules

- Commit by logical scope, per repository.
- Do not rewrite shared history unless explicitly requested.
- Default working branch for all 4 ecosystem repos: **`develop`**
  1. `muonroi-building-block`: `develop`
  2. `muonroi-ui-engine`: `develop`
  3. `muonroi-control-plane`: `develop`
  4. `muonroi-license-server`: `develop`
- `main` = stable/release only — never commit directly to `main`
- Templates / Docs: `main`

---

## Version Bump And Local Package Flow

All steps are local-only (no publish to public NuGet).

1. Bump library version:

```powershell
# Detect workspace root first:
$root = Split-Path (git rev-parse --show-toplevel) -Parent

cd "$root\muonroi-building-block"
.\scripts\bump-version.ps1 -Version 1.9.11
```

2. Local package outputs:
   - `<workspace-root>/LocalNuget`
   - `<workspace-root>/LocalNuGetFeed`

3. Bump template package versions (`.csproj` and `.nuspec`) to same version.

4. Pack each template to local feed:

```powershell
$root = Split-Path (git rev-parse --show-toplevel) -Parent

cd "$root\Muonroi.BaseTemplate"
dotnet pack .\Muonroi.BaseTemplate.csproj -c Release -o "$root\LocalNuget"

cd "$root\Muonroi.Modular.Template"
dotnet pack .\Muonroi.Modular.csproj -c Release -o "$root\LocalNuget"

cd "$root\Muonroi.Microservices.Template"
dotnet pack .\Muonroi.Microservices.csproj -c Release -o "$root\LocalNuget"
```

5. Reinstall local templates:

```powershell
$root = Split-Path (git rev-parse --show-toplevel) -Parent

dotnet new install "$root\LocalNuget\Muonroi.BaseTemplate.1.9.11.nupkg" --force
dotnet new install "$root\LocalNuget\Muonroi.Modular.Template.1.9.11.nupkg" --force
dotnet new install "$root\LocalNuget\Muonroi.Microservices.Template.1.9.11.nupkg" --force
```

---

## Generate New Projects And Verify

1. Create from template (`dotnet new ...`)
2. Run EF scripts:

```powershell
cd <generated-project>
.\scripts\ef.cmd init
.\scripts\ef.cmd update
dotnet restore
dotnet run
```

---

## License Keys And Tier Setup (Free/Paid/Enterprise)

1. Generate master/child key assets:

```powershell
$root = Split-Path (git rev-parse --show-toplevel) -Parent
cd "$root\muonroi-building-block"
.\scripts\flow-license-server.ps1 -Organization "Muonroi Local Verify" -NoRunServer
```

2. Run full runtime verification for `Free/Paid/Enterprise` modes:

```powershell
$root = Split-Path (git rev-parse --show-toplevel) -Parent
cd "$root\muonroi-building-block"
.\scripts\flow-license-modes.ps1 -Organization "Muonroi Local Verify"
```

3. Configure app (`appsettings` or env vars):
   - `LicenseConfigs:Mode=Offline`
   - `LicenseConfigs:LicenseFilePath=<license-json>`
   - `LicenseConfigs:PublicKeyPath=<public-key-pem>`

---

## Tier Verification Matrix

- `Free`: Register/Login/CRUD must work. Premium endpoints must be blocked.
- `Paid`: Login returns token. Paid-scope endpoints return success.
- `Enterprise`: Login returns token. Enterprise features enabled by license.

## Runtime Verification Requirements

1. `dotnet test` green.
2. Log contains `[License] Verified tier: ...`
3. API flow `register → login → GET /api/v1/Auth/verify-token` succeeds with bearer token.
4. Login response contains `result.accessToken`.

---

## Docs Rule

- All new developer/user-facing documents in `Muonroi.Docs` (not `muonroi-building-block/docs`).
- Locations in `Muonroi.Docs`:
  1. `docs/03-guides/*` — feature guides and API references.
  2. `docs/04-operations/*` — deployment/runbook/troubleshooting.
  3. `docs/05-reference/*` — API/interface/package reference.
  4. `docs/06-resources/*` — CHANGELOG, CONTRIBUTING, SECURITY, samples.
- Do NOT create `.md` files inside `src/`, `scripts/`, or `tools/` directories in any of the 4 repos, except:
  - `README.md` at package root (npm/NuGet package description — keep in repo)
  - `CLAUDE.md`, `AGENTS.md` (agent/workflow instructions — keep in repo)
  - `AnalyzerReleases.*.md` (Roslyn convention — keep in repo)
  - All other documentation goes to `Muonroi.Docs`.
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
private readonly IMDateTimeService _dateTimeService;

DateTime utcNow = _dateTimeService.UtcNow();
DateTime now    = _dateTimeService.Now();
```

**Interface** (`Muonroi.Core.Abstractions.Interfaces.IMDateTimeService`):
- `DateTime Now()` / `DateTime UtcNow()` / `DateTime Today()` / `DateTime UtcToday()`
- `double NowTs()` / `double UtcNowTs()` — Unix timestamps

**Exempt**: `MDateTimeService.cs`, clock providers, static-class boundaries → add `// MBB001-exempt: reason`

### 2. JSON — Always Use `IMJsonSerializeService`

**Forbidden:**
```csharp
JsonSerializer.Serialize(obj)        // MBB002
JsonSerializer.Deserialize<T>(text)  // MBB002
```

**Required:**
```csharp
private readonly IMJsonSerializeService _jsonService;

string json = _jsonService.Serialize(obj);
T? result   = _jsonService.Deserialize<T>(json);
```

**Exempt**: `MJsonSerializeService.cs`, byte-level ops (`SerializeToUtf8Bytes`/`Deserialize<T>(byte[])`), static-class boundaries → add `// MBB002-exempt: reason`

### 3. Logging — Always Use `IMLog<T>`

```csharp
private readonly IMLog<MyService> _log;

_log.Info("Rule {@Rule} fired in {Ms}ms", rule, elapsed);
_log.Warn("Quota warning for tenant {TenantId}", tenantId);
_log.Error(ex, "Operation {Op} failed", op);
_log.Debug("State: {@State}", state);

using IMLogContextScope scope = _log.BeginProperty("TenantId", tenantId);
```

**MBB007**: Never call `Serilog.Context.LogContext.PushProperty()` directly — use `IMLogContext.PushProperty()`.

### 4. Context Propagation — Always Use `ISystemExecutionContextAccessor`

**Never write to** `TenantContext.CurrentTenantId` / `UserContext.CurrentUserGuid` static statics in new code.

**Required:**
```csharp
private readonly ISystemExecutionContextAccessor _contextAccessor;

ISystemExecutionContext ctx = _contextAccessor.Get();
string? tenantId      = ctx.TenantId;
Guid    userId        = ctx.UserId;
string? correlationId = ctx.CorrelationId;
```

**At transport boundaries:**
```csharp
using SystemExecutionContextScope scope = SystemExecutionContextScope.Push(new SystemExecutionContext
{
    TenantId = resolvedTenantId, UserId = resolvedUserId, CorrelationId = correlationId
});
using ContextMirrorScope mirror = ContextMirrorScope.Apply(scope.Context, logScopeFactory);
```

Transport boundaries that already handle this (do NOT add again): `JwtMiddleware`, `GrpcServerInterceptor`, `AmqpContextConsumeFilter`, `TenantContextConsumeFilter`, `JobContextActivatorFilter`, `QuartzContextJobListener`.

### 5. DbContext — Always Inherit from `MDbContext`

```csharp
public class MyDbContext : MDbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options, IMediator mediator,
        ILicenseGuard? licenseGuard = null, ILogger<MyDbContext>? logger = null,
        IMDateTimeService? dateTimeService = null)
        : base(options, mediator, licenseGuard, logger, dateTimeService) { }
}
```

### 6. Repository — Always Inherit from `MRepository<T>`

```csharp
public class MyRepository(MyDbContext db, IAuthenticateInfoContext auth, ILicenseGuard guard, IMDateTimeService dt)
    : MRepository<MyEntity>(db, auth, guard, dt), IMyRepository { }
```

### 7. Tier Enforcement — Always Guard Enterprise/Licensed Features

```csharp
services.EnsureFeatureOrThrow(LicenseTier.Enterprise, "feature.name");
_licenseGuard.EnsureValid("feature.action", context: entityName);
```

Tier ladder: `Free (0)` < `Licensed (1)` < `Enterprise (2)`. Never register enterprise-only services in Free tier.

### 8. Rule Engine — Use `IRuleExecutionTracer` for Flight Recording

```csharp
if (_tracer?.IsEnabled(ctx.TenantId) ?? false)
{
    await _tracer.TraceAsync(new RuleTraceEntry
    {
        Phase = RuleTracePhase.AfterExecution,
        TenantId = ctx.TenantId,
        RuleCode = rule.Code,
        InputFacts = factBag.Snapshot(),
    }, ct);
}
```

### 9. AsyncLocal — Only in `Muonroi.Core.Abstractions.Context`

**MBB004**: `AsyncLocal<T>` outside `Muonroi.Core.Abstractions.Context` namespace fails the build.
Use: `SystemExecutionContextHolder` (the single AsyncLocal) + `SystemExecutionContextScope`.

### 10. Abstractions Must Not Reference Infrastructure

**MBB005**: `*.Abstractions` packages must contain only contracts (interfaces, records, enums, exceptions).
Forbidden in abstractions: `EntityFrameworkCore`, `Hangfire.*`, `Quartz.*`, `Serilog.*`, `MassTransit.*`.

### 11. DecisionTable Store — Always Wire a Persistent Store in Production

```csharp
// ControlPlane wiring (Program.cs):
builder.Services.AddDecisionTableWeb(o => o.PostgresConnectionString = connectionString);
// or for SQL Server:
builder.Services.AddDecisionTableWeb(o => o.SqlServerConnectionString = connectionString);
```

Never leave `DecisionTableEngineOptions` without a connection string in production — it falls back to `InMemoryDecisionTableStore` which loses data on restart.

---

## How to Add a New Feature — Step-by-Step

1. **Define contracts in `*.Abstractions`**: interfaces, request/response records, domain events.
2. **Implement in the feature package** — never in abstractions.
3. **Inject via DI**: use primary constructor syntax, inject wrappers (`IMDateTimeService`, `IMJsonSerializeService`, `IMLog<T>`, `ISystemExecutionContextAccessor`).
4. **Register with tier guard** if Licensed/Enterprise:
   ```csharp
   services.EnsureFeatureOrThrow(LicenseTier.Licensed, "my.feature");
   services.AddSingleton<IMyFeature, MyFeature>();
   ```
5. **Never use static ambient state** — all context flows through `ISystemExecutionContextAccessor`.
6. **Tests**: write unit tests per rule/service. Use `SystemExecutionContextScope.Push(...)` in tests to set up context.

---

## Roslyn Analyzer Reference

| Code   | Rule                                                         | Severity |
|--------|--------------------------------------------------------------|----------|
| MBB001 | Forbidden `DateTime.Now/UtcNow` — use `IMDateTimeService`   | Error    |
| MBB002 | Forbidden `JsonSerializer.*` — use `IMJsonSerializeService`  | Error    |
| MBB003 | Forbidden `DbContext` inheritance — use `MDbContext`         | Error    |
| MBB004 | Forbidden `AsyncLocal` outside Core.Abstractions.Context    | Error    |
| MBB005 | Abstractions package must not reference infrastructure       | Error    |
| MBB006 | Missing tier guard on infrastructure registration            | Warning  |
| MBB007 | Forbidden `LogContext.PushProperty` — use `IMLogContext`    | Error    |

**To suppress a legitimate exemption**, add an inline comment (NOT `#pragma warning disable`):
```csharp
// MBB001-exempt: static-class boundary — cannot inject IMDateTimeService
// MBB002-exempt: byte-level operation not in wrapper
```

---

## Track Status (as of 2026-03-06)

| Track | Description | Status |
|-------|-------------|--------|
| Track 0 | License boundary fix + Governance split | ✅ Done |
| Track 1 | OSS NuGet CI/CD + VitePress docs + VSIX | ✅ Done |
| Track 2 | Production License Server | ✅ Done |
| Track 3 | Rule Control Plane API + Dashboard + Repo split | ✅ Done |
| Track 4 | FEEL backend + npm publish + templates + samples | 🔄 In progress |

### Track 4 Remaining Items

1. **✅ DecisionTable Postgres gap** — `Program.cs` now passes `PostgresConnectionString` to `AddDecisionTableWeb()`; `EfCoreDecisionTableStore` activated.
2. **FEEL autocomplete backend** — frontend wired, backend endpoint needs implementation.
3. **Decision Table version diff** — `mu-decision-table` has the viewer; wire to version API.
4. **npm publish pipeline** — CI/CD for `@muonroi/ui-engine-*` packages; eliminate local mirror in control-plane.
5. **Developer templates** — `dotnet new muonroi-*` with `--tier` and `--control-plane` options.
6. **Community samples** — quickstart projects for rule engine and decision table.

---

## Source Generator Rules (netstandard2.0)

- NO `Environment.NewLine` → use `"\n"`
- NO `ToHashSet()` → use `new HashSet<>(collection)`
- NO `string.Replace(s, s, StringComparison)` → use 2-arg overload
- `IsExternalInit` polyfill required for record types → `Polyfills.cs`
- `EnforceExtendedAnalyzerRules=true` in csproj is required
