> Quickstart demonstrating the Muonroi Source Generators for Diagnostics.

## What This Sample Demonstrates
- Using `[MTraceable]` attribute on a partial class method
- Consuming the generated trace wrapper (`DoHeavyWork_TraceWrapper()`) 
- Using `OutputItemType="Analyzer"` in `.csproj` to run the generator

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Diagnostics.Generator/src/Quickstart.Diagnostics.Generator.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `DemoService.cs` — Contains the `partial class DemoService` and `[MTraceable]` attribute usage
- `Quickstart.Diagnostics.Generator.Api.csproj` — The generator project reference

## How It Works
When you build the project, the `TraceableGenerator` scans for methods tagged with `[MTraceable]`. It generates a partial class that includes `{MethodName}_TraceWrapper()`. This wrapper wraps the original method call within an `MTraceContextHolder` tracing scope.
