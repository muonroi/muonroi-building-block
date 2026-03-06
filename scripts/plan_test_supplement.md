# Test Supplement Plan - Runtime Coverage

Date: 2026-03-03

## Scope

This supplement adds runtime verification coverage for gaps G01-G27 with 4 flows:

1. `flow-rule-engine-behaviors.py`
2. `flow-multitenant-rule-isolation.py`
3. `flow-rulegen-cli-v2.py`
4. `flow-source-generator-integration.py`

And extends:

5. `flow-enterprise-multitenant-ruleengine-ui.ps1` (decision-table evaluate + feel-eval checks)
6. `flow-runtime-roundtrip.py` (runtime export -> merge --compile-check -> optional parity)
7. `check-runtime-parity.py` (runtime response parity with code-generated endpoint)

## Endpoint Supplements In Sample Project

The sample API (`PairMultiTenantE2E.API`) was extended to expose missing runtime test hooks:

- `POST /api/v1/rule-engine/supplement/reset`
- `GET /api/v1/rule-engine/supplement/metrics`
- `POST /api/v1/rule-engine/supplement/test`
- `POST /api/v1/rule-engine/supplement/tenant-rules/register`
- `POST /api/v1/rule-engine/supplement/tenant-rules/evaluate`
- `GET /api/v1/rule-engine/feel-eval`
- `POST /api/v1/decision-tables/evaluate`

These are used only for E2E supplement validation.

## Coverage Map

- `G01-G08, G10-G12` -> `flow-rule-engine-behaviors.py`
- `G09` -> `flow-multitenant-rule-isolation.py`
- `G13-G20` -> `flow-rulegen-cli-v2.py`
- `G21-G24` -> `flow-source-generator-integration.py`
- `G25-G27` -> `flow-enterprise-multitenant-ruleengine-ui.ps1` (updated)

## Evidence Outputs

All flows write JSON evidence to `MuonroiBuildingBlock/_tmp/`:

- `{project}_rule_engine_behaviors_{timestamp}.json`
- `{project}_multitenant_rule_isolation_{timestamp}.json`
- `{cli}_v2_behaviors_{timestamp}.json`
- `source_generator_integration_{timestamp}.json` (default naming from script)
- `{project}_enterprise_multitenant_e2e_{timestamp}.json` (updated enterprise flow)
- `runtime_roundtrip_{workflow}_{timestamp}.json`
- `runtime_parity_{timestamp}.json`

Each flow emits `OverallStatus = PASS|FAIL`.

## Run Commands

```powershell
# D1
py -3 .\scripts\flow-rule-engine-behaviors.py `
  --project-path "...\PairMultiTenantE2E.API.csproj" `
  --activation-proof-path "...\enterprise\activation_proof.json"

# D2
py -3 .\scripts\flow-multitenant-rule-isolation.py `
  --project-path "...\PairMultiTenantE2E.API.csproj" `
  --activation-proof-path "...\enterprise\activation_proof.json"

# D3
py -3 .\scripts\flow-rulegen-cli-v2.py `
  --rulegen-project "D:\sources\Core\MuonroiBuildingBlock\tools\Muonroi.RuleGen\Muonroi.RuleGen.csproj"

# D4
py -3 .\scripts\flow-source-generator-integration.py `
  --source-generator-project "D:\sources\Core\MuonroiBuildingBlock\src\Muonroi.RuleEngine.SourceGenerators\Muonroi.RuleEngine.SourceGenerators.csproj"

# D5 (updated existing flow)
.\scripts\flow-enterprise-multitenant-ruleengine-ui.ps1 `
  -ProjectPath "...\PairMultiTenantE2E.API.csproj" `
  -ActivationProofPath "...\enterprise\activation_proof.json"
```
