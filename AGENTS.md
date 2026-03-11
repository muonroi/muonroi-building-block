# Muonroi Ecosystem — Agent Working Guide

> This file describes the 4-repository open-core ecosystem: what each repo does,
> how they relate to each other, and the rules an agent must follow when working
> across them.

---

## 1. Ecosystem Overview

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
│  │  OSS packages (NuGet.org):   │  │  OSS packages (npm):                 │ │
│  │  • Core.Abstractions         │  │  • @muonroi/m-ui-engine-core         │ │
│  │  • RuleEngine.Abstractions   │  │  • @muonroi/m-ui-engine-react        │ │
│  │  • RuleEngine.Runtime        │  │  • @muonroi/m-ui-engine-angular      │ │
│  │  • RuleEngine.DecisionTable  │  │  • @muonroi/m-ui-engine-primeng      │ │
│  │  • RuleEngine.SourceGen      │  │                                      │ │
│  │  • Governance.Abstractions   │  │  Commercial packages (npm/registry): │ │
│  │  • Governance (slim)         │  │  • @muonroi/m-ui-engine-rule-comp.   │ │
│  │  • Tenancy, Messaging, ...   │  │  • @muonroi/m-ui-engine-signalr      │ │
│  │                              │  │  • @muonroi/m-ui-engine-sync         │ │
│  │  Commercial (GitHub Pkgs):   │  │                                      │ │
│  │  • Governance.Enterprise     │  │  Key component:                      │ │
│  │  • RuleEngine.Runtime.Web    │  │  • mu-decision-table (Lit Element)   │ │
│  │  • DecisionTable.Web         │  │    full FEEL editor, undo/redo,      │ │
│  │  • AspNetCore, ...           │  │    version diff, history             │ │
│  └──────────────┬───────────────┘  └──────────────────────┬───────────────┘ │
│                 │ NuGet refs                               │ npm refs        │
│                 │                                          │                 │
│  PRIVATE (internal services — not published as packages)   │                 │
│                 │                                          │                 │
│  ┌──────────────▼───────────────────────────────────────▼──────────────┐   │
│  │                  muonroi-control-plane                               │   │
│  │  (Rule Engine SaaS Control Plane — private repo)                    │   │
│  │                                                                      │   │
│  │  Backend (ASP.NET 8):                   Frontend (React+TS+SWR):    │   │
│  │  • Muonroi.ControlPlane.Api             • Dashboard (Vite)          │   │
│  │    - RuleSet CRUD + Approval            • Pages: Rules, Canary,     │   │
│  │    - Canary rollout                       Audit, DecisionTable,     │   │
│  │    - Audit trail (RSA-signed)             Tenants, Info             │   │
│  │    - Decision Table CRUD (Postgres)     • SignalR real-time updates │   │
│  │    - SignalR hub (hot-reload)           • Monaco editor (FEEL)      │   │
│  │    - FEEL autocomplete endpoint         • mu-decision-table widget  │   │
│  │    - JWT auth                                                        │   │
│  │  • Postgres: RuleEngineDb + DecisionTableDb                         │   │
│  │  • Redis: cross-node hot-reload pub/sub                             │   │
│  └──────────────────────────────┬───────────────────────────────────────┘   │
│                                 │ HTTP (license validation)                  │
│  ┌──────────────────────────────▼───────────────────────────────────────┐   │
│  │                  muonroi-license-server                              │   │
│  │  (SaaS License Server — private repo)                                │   │
│  │                                                                      │   │
│  │  • Issue / revoke license keys (MRR-{24-byte base64url})            │   │
│  │  • Generate ActivationProof (server-signed, client-verified)        │   │
│  │  • Tenant + seat quotas, expiry, feature flags                      │   │
│  │  • Postgres backend (EF Core migrations)                             │   │
│  │  • CLI admin tool (dotnet tool)                                      │   │
│  │  • REST API consumed by Governance.Enterprise at startup             │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 0. Workspace

The workspace root is the **common parent directory** of all repos. The exact absolute path differs per machine — **never hardcode it**.

**Detect workspace root at runtime:**
```shell
# Bash / Git Bash (from inside any repo):
workspace=$(dirname "$(git rev-parse --show-toplevel)")

# PowerShell (from inside any repo):
$workspace = Split-Path (git rev-parse --show-toplevel) -Parent
```

**Structure** — identical on all machines; only the drive letter or parent path differs:
```
<workspace-root>/
├── muonroi-building-block/          ← .NET library packages (OSS + Commercial)
├── muonroi-ui-engine/               ← TypeScript UI libraries (OSS + Commercial)
├── muonroi-control-plane/           ← SaaS Control Plane (private)
├── muonroi-license-server/          ← License Server (private)
├── Muonroi.BaseTemplate/            ← Dotnet base project template
├── Muonroi.Modular.Template/        ← Modular monolith template
├── Muonroi.Microservices.Template/  ← Microservices template
├── Docs/
│   └── muonroi-docs/                ← System-wide documentation (Docusaurus, branch: main)
├── GodProject/                      ← Legacy monolith (read-only reference)
├── LocalNuget/                      ← Local NuGet feed output
├── LocalNuGetFeed/                  ← Local NuGet feed (alternate)
└── _tmp/                            ← Temp/debug artifacts (never commit)
```

**Default branches:**
- `muonroi-building-block`, `muonroi-ui-engine`, `muonroi-control-plane`, `muonroi-license-server` → **`develop`**
- `Muonroi.BaseTemplate`, `Muonroi.Modular.Template`, `Muonroi.Microservices.Template`, `muonroi-docs` → **`main`**

**Docs update rule:**
> When implementing a new feature or changing existing API/behavior, you **MUST** update or add documentation in `<workspace-root>/Docs/muonroi-docs/docs/`:
> - `03-guides/` — feature guides, integration how-to
> - `05-reference/` — API and interface reference
> - `06-resources/` — CHANGELOG, migration guides

> ⚠️ Never hardcode absolute paths in plans, scripts, or agent instructions. Always derive `<workspace-root>` at runtime.

---

## 2. Repository Responsibilities

### 2.1 `muonroi-building-block` (public)

**Purpose**: Core .NET library packages — the OSS foundation plus commercial extensions.

| Layer | Package | License | Published |
|-------|---------|---------|-----------|
| Abstractions | `Muonroi.Core.Abstractions` | Apache 2.0 | NuGet.org |
| Abstractions | `Muonroi.Governance.Abstractions` | Apache 2.0 | NuGet.org |
| Rule Engine | `Muonroi.RuleEngine.Abstractions` | Apache 2.0 | NuGet.org |
| Rule Engine | `Muonroi.RuleEngine.Runtime` | Apache 2.0 | NuGet.org |
| Rule Engine | `Muonroi.RuleEngine.DecisionTable` | Apache 2.0 | NuGet.org |
| Rule Engine | `Muonroi.RuleEngine.SourceGenerators` | Apache 2.0 | NuGet.org |
| Rule Engine | `Muonroi.RuleEngine.Runtime.Web` | Commercial | GitHub Packages |
| Rule Engine | `Muonroi.RuleEngine.DecisionTable.Web` | Commercial | GitHub Packages |
| Governance | `Muonroi.Governance` (slim) | Apache 2.0 | NuGet.org |
| Governance | `Muonroi.Governance.Enterprise` | Commercial | GitHub Packages |
| Infrastructure | `Muonroi.AspNetCore`, `Muonroi.Tenancy`, etc. | Mixed | Mixed |

**Key architectural decisions**:
- `ILicenseGuardEnhancer`: OSS uses `NoopLicenseGuardEnhancer`; Enterprise overrides with `EnterpriseLicenseGuardEnhancer`
- `ILicenseFingerprintProvider`: OSS uses `DefaultLicenseFingerprintProvider` (no-op); Enterprise overrides with `FingerprintProvider`
- `LicenseGuard` never references `Governance.Enterprise` — dependency only flows upward
- Roslyn analyzers MBB001–MBB007 enforce ecosystem closure (no raw DateTime, no raw JsonSerializer, etc.)

**Key services in DecisionTable**:
- `IDecisionTableStore` → `EfCoreDecisionTableStore` (Postgres or SQL Server) or `InMemoryDecisionTableStore`
- `DecisionTableEngineOptions.PostgresConnectionString` or `.SqlServerConnectionString` triggers EF Core store
- `DecisionTableDatabaseMigrator` (hosted service) auto-migrates on startup

### 2.2 `muonroi-ui-engine` (public)

**Purpose**: TypeScript/Lit/React UI component libraries.

| Package | Description |
|---------|-------------|
| `m-ui-engine-core` | Base Lit elements, design tokens |
| `m-ui-engine-react` | React wrappers for Lit elements |
| `m-ui-engine-angular` | Angular wrappers |
| `m-ui-engine-primeng` | PrimeNG integration |
| `m-ui-engine-rule-components` | Commercial: `mu-decision-table` Lit element, FEEL editor, rule viewer |
| `m-ui-engine-signalr` | Commercial: SignalR reactive store |
| `m-ui-engine-sync` | Commercial: offline sync engine |

**`mu-decision-table`** (Lit Element in `m-ui-engine-rule-components`):
- REST calls to `/api/v1/decision-tables`
- FEEL expression autocomplete
- Version diff viewer
- Full undo/redo via Zustand store (`decision-table-store.ts`)

### 2.3 `muonroi-control-plane` (private)

**Purpose**: Deployed SaaS service — Rule Engine operator dashboard + API.

Structure:
```
muonroi-control-plane/
  src/
    Muonroi.ControlPlane.Api/          # ASP.NET 8 Minimal API
      Endpoints/                        # RuleSet, Approval, Canary, Audit,
                                        # Tenant, DecisionTable, Info
      Options/                          # Auth, ControlPlane options
      Program.cs                        # Wires all services
  apps/
    control-plane-dashboard/            # React 18 + Vite + SWR + SignalR
      src/pages/                        # 9+ pages
      src/components/                   # Shared components
  packages/                            # Short-term: mirror copies of ui-engine pkgs
  tests/
    Muonroi.ControlPlane.Tests/        # Integration tests (8/8 pass)
  scripts/
    sync-ui-packages.mjs               # Sync mirror packages from ui-engine
```

**Database**: Postgres via two DbContexts:
- `RuleEngineDbContext` — rule sets, approvals, canary, audit
- `DecisionTableDbContext` — decision tables, versions, audit logs

**Key wiring in `Program.cs`**:
```csharp
builder.Services.AddDecisionTableWeb(o => o.PostgresConnectionString = connectionString);
builder.Services.AddMRuleEngineWithPostgres(connectionString, ...);
builder.Services.AddMRuleEngineWithRedisHotReload(redisConnection); // optional
```

**Real-time**: `RuleSetChangeHub` (SignalR) + `RuleSetHubNotifier` (hosted service)
**Approval flow**: Draft → PendingApproval → Approved → Active (maker-checker)
**Canary rollout**: `CanaryRolloutService` — tenant-targeted or %-based, promote/rollback

### 2.4 `muonroi-license-server` (private)

**Purpose**: SaaS license issuance and validation server.

```
muonroi-license-server/
  src/
    Muonroi.LicenseServer/
      Auth/           # JWT + API key auth
      Cli/            # dotnet admin CLI tool
      Endpoints/      # License CRUD, activation, validation
      Infrastructure/ # Postgres EF migrations
      Services/       # LicenseIssuer, ActivationProofGenerator, QuotaEnforcer
      Storage/        # LicenseRepository (Postgres)
```

**License key format**: `MRR-{24-byte base64url}`
**ActivationProof**: server RSA-signed → client verifies offline via `LicenseVerifier`
**Consumed by**: `Governance.Enterprise.LicenseActivator` calls this at app startup

---

## 3. Cross-Repo Data Flow

```
Developer app startup:
  1. App calls AddMEnterpriseGovernance()
  2. LicenseActivator reads license key from config
  3. POST /api/v1/licenses/activate  →  muonroi-license-server
  4. Server returns ActivationProof (RSA-signed JWT)
  5. LicenseVerifier checks signature with embedded public key
  6. LicenseState.IsValid = true → features unlocked

Rule Engine hot-reload:
  1. Operator saves rule set via Dashboard → ControlPlane.Api
  2. Api persists to Postgres (RuleEngineDbContext)
  3. Api publishes to Redis channel "muonroi:ruleset:changed"
  4. All app nodes receive notification → reload from Postgres
  5. RuleSetHubNotifier pushes SignalR event to Dashboard

Decision Table flow:
  1. Operator edits table in mu-decision-table (Dashboard)
  2. PUT /api/v1/decision-tables/{id} → ControlPlane.Api
  3. EfCoreDecisionTableStore persists to Postgres
  4. Version snapshot + audit log created automatically
```

---

## 4. OSS Boundary Rules

See `OSS-BOUNDARY.md` for the full package list. Core rule:

```
OSS packages MUST NOT reference Commercial packages.
Commercial packages MAY reference OSS packages.
```

Enforced by:
- `scripts/check-modular-boundaries.ps1` (CI gate)
- Roslyn analyzers MBB001–MBB007 (compile-time)
- `IsCommercialPackage` MSBuild property selects license file

---

## 5. Coding Standards (applies to all 4 repos)

### .NET (muonroi-building-block, muonroi-control-plane, muonroi-license-server)

- **DateTime** → `IMDateTimeService` (MBB001)
- **JsonSerializer** → `IMJsonSerializeService` (MBB002) except byte-level ops
- **DbContext** → extend `MDbContext` (MBB003)
- **AsyncLocal** → only in `Core.Abstractions.Context` (MBB004)
- **Logging** → `IMLog<T>`, never `ILogger<T>` directly
- **Execution context** → `ISystemExecutionContextAccessor`, never static TenantContext reads
- **XML documentation** → every new or modified C# type/member must include XML documentation comments
- Static extension classes exempt: add `// MBBxxx-exempt: static-class boundary`

### TypeScript (muonroi-ui-engine, muonroi-control-plane/apps)

- Lit Element web components in `m-ui-engine-*` packages
- React pages in `control-plane-dashboard` import from `packages/` mirror (short-term)
- Long-term: publish `@muonroi/ui-engine-*` to npm, dashboard consumes from registry

### Source Generator (netstandard2.0)
- NO `Environment.NewLine` → use `"\n"`
- NO `ToHashSet()` → use `new HashSet<>()`
- NO `string.Replace(s, s, StringComparison)` → use 2-arg overload
- `IsExternalInit` polyfill required for record types → `Polyfills.cs`

---

## 6. Agent Working Rules

1. **Read before modifying** — never suggest changes to files you haven't read.
2. **Verify OSS boundary** before adding any project reference — run `check-modular-boundaries.ps1`.
3. **Never use raw DateTime / JsonSerializer** — use ecosystem wrappers.
4. **DecisionTable store** — always wire `PostgresConnectionString` or `SqlServerConnectionString`; never leave it on `InMemoryDecisionTableStore` in production deployments.
5. **Repo split** — code that belongs to a deployed service (control-plane, license-server) must stay in private repos; only library packages go in public repos.
6. **Commercial package guard** — `Governance.Enterprise` registration (`AddMEnterpriseGovernance`) requires valid `ActivationProof`; do not stub this out in production.
7. **Test gate** — "done" means 100% unit tests pass AND new behavior has test coverage.
8. **UI package drift** — `muonroi-control-plane/packages/` contains mirrored copies; run `sync-ui-packages.mjs` after any ui-engine change. Long-term goal: publish to npm registry.
9. **Tool priority** — MCP tools and plugins are the highest-priority execution path; use them before shell commands whenever they can solve the task.
10. **Avoid PowerShell when MCP can do it** — for scripting, data processing, file inspection, structured transformations, or small automation tasks, prefer MCP tools and especially MCP Python instead of ad-hoc PowerShell commands.
11. **C# documentation gate** — all new or changed C# code must ship with XML documentation comments as part of the implementation, not as a later cleanup pass.

---

## 7. Known Packages — Do Not Miss

### Actor Map — CRITICAL: Who Uses What

**Three distinct actors. Each uses a completely different layer. Never confuse them.**

```
ACTOR 1: End-user (người dùng cuối của app)
  App FE  →  @muonroi/m-ui-engine-angular / m-ui-engine-primeng / m-ui-engine-react
             Renders metadata-driven forms, grids, wizards for end-users.
             End-users have NO knowledge of rule engine.
             Example: nhân viên cảng dùng wizard 5 bước trong ePORT.

ACTOR 2: Developer (builds the app)
  App BE  →  Muonroi.RuleEngine.Runtime + RuleEngine.SourceGenerators
             Installs these into their own backend service.
             Fetches active rulesets from Control Plane at startup.
             Executes rules in-process on each request.
             Does NOT need Runtime.Web or DecisionTable.Web.

ACTOR 3: BA / Product Owner (authors rules)
  Browser →  Control Plane Dashboard (muonroi-control-plane React app)
             Uses Runtime.Web API to save/activate/dry-run rulesets.
             Uses DecisionTable.Web API + mu-decision-table component to author IF/THEN tables.
             Has NO direct access to developer's app.
```

**Rule of thumb**: `Runtime.Web` and `DecisionTable.Web` = **Control Plane packages**, not developer app packages.
The developer's app only needs `RuleEngine.Runtime`. The BA uses the Control Plane dashboard.

### muonroi-building-block/src/ — packages by deployment target

**Developer's own app (BE) — install these:**

| Package | Type | Purpose |
|---------|------|---------|
| `Muonroi.RuleEngine.Runtime` | OSS | Core execution — `RuleOrchestrator<T>`, rule pipeline, FactBag, hot-reload listener. This is what developer's app installs. |
| `Muonroi.RuleEngine.SourceGenerators` | OSS | `[MExtractAsRule]` → generates `IRule<TContext>` at build time. Install in developer's app. |
| `Muonroi.RuleEngine.Abstractions` | OSS | Contracts: `IRule<T>`, `IRulesEngineService`, `FactBag`, `OrchestratorResult`. |
| `Muonroi.AuthZ` | Commercial | Rule-driven authorization bridge — `IAuthorizationPolicyEvaluator`, `MuonroiAuthorizationHandler`, `IRuleRowFilter<T>`. Install in developer's app if using AuthZ. Track 7. |
| `Muonroi.Observability` | Mixed | OTel wiring for `ActivitySource("Muonroi.RuleEngine")`, metrics counters, histogram. |
| `Muonroi.Logging` / `.Abstractions` | OSS | `IMLog<T>` (auto-enriches with TenantId/UserId/CorrelationId), `IMLogContext`, `IMTraceContext`. |

**Control Plane server — these live there, NOT in developer's app:**

| Package | Type | Actor | Purpose |
|---------|------|-------|---------|
| `Muonroi.RuleEngine.Runtime.Web` | Commercial | **BA + Developer** | HTTP API for ruleset CRUD/activate/dry-run (`/api/v1/rule-engine/rulesets`). SignalR hub for hot-reload push. `RuntimeRuleSetManifestContributor` declares screens/actions in dashboard. **Lives in Control Plane, not developer's app.** |
| `Muonroi.RuleEngine.DecisionTable.Web` | Commercial | **BA** | HTTP API for Decision Table CRUD/execute/import (`/api/v1/decision-tables`). Wires `mu-decision-table` Lit component into dashboard via `DecisionTableManifestContributor`. **Lives in Control Plane, not developer's app.** |
| `Muonroi.UiEngine.Catalog` | Commercial | **Developer/Architect** | Introspection — scans deployed `IRule<T>` + API endpoints, builds dependency graph. 7 REST endpoints (`/api/v1/ui-engine/catalog/*`). Per-tenant snapshot store. `[BindRuleContext]` attribute. **Lives in Control Plane.** |

**UI Manifest Contributor pattern** (IUiEngineManifestContributor):
- Both `Runtime.Web` and `DecisionTable.Web` implement this
- They self-register their screens, actions, data sources into the dashboard at startup
- `DecisionTableManifestContributor` order=100 (module: `decision-table`, tier: `Professional`)
- `RuntimeRuleSetManifestContributor` order=140 (module: `runtime-ruleset`, tier: `Starter`)
- Adding a new commercial package = its screens appear in dashboard automatically

### muonroi-control-plane/src/ — key projects agents often overlook

| Project | Purpose |
|---------|---------|
| `Muonroi.ControlPlane.Mcp` | **MCP Server** — 42+ MCP tools for AI agents to manage rules, approvals, canary, tenants, audit, decision tables, FEEL autocomplete. Resources (`muonroi://rulesets/*`, `muonroi://tenants/*`, etc.). Prompts for analyze/compliance/dry-run workflows. Uses `ModelContextProtocol.AspNetCore`. Fully implemented. |
| `Muonroi.ControlPlane.Api` | Main API. Check `Program.cs` to see which endpoints are actually mapped — some library endpoints (e.g. `MapRuleTracingEndpoints`) exist in the package but may not be wired yet. |

### muonroi-ui-engine — FE packages by actor

| Package | Actor | Purpose |
|---------|-------|---------|
| `@muonroi/m-ui-engine-core` | End-user | Metadata-driven base components (forms, grids, validation) |
| `@muonroi/m-ui-engine-angular` | End-user | Angular 20 bindings for UI Engine components |
| `@muonroi/m-ui-engine-primeng` | End-user | PrimeNG-based UI Engine components |
| `@muonroi/m-ui-engine-react` | End-user | React bindings |
| `@muonroi/m-ui-engine-rule-comp` | **BA** | Rule Studio components (flow graph, palette, FEEL editor) — used in Control Plane dashboard |
| `mu-decision-table` (Lit) | **BA** | Decision Table editor web component — used in Control Plane dashboard |

**Do NOT confuse**: `m-ui-engine-angular` (end-user app) vs `m-ui-engine-rule-comp` (BA in dashboard).

### Production consumer — ePORT

Real-world usage of the rule engine in production:
- Backend: `D:/sources/TCIS.ePORT/tcis.eport.fullcontainerdelivery.aggregate.services/src/v2/Applications/Commands/Create/`
- Frontend: `D:/sources/TEP/tcis.eport.web/` (Angular 20 + PrimeNG — **end-user** facing, not BA)
- Uses: `[MExtractAsRule]`, `FactBag`, `IRulesEngineService`, `CreateRuleContext`, `IWorkContextAccessor`
- 10 compiled C# rules (FCD_V2_TAX_VALID through FCD_V2_BOOKING_ELIGIBLE)
- ePORT frontend uses `@muonroi/m-ui-engine-angular` / `m-ui-engine-primeng` (Actor 1 — end-user layer)

---

## 8. Source Code Exploration Guide

**Follow this order to avoid missing important context.**

### Step 1 — Read context files first (before touching any code)

```
1. CLAUDE.md (root workspace)         — rules, coding standards, track status
2. ~/.claude/projects/.../MEMORY.md   — accumulated session knowledge
3. Docs/MainMap.txt                   — competitive analysis, known gaps
4. Docs/implement_*.txt               — plan files for current/planned tracks
```

### Step 2 — Discover package structure

For .NET (muonroi-building-block):
```
Glob: src/**/*.csproj
  → Note package name, <IsCommercialPackage> flag (Commercial vs OSS)
  → Note <TargetFramework> (net8.0 vs netstandard2.0 for source generators)
  → Read <PackageReference> to understand dependencies
```

For TypeScript (muonroi-ui-engine):
```
Glob: packages/*/package.json
  → Note name, version, dependencies
```

For Control Plane:
```
src/Muonroi.ControlPlane.Api/Program.cs  ← what's actually wired at runtime
src/Muonroi.ControlPlane.Mcp/           ← MCP tools available to AI agents
apps/control-plane-dashboard/src/pages/ ← existing UI pages
```

### Step 3 — Understand a new package (reading order)

```
1. *.csproj                     → dependencies, tier, target framework
2. *Abstractions package first  → interfaces, records, enums (contracts)
3. *Extensions.cs               → DI registration, what gets wired
4. Core service/adapter files   → implementation
5. tests/                       → expected behavior, edge cases
```

### Step 4 — Cross-reference search

When you find an interface/method, always check:
```
Grep across ALL repos (building-block + control-plane + ui-engine + ePORT)
  → Who implements it?
  → Who consumes it?
  → Is it wired in any Program.cs?

Example: found MapRuleTracingEndpoints
  → Grep all repos → found in RuleTracingEndpoints.cs (defines)
     AND RuleEngineRuntimeEndpointExtensions.cs (calls it)
     BUT control-plane Program.cs does NOT call MapRuleEngineRuntimeWeb()
  → Conclusion: endpoints exist in library but not exposed in running service
```

### Step 5 — Common traps to avoid

```
TRAP 1: GodProject/ = legacy copy of old building-block
  → Always confirm you are reading from muonroi-building-block/src/
    not GodProject/Hello/MuonroiBuildingBlock/src/
  → When grep returns hits from both → ignore GodProject hits

TRAP 2: Package exists ≠ endpoint is wired
  → Muonroi.RuleEngine.Runtime.Web has MapRuleTracingEndpoints()
  → But control-plane Program.cs must call MapRuleEngineRuntimeWeb() to expose it
  → Always check Program.cs wiring before assuming feature is live

TRAP 3: UiEngine.Catalog ≠ Rule Catalog for BA authoring
  → Muonroi.UiEngine.Catalog = introspection (scans deployed rules, builds graph)
  → "Rule Catalog" in plan = authoring-time BA palette (typed I/O schema)
  → They are different; the plan catalog should BUILD ON UiEngine.Catalog

TRAP 4: Binary files in grep results
  → grep across workspace includes .dll, .db files — skip them
  → Only act on .cs, .ts, .json, .csproj text file hits

TRAP 5: Control Plane has Mcp project
  → Muonroi.ControlPlane.Mcp is a full MCP Server (42+ tools)
  → If implementing anything rule-management-related, check if MCP tool already exists
  → MCP tools: muonroi_ruleset_*, muonroi_approval_*, muonroi_canary_*,
    muonroi_tenant_*, muonroi_audit_*, muonroi_decision_table_*, muonroi_feel_*

TRAP 6: Runtime.Web and DecisionTable.Web are NOT for the developer's app
  → These two packages live in the Control Plane server, not in the developer's backend
  → Developer's app installs: RuleEngine.Runtime + SourceGenerators only
  → Runtime.Web = BA uses dashboard to manage rulesets via HTTP API
  → DecisionTable.Web = BA uses dashboard to author IF/THEN decision tables
  → If asked to "add rule engine to developer's app" → do NOT reference these packages

TRAP 7: UI Engine packages have two separate actor layers
  → @muonroi/m-ui-engine-angular / primeng / react = END-USER layer (install in developer's app FE)
  → @muonroi/m-ui-engine-rule-comp / mu-decision-table = BA layer (only in Control Plane dashboard)
  → Never install rule-comp or mu-decision-table into an end-user facing Angular/React app
```

### Step 6 — Check OrchestratorResult and trace data

When analyzing rule execution results, look for:
```
OrchestratorResult (returned from ExecuteWithResultAsync):
  .IsSuccess
  .RuleResults["RULE_CODE"].IsPass   ← per-rule verdict
  .Facts                             ← output FactBag
  .Errors                            ← human-readable errors

IRuleExecutionTracer trace entries:
  .InputFactsJson  ← FactBag snapshot BEFORE rule ran
  .OutputFactsJson ← FactBag snapshot AFTER rule ran
  .ChangedFactKeys ← diff
  .ElapsedMs, .Phase, .IsSuccess
```

### Step 7 — Plan files location

All implementation plans stored in `Docs/`:
```
implement_rule_studio_ux_fix_plan.txt   — Phase 1-4 (Publish fix, Condition facts, Branch semantics)
implement_rule_runtime_deep_plan.txt    — Phase A-F (Expression compiler, Catalog, UI palette)
implement_track7_authz_plan.txt         — Rule-driven Authorization
MainMap.txt                             — Competitive analysis, unique features, gaps
```

---

## 9. Track Status (as of 2026-03-11)

| Track | Description | Status |
|-------|-------------|--------|
| Track 0 | License boundary fix + Governance split | ✅ Done |
| Track 1 | OSS NuGet CI/CD + VitePress docs + VSIX | ✅ Done |
| Track 2 | Production License Server | ✅ Done |
| Track 3 | Rule Control Plane API + Dashboard + Repo split | ✅ Done |
| Track 4 | Decision Table Postgres store + FEEL backend + npm publish | 🔄 In progress |

### Track 4 Remaining Items

1. **✅ DecisionTable Postgres gap closed** — `Program.cs` now passes `PostgresConnectionString` to `AddDecisionTableWeb()`; `EfCoreDecisionTableStore` activated.
2. **FEEL autocomplete backend** — frontend wired, backend endpoint needs implementation.
3. **Decision Table version diff UI** — `mu-decision-table` component has the diff viewer; wire to `/api/v1/decision-tables/{id}/versions/{v}` endpoint.
4. **npm publish pipeline** — CI/CD to publish `@muonroi/ui-engine-*` packages so dashboard can consume from registry instead of local mirror.
5. **Developer templates** — `dotnet new muonroi-*` templates with `--tier` and `--control-plane` options.
6. **Community samples** — quickstart sample projects for rule engine and decision table.
