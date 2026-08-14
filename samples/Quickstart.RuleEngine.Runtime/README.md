> Demonstrates the runtime execution engine, supporting JSON-defined rules, dynamic reloading, and storage abstractions.

## What This Sample Demonstrates
- Setup and usage of `RulesEngineService`
- File-based persistence of Rule Sets using `FileRuleSetStore`
- Dynamic definition and execution of rules via API
- Utilizing OpenTelemetry via `WorkflowCacheTelemetry`

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.RuleEngine.Runtime/src/Quickstart.RuleEngine.Runtime.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `appsettings.json` — configuration for `RuleStore` (e.g., path, cache toggle)
- `Program.cs` — service registration via `AddRuleEngineStore`
- `Controllers/RuntimeController.cs` — endpoints to dynamically deploy and execute rules

## How It Works
The `Muonroi.RuleEngine.Runtime` namespace provides infrastructure for defining rules dynamically instead of compiling them into C# code. Rules are stored in an `IRuleSetStore` (configured as a file store here) and cached by `IRuleSetRuntimeCache`. The `RulesEngineService` abstracts execution and rule retrieval.
