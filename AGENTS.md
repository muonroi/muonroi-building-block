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

---

## 7. Track Status (as of 2026-03-06)

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
