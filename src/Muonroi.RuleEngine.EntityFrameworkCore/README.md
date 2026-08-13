# Muonroi.RuleEngine.EntityFrameworkCore

## Description
Provides Entity Framework Core integration for the Muonroi Rule Engine, enabling rule persistence and loading from databases.

## Features
- Rule storage in relational databases.
- Dynamic rule updates via EF Core tracked entities.
- Optimized query translation for rule retrieval.

## Minimal Usage
```csharp
services.AddMuonroiRuleEngine(builder => 
{
    builder.UseEntityFrameworkCore<MyDbContext>();
});
```
