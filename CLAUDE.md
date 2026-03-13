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

vector-memory is the **long-term memory system**.

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

Core components:

- Rule Engine Runtime
- Decision Table Execution
- Control Plane
- License Server
- UI Engine

Execution pipeline:

Client → ControlPlane → RuleEngine → DecisionTable → Result

---

# Scope — 4 Repo Ecosystem

Public OSS:

- muonroi-building-block (.NET libraries)
- muonroi-ui-engine (TypeScript UI)

Private services:

- muonroi-control-plane
- muonroi-license-server

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