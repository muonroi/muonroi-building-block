> Demonstrates how to create and evaluate DMN-style Decision Tables programmatically.

## What This Sample Demonstrates
- Setup and definition of a `DecisionTable`
- Using `IDecisionTableStore` (specifically `InMemoryDecisionTableStore`) to persist tables
- Using `IDecisionTableExecutor` to evaluate tables using FEEL expressions
- Implementing business rules like Loan Approval as decision tables

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.RuleEngine.DecisionTable/src/Quickstart.RuleEngine.DecisionTable.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `Program.cs` — service registration via `AddDecisionTableEngine()`
- `Controllers/DecisionController.cs` — API endpoints for setting up and evaluating the table

## How It Works
The Decision Table Engine evaluates input facts against a matrix of rows and columns. It uses FEEL (Friendly Enough Expression Language) syntax for its cells. When an evaluation request comes in, the executor processes the rules line by line, evaluating FEEL conditions and collecting output values based on the table's `HitPolicy`.
