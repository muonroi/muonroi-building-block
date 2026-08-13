# Muonroi.RuleEngine.Core

## Description
The core implementation of the Muonroi Rule Engine, providing basic rule parsing, compilation, and evaluation logic.

## Features
- In-memory rule evaluation.
- Expression tree compilation for fast execution.
- Extensible action and condition handlers.

## Minimal Usage
```csharp
var rules = new[] { new Rule { Expression = "User.Age >= 18" } };
var engine = new RuleEngineCore(rules);
bool result = await engine.EvaluateAsync(new { User = new { Age = 20 } });
```
