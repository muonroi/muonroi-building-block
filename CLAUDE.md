# Agent Working Standard

This file defines the unified working rules for all Muonroi repositories.

---

# Tool Priority Rule (HIGHEST PRIORITY)

ALWAYS prefer MCP tools and available plugins over shell commands.

Shell/Bash commands are fallback only when no MCP tool or plugin can accomplish the task.

Available MCP servers:

- context7 — fetch up-to-date library documentation
- filesystem — structured file operations
- playwright — browser automation and UI verification
- vector-memory (Qdrant) — long-term project memory

Apply immediately in every session.

---

# Context Control Rule

Avoid filling the conversation with unnecessary content.

Prefer:

- targeted file reads
- code search
- MCP filesystem queries
- summarization

Avoid:

- loading entire directories
- dumping large files into chat
- long raw logs
- repeated screenshots

When large data is required, summarize it before continuing.

---

# Session Start Rule

At the start of every session, read `SESSION_START.md` for orientation.

Before working in any repo, **always read that repo's `REPO_DEEP_MAP.md` first**.

Deep maps contain every key file path, class name, method signature, and connection.
Do NOT explore/glob/grep a repo if its deep map already has the information you need.

| Repo | Deep Map |
|------|----------|
| muonroi-building-block | `muonroi-building-block/REPO_DEEP_MAP.md` |
| muonroi-ui-engine | `muonroi-ui-engine/REPO_DEEP_MAP.md` |
| muonroi-control-plane | `muonroi-control-plane/REPO_DEEP_MAP.md` |
| muonroi-license-server | `muonroi-license-server/REPO_DEEP_MAP.md` |
| muonroi-docs | `Docs/muonroi-docs/REPO_DEEP_MAP.md` |

Other workspace-level maps:

- `REPO_MAP.md` — ecosystem overview
- `RULE_ENGINE_MAP.md` — rule engine deep architecture
- `DOCS_WORKFLOW.md` — when and where to write docs
- `PLAN_WORKFLOW.md` — how to create and manage plans

---

# Deep Map Maintenance Rule

When you **add, rename, or delete** a key file (endpoint, service, component, model):

- Update that repo's `REPO_DEEP_MAP.md` to reflect the change.
- This keeps future sessions accurate without re-exploration.

---

# Vector Memory Rule

vector-memory is the **long-term memory system** with verified deep architecture knowledge.

## Retrieval Strategy (3-tier fallback)

1. **Vector memory** — `qdrant-find "{topic}"` — fastest, contains verified implementation-level details
2. **Deep maps** — `REPO_DEEP_MAP.md` — file-level mapping if vector memory doesn't have enough detail
3. **Codebase exploration** — direct code reading via Glob/Grep/Read — only when tiers 1-2 insufficient

## Available Deep Architecture Entries (verified 2026-03-16)

| Query Pattern | Content |
|---------------|---------|
| `rule engine architecture` | Pipeline, FactBag, flow graph, execution modes, adapters |
| `RuleGen source generator` | CLI commands, Roslyn services, writers, analyzers MBB001-007 |
| `multi-tenancy tenant` | AsyncLocal propagation, ContextMirrorScope, EF filters, quota enforcement |
| `auth governance license` | HMAC chain, anti-tamper, fail-closed matrix, heartbeat, auth rules hot-reload |
| `UI engine` | Runtime resolution, bootstrap lifecycle, Zustand stores, Lit patterns, license gating |
| `control plane` | API endpoints, MCP tools, dashboard pages, SignalR hubs |
| `rule engine deep` | DFS topo sort, Kahn's algorithm, 2-phase execution, compensation LIFO |
| `workflow deep` | State machine, execution router, 3-level cache, canary, hot-reload |
| `multi-tenancy deep` | Data isolation strategies, quota cache keys, security validation chain |
| `auth license deep` | License enforcement pipeline, HMAC key derivation, anti-tamper mechanisms |
| `UI engine deep` | Manifest schema, ETag caching, virtualized rendering, Lit+React hybrid |

Before starting work:

- search vector-memory for relevant architecture or prior decisions.

Example:

qdrant-find "rule engine architecture"

After completing meaningful work:

store a concise summary containing:

Decision:
What was implemented or changed

Files modified:
List of relevant files

Reasoning:
Why this approach was chosen

---

# Memory Quality Rule

When storing memory:

DO:

- summarize knowledge
- store architecture decisions
- store reusable patterns
- keep memory under ~200 words

DO NOT:

- store raw conversation
- store temporary debugging notes
- store trivial context

Good memory examples:

- architecture summary
- design decision
- bug root cause
- reusable implementation pattern

---

# Playwright Lightweight Rule

Playwright can quickly consume context if misused.

Use Playwright only for **targeted verification**.

Prefer:

- locator checks
- role/text assertions
- visibility assertions
- navigation steps

Avoid:

- repeated screenshots
- full DOM dumps
- long UI sessions

Recommended workflow:

1. implement code
2. verify UI behavior
3. take **one final screenshot if needed**

If UI work is extensive:

Use a **separate Claude session** dedicated to Playwright testing.

---

# Communication Rule

Reply to the user in Vietnamese.

Write:

- code comments
- plan files
- documentation

in English.

---

# Architecture Summary (High Priority Context)

Muonroi ecosystem uses a **rule-engine-centric architecture**.

## Knowledge Retrieval Priority

1. **Vector memory first** — `qdrant-find "{topic}"` for verified deep architecture (11 entries as of 2026-03-16)
2. **Deep maps second** — each repo's `REPO_DEEP_MAP.md` for file-level mapping
3. **Codebase last** — only if vector memory + deep maps don't have the answer

## Core Systems (verified against codebase 2026-03-16)

### Rule Engine
- **Pipeline**: RuleOrchestrator creates FactBag → DFS topo sort rules → FOR EACH: quota check → EvaluateAsync → ExecuteAsync → telemetry
- **Two-phase execution**: Phase 1 (EvaluateAsync) = condition + output fields, Phase 2 (ExecuteAsync) = side effects
- **Execution modes**: AllOrNothing (stop on fail), BestEffort (continue + aggregate), CompensateOnFailure (LIFO reversal)
- **Flow graph**: RuleGraphParser (Kahn's algorithm) → GraphRuleDispatchAdapter (edge routing: always/on-true/on-false/on-error)
- **FactBag**: Dictionary<string, object?>, JSON coercion for JsonElement, graph keys `__graph.node.{id}.*`
- **Decision Table**: hit policies (First/Unique/Collect/Priority), IFeelCellEvaluator per cell, forward-propagation outputs

### Workflow System
- **State machine**: MRuleWorkflowRunner loops steps (max 256), 5 types: Start/RuleTask/ServiceTask/ExclusiveGateway/End
- **Execution router**: 4 modes — Traditional, Rules, Hybrid (probabilistic), Shadow (diff logging)
- **RulesEngineService**: 3-level cache (RuntimeCache per-tenant TTL → WorkflowCache static max 2048 → ReflectionRuleCache per-TContext)
- **Hot-reload**: SaveAsync/SetActiveVersionAsync → invalidate caches → publish RuleSetChangeEvent → SignalR broadcast
- **Canary**: GetCanaryVersionForTenantAsync → tenant-specific version selection before cache lookup

### Multi-Tenancy
- **Context propagation**: TenantResolutionMiddleware → AsyncLocal (TenantContext.CurrentTenantId) → no parameter passing needed
- **Resolution order**: header(x-tenant-id) → path → subdomain → validates vs JWT claim (401 on mismatch)
- **ContextMirrorScope**: push/pop pattern for temporary tenant switches, manages TenantContext + UserContext + logging scope
- **Data isolation**: 3 strategies — SharedSchema (EF query filters), SeparateSchema (PostgreSQL SearchPath), SeparateDatabase
- **EF filters**: `e => e.TenantId == TenantContext.CurrentTenantId || TenantContext.CurrentTenantId == null` (auto-applied to ITenantScoped)
- **Quota**: 13 limits, 4 tier presets (Free/Starter/Professional/Enterprise), cache key `quota:{tenantId}:{type}:{periodKey}`

### Auth & License Guard
- **Startup**: CodeIntegrityVerifier (SHA256 assembly hashes) → AntiTamperDetector (debugger/profiler/hooks/breakpoints)
- **Runtime**: EnsureValid → grace period → feature check → enterprise fail-closed → HMAC chain verify
- **HMAC chain**: key = SHA256(licenseSignature + projectSeed + salt + serverNonce), data = `{prev}|{seq}|{tenant}|{action}|{hash}|{timestamp}`
- **Heartbeat**: BackgroundService, nonce rotation, revocation grace period (24h), degrade to Free on expiry
- **Auth rules**: CRUD at /api/v1/auth-rules → SignalR AuthRuleChangeHub → hot-reload broadcast
- **PDP**: IMPolicyDecisionService supports OpenFGA (/check) and OPA (/v1/data/authz/allow), fail-closed or fallback

### UI Engine
- **Runtime**: MUiEngineRuntime builds 4 indexed maps (O(1) lookup), manifest schema v1/v2
- **Bootstrap**: TTL cache (60s) + ETag HTTP caching + SignalR schema watcher → full cache invalidation on change
- **Components**: 23 Lit custom elements (mu-* prefix), Zustand vanilla stores (MCreate*Store factory pattern)
- **Decision table**: virtualized rendering (44px rows, 45 visible), undo/redo (50-action stack), version diff
- **Flow designer**: Lit + React hybrid (createRoot in shadow DOM), publish → save + approve/activate
- **License**: MLicenseVerifier (JWT RS256 verification in browser), MCanRenderCommercialFeature() gate with feature aliases

## Integration Flow

```
App → Governance.Enterprise (license check at startup) → LicenseServer POST /activate
App → RuleEngine.Runtime (execute rules) → RuleOrchestrator → FactBag pipeline → OrchestratorResult
App → ControlPlane.Api (manage rules) → REST + SignalR → Postgres + Redis(hot-reload)
Dashboard (React) → ControlPlane.Api (40+ REST methods) → SignalR (ruleset-changes, auth-rule-changes)
Dashboard → UI Engine (npm @muonroi/ui-engine-rule-components) → 23 Lit custom elements
```

---

# Scope — 4 Repo Ecosystem

Public OSS:

- muonroi-building-block (.NET libraries, 54 NuGet packages)
- muonroi-ui-engine (TypeScript UI, 8 npm packages)

Private services:

- muonroi-control-plane (31 MCP tools, 13 dashboard pages)
- muonroi-license-server (RSA-2048, MRR-{24-byte} keys)

Legacy:

- Muonroi.BaseTemplate
- Muonroi.Modular.Template
- Muonroi.Microservices.Template

---

# Automatic Knowledge Extraction

When any of these files change:

- REPO_MAP.md
- RULE_ENGINE_MAP.md
- AGENTS.md

Run architecture knowledge extraction and update vector-memory.

Command: `.commands/extract_architecture.md`