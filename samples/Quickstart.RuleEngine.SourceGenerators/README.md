> Demonstrates automatic generation of rules from methods annotated with `[MExtractAsRule]`.

## What This Sample Demonstrates
- Using the `[MExtractAsRule]` attribute
- How the source generator automatically creates `IRule` classes for the annotated methods
- Automatic dependency injection registration via `AddMGeneratedRules()`

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.RuleEngine.SourceGenerators/src/Quickstart.RuleEngine.SourceGenerators.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `Program.cs` — service registration using the generated `AddMGeneratedRules()` extension
- `Rules/UserValidationRules.cs` — standard partial class with rule methods annotated with `[MExtractAsRule]`

## How It Works
The `Muonroi.RuleEngine.SourceGenerators` analyzer hooks into the build pipeline. It scans for methods annotated with `[MExtractAsRule]`, generates dedicated classes that implement `IRule<TContext>`, and emits DI wiring methods so developers do not have to write boilerplate classes for every simple rule.
