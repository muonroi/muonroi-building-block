# Muonroi.RuleEngine.Abstractions

## Description
Contains the core interfaces and abstractions for rule evaluation, execution, and management within the Muonroi ecosystem.

## Features
- Standardized `IRuleEngine` interface.
- Core rule definitions and execution contexts.
- Extensible rule provider interfaces.

## Minimal Usage
```csharp
public class MyRuleEvaluator
{
    private readonly IRuleEngine _ruleEngine;
    public MyRuleEvaluator(IRuleEngine ruleEngine) => _ruleEngine = ruleEngine;
}
```
