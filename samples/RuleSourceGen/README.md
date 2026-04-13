# RuleSourceGen Sample

## What this demonstrates

- `[MExtractAsRule]` on plain C# methods instead of hand-written `IRule<TContext>` classes.
- Source-generated rule classes compiled into the sample assembly.
- Runtime discovery of generated rules through `AddRulesFromAssemblies(typeof(Program).Assembly)`.
- Unit tests with `MRuleOrchestratorSpy` against the generated rules.

## Quick run

```powershell
cd .\samples\RuleSourceGen\src\RuleSourceGen.Api
dotnet restore
dotnet run
```

## Evaluate discounts

```powershell
curl -X POST http://localhost:5000/api/discounts/evaluate -H "Content-Type: application/json" -d "{\"customerType\":\"premium\",\"subtotal\":600,\"loyaltyYears\":6,\"isBlackFriday\":true}"
```

Expected result:

- `totalDiscountRate: 0.25`
- `finalTotal: 450`

## Run the sample tests

```powershell
cd .\samples\RuleSourceGen\tests\RuleSourceGen.Api.Tests
dotnet test
```

The tests assert both the generated rule execution order and the produced fact values.
