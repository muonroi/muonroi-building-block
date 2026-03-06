# Muonroi Naming and Branding Standard (English)

## Purpose

Standardize naming across packages, namespaces, and public APIs while **preserving Muonroi brand identity with `M*` prefix**.

## Core Rules

1. Package naming: `Muonroi.<Capability>[.<Implementation>]`.
2. Namespace root mirrors package root.
3. Public framework-owned types keep `M*` prefix, but must use approved domain taxonomy.
4. File name matches primary public type.
5. No vague bucket names (`Common`, `Utils`) in new code without ADR.

## Approved `M*` Prefix Taxonomy

Use `M` + Domain + TypeName:

1. `MCore*` for base primitives, helpers, and shared low-level utilities.
2. `MAuth*` for authentication and identity context types.
3. `MAuthZ*` for authorization policy/permission components.
4. `MData*` for persistence/repository/UoW implementations.
5. `MCache*` for cache services/options.
6. `MMsg*` for messaging and event bus components.
7. `MWeb*` for ASP.NET Core controllers/middleware/filters.
8. `MInfra*` for infrastructure adapters (Consul, K8s, jobs, observability).
9. `MTenant*` for tenancy concerns.
10. `MRule*` for rule engine orchestration/runtime.

## Interface Rules

1. Interfaces remain `I*`.
2. For Muonroi-owned interfaces, use `IM<Domain><Name>` (example: `IMTenantResolver`).
3. Avoid plain `IMXxx` where domain is unclear.

## Class Rules

1. Avoid generic single-prefix names like `MHelper`, `MService`, `MContext`.
2. Use explicit domain names: `MWebGenericController`, `MDataEfRepository`, `MCacheRedisService`.
3. Extension class names should be domain-explicit: `MWebServiceCollectionExtensions`.

## DTO / Options / Exception Rules

1. DTOs: `M<Domain><Name>Request` / `M<Domain><Name>Response`.
2. Options: `M<Domain><Name>Options`.
3. Exceptions: `M<Domain><Name>Exception`.

## Migration Strategy for Existing Names

1. Keep `M*` where already public and stable.
2. Normalize only ambiguous names to domain-qualified `M*`.
3. If a rename is required, provide `[Obsolete]` compatibility shims for at least one minor release.

## Enforcement

1. CI check: public types must match either:
   - `^M(Core|Auth|AuthZ|Data|Cache|Msg|Web|Infra|Tenant|Rule)[A-Z]\w*$`
   - or approved external conventions (`I*`, framework base classes, records).
2. Reject new ambiguous public names: `^M[A-Z][a-z]+$` with no domain token.
3. Require ADR for any exception.

## Done Criteria

1. `M*` brand prefix is preserved.
2. New/changed public types follow domain-qualified `M*` taxonomy.
3. Package and namespace naming remain consistent and migration-safe.
