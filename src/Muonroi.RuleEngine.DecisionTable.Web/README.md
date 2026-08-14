# Muonroi.RuleEngine.DecisionTable.Web

[![NuGet](https://img.shields.io/nuget/v/Muonroi.RuleEngine.DecisionTable.Web.svg)](https://www.nuget.org/packages/Muonroi.RuleEngine.DecisionTable.Web/)

> REST API endpoints for managing, validating, and testing decision tables in the Muonroi ecosystem.

## Installation

```bash
dotnet add package Muonroi.RuleEngine.DecisionTable.Web
```

## Overview
Exposes standard web controllers to manage decision tables over HTTP. It includes `DecisionTableController` for CRUD operations, `DecisionTableFeelController` for testing FEEL expressions, and `DecisionTableValidationController` for detecting gaps and overlaps.

## Features
- **CRUD Operations**: Manage decision tables via `DecisionTableController` (`GET/POST/PUT/DELETE /api/v1/decision-tables`).
- **Validation Endpoints**: Detect gaps and overlaps using `DecisionTableValidationController` (`POST /api/v1/decision-tables/validate`).
- **FEEL Testing**: Test specific FEEL expressions in real-time via `DecisionTableFeelController`.
- **Exporting**: Export decision tables via `DecisionTableExportController` (`GET /export`).

## Quick Start
```csharp
// Register the controllers in your ASP.NET Core application
builder.Services.AddDecisionTableWebEndpoints();

// In the application configuration pipeline
app.MapDecisionTableEndpoints();
```

## Ecosystem Combinations

### + RuleEngine.DecisionTable → Complete Execution Environment
Combine the web endpoints with the execution engine to allow hot-reloading and remote management of active decision tables.

### + Governance.Enterprise → Compliance and Auditing
All modifications made via `DecisionTableController` are automatically audited and linked to the active enterprise user session via `ISystemExecutionContext`.

## Samples
- [`Quickstart.DecisionTable`](../../samples/Quickstart.DecisionTable)

## License
Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).



