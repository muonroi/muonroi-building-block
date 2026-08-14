> Demonstrates the core components of the Muonroi Rule Engine, including context execution, facts storage, and failure compensation.

## What This Sample Demonstrates
- Setup and usage of `RuleOrchestrator`
- Execution models (`ExecutionMode.AllOrNothing`)
- Standard Validation Rules (`Type = RuleType.Validation`)
- Business Rules with state compensation (`Type = RuleType.Business`, `ICompensatableRule`)
- Context modifications and `FactBag` propagation

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.RuleEngine.Core/src/Quickstart.RuleEngine.Core.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `Program.cs` — service registration and endpoint wiring
- `Controllers/OrderController.cs` — API endpoint triggering the orchestration
- `Rules/MinimumOrderValueRule.cs` — validation rule checking conditions
- `Rules/PremiumDiscountRule.cs` — compensatable business rule

## How It Works
The `RuleOrchestrator` manages execution of registered rules for an `OrderContext`. If `ExecutionMode.AllOrNothing` is set and a business rule fails, the orchestrator triggers `CompensateAsync` on already executed rules to revert context changes.
