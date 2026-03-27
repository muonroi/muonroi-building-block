# Quickstart Rule Engine Sample

Source path:

- `muonroi-building-block/samples/Quickstart.RuleEngine`

## What this demonstrates

- Minimal API wiring for `AddRuleEngine<TContext>()`.
- Rule discovery via `AddRulesFromAssemblies(...)`.
- A single business endpoint: `POST /api/orders/evaluate`.
- Fact extraction from `RuleOrchestrator<OrderRequest>`.

## Quick run

```powershell
cd <workspace-root>\muonroi-building-block\samples\Quickstart.RuleEngine\src\Quickstart.RuleEngine.Api
dotnet restore
dotnet run
```

## Test request

```powershell
curl -X POST http://localhost:5000/api/orders/evaluate -H "Content-Type: application/json" -d "{\"amount\":1200,\"customerType\":\"premium\",\"countryCode\":\"US\"}"
```
