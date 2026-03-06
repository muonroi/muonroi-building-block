# Rule Engine Upgrade Plan (Execution Status)

## Scope

Upgrade Rule Engine across:

- `MuonroiBuildingBlock`
- `Muonroi.Docs`
- `Muonroi.Microservices.Template`
- `Muonroi.Modular.Template`
- `Muonroi.BaseTemplate`

## Plan 1: Code-First Rule Extraction

- [x] Added extraction attributes:
  - `MExtractAsRuleAttribute` + alias `ExtractAsRuleAttribute`
  - `RuleExecutionMode`
  - `MRuleModeAttribute` + alias `RuleModeAttribute`
- [x] Implemented CLI tool `Muonroi.RuleGen` (`muonroi-rule`):
  - `extract --source --output [--namespace] [--context]`
  - `verify --source --rules`
  - `register --rules --output [--namespace]`
- [x] Added generated registration pattern (`AddMGeneratedRules` output from CLI)

## Plan 2: Testing & Verification Toolkit

- [x] Added package `Muonroi.RuleEngine.Testing`:
  - `MRuleTestBuilder<TContext>`
  - `MRuleOrchestratorSpy<TContext>`
  - `FactBagAssertions.Should(...)`
  - `MRuleExecutionRecord`, `MRuleTestResult`
- [x] Added tests for:
  - new extraction/runtime mode attributes
  - unified registration + runtime router behavior
  - test toolkit usage

## Plan 3: Documentation & Template Enhancement

- [x] Added new docs in `Muonroi.Docs/docs`:
  - `rule-engine-testing-guide.md`
  - `rule-engine-external-services.md`
  - `rule-engine-hooks-guide.md`
  - `rule-engine-configuration-reference.md`
  - `rule-engine-dependencies.md`
  - `rule-engine-advanced-patterns.md`
- [x] Updated docs navigation/index:
  - `docs/README.md`
  - `docs/toc.yml`
  - `index.md`
  - `docs/rule-engine-guide.md` (related links)
- [x] Added template quickstarts:
  - `Muonroi.Microservices.Template/docs/RULE_ENGINE_QUICKSTART.md`
  - `Muonroi.Modular.Template/docs/RULE_ENGINE_QUICKSTART.md`
  - `Muonroi.BaseTemplate/docs/RULE_ENGINE_QUICKSTART.md`
- [x] Updated template `README.md` files with Rule Engine section and centralized docs links

## Plan 4: Unify Dual Rule Engines

- [x] Added unified typed registration API in `Muonroi.RuleEngine.Core`:
  - `AddRuleEngine<TContext>(...)`
  - `AddMRuleEngine<TContext>(...)`
  - `AddRuleOrchestrator<TContext>(...)` (obsolete alias)
  - `MRuleEngineBuilder<TContext>`
- [x] Added runtime routing layer:
  - `IMRuleExecutionRouter<TContext>`
  - `MRuleExecutionRouter<TContext>`
  - `MRuleEngineOptions`
- [x] Added optional hook-point filtered execution:
  - `RuleOrchestrator.ExecuteAsync(context, HookPoint, cancellationToken)`

## Plan 5: Enhanced FEEL Evaluator

- [x] Added logical operators:
  - `AND`, `OR`, `NOT`, parentheses
- [x] Added arithmetic operators:
  - `+`, `-`, `*`, `/`
- [x] Added operators:
  - `=`, `!=`, `>`, `>=`, `<`, `<=`, `in`, `contains`, `startsWith`
- [x] Added functions:
  - `today()`, `now()`, `days(n)`, `years(n)`
  - `upper(x)`, `lower(x)`, `abs(x)`, `round(x,n)`, `sum(x)`
- [x] Added nested path access:
  - `order.customer.vipStatus`
  - `items[0].price`
  - wildcard aggregation: `items[*].price`
- [x] Added advanced FEEL test coverage in `tests/Rules/Feel/FeelEvaluatorAdvancedTests.cs`

## Notes

- Existing API behavior is kept backward compatible for current consumers.
- Long-term Source Generator phase can be added later as a separate package (`Muonroi.RuleEngine.SourceGen`) without breaking this rollout.
