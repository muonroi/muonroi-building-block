# Muonroi.RuleEngine.DecisionTable

## Description
Provides support for representing and executing business rules as Decision Tables within the Muonroi ecosystem.

## Features
- Parsers for DMN-style and spreadsheet-based decision tables.
- Fast multi-condition matching.
- Hit policy support (Unique, First, Priority, Collect).

## Minimal Usage
```csharp
var decisionTable = await decisionTableLoader.LoadAsync("PricingTable.json");
var result = decisionTable.Evaluate(new { CustomerType = "VIP", OrderTotal = 500 });
```
