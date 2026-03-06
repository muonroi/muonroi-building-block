# Runtime Ruleset Governance

This module provides production-grade APIs for runtime-configurable rulesets while keeping code-first flows intact.

## Package

- `Muonroi.RuleEngine.Runtime.Web`

## Registration

```csharp
services.AddRuleEngineRuntimeWeb(configuration);
```

This registers:

- `RulesEngineService`
- `IRuleSetStore` (file-backed)
- `IRuleSetDefinitionValidator`
- `IRuleSetAuditStore`
- Runtime ruleset controllers
- UI Engine manifest contributor

## API endpoints

Base route: `/api/v1/rule-engine/rulesets`

- `GET /` list workflows
- `GET /{workflow}/versions` list versions + active version
- `GET /{workflow}/export?version=` export ruleset JSON
- `POST /{workflow}` save ruleset (new version)
- `POST /{workflow}/activate/{version}` activate version
- `POST /{workflow}/validate` validate payload shape/contract
- `POST /{workflow}/dry-run` execute payload without persisting
- `GET /{workflow}/audit` query governance audit entries

## Two-lane model

1. Code-first
- Dev writes handlers + attributes.
- RuleGen generates `.g.cs`.
- Production runs generated rules.

2. Runtime-configurable
- BA/QC updates runtime JSON/DT through governance APIs/UI.
- Activate new versions without app rebuild.
- Export and merge back into source when needed.

## CI merge-back flow

1. Export active runtime ruleset.
2. `muonroi-rule merge --compile-check`.
3. Run parity check (runtime endpoint vs code endpoint).
4. Create PR to dev branch.

Helper scripts:

- `scripts/flow-runtime-roundtrip.py`
- `scripts/check-runtime-parity.py`
