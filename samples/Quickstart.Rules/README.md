> Demonstrates standalone usage of the FEEL (Friendly Enough Expression Language) Evaluator and Feature Flags.

## What This Sample Demonstrates
- Using `FeelEvaluator` to evaluate mathematical, logical, and string operations defined in FEEL
- Using `FeatureFlagEvaluator` for simple conditional feature toggles

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Rules/src/Quickstart.Rules.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `Controllers/RulesController.cs` — exposes `EvaluateFeel` and `EvaluateFlag`

## How It Works
The `Muonroi.Rules` package provides a standalone parser and evaluator for the FEEL specification. This is used as the underlying expression engine in other building blocks, but it can also be used directly for mathematical parsing or dynamic feature flags.
