# Muonroi.RuleEngine.NRules

## Description
An NRules-backed implementation of the Muonroi Rule Engine, bringing a powerful Rete-based forward-chaining inference engine to the ecosystem.

## Features
- Fluent API for defining rules in C#.
- Advanced pattern matching and forward chaining.
- Seamless integration with `Muonroi.RuleEngine.Abstractions`.

## Minimal Usage
```csharp
services.AddMuonroiRuleEngine(builder => 
{
    builder.UseNRules(cfg => cfg.LoadRulesFromAssembly(typeof(MyRule).Assembly));
});
```
