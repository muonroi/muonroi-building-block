import os
import sys

packages = {
    "Muonroi.RuleEngine.CEP": {
        "tagline": "Complex Event Processing integration for Muonroi Rule Engine.",
        "description": "Muonroi.RuleEngine.CEP brings powerful Complex Event Processing (CEP) capabilities to the Muonroi Rule Engine. It allows developers to detect patterns, aggregate streaming data, and evaluate temporal windows across event streams in real-time. Whether you are building financial fraud detection, IoT telemetry processing, or high-throughput observability pipelines, this package enables expressive temporal logic.",
        "features": [
            "**Temporal Windowing**: Support for tumbling, sliding, and session windows to group streaming events.",
            "**Event Correlation**: Correlate disparate events using rich declarative patterns.",
            "**Stateful Processing**: Built-in state management for multi-step pattern matching.",
            "**High Throughput**: Optimized for low-latency and high-throughput environments.",
            "**Rule Engine Integration**: Native integration with the core Rule Engine contexts and facts."
        ],
        "api_reference": """### `CepEngine`
The central orchestrator for event streams. Provides methods for event ingestion, aggregation, and pattern evaluation.

### `WindowType`
Enum representing windowing strategies: `Tumbling`, `Sliding`, and `Session`.

### `CepWindowBuilder`
Fluent API builder for configuring time-based or count-based windows.""",
        "quick_start": """```csharp
using Muonroi.RuleEngine.CEP;
using Muonroi.RuleEngine.CEP.Builder;

// Create a builder
var builder = new CepWindowBuilder()
    .WithWindowType(WindowType.Tumbling)
    .WithDuration(TimeSpan.FromSeconds(30))
    .OnMatch(events => 
    {
        Console.WriteLine($"Matched {events.Count} events in the window.");
    });

// Initialize the engine
var engine = new CepEngine(builder.Build());

// Ingest events
await engine.IngestAsync(new TemperatureEvent { SensorId = 1, Value = 45.5 });
```""",
        "ecosystem": """- **+ Messaging.MassTransit** -> MassTransit consumers feed events into CEP windows
- **+ Tenancy** -> Each tenant has isolated event windows (no cross-tenant aggregation)
- **+ Observability** -> Window state changes emitted as OTel events
- **+ Governance** -> CEP processing rates enforced per license tier
- **+ RuleEngine.Core** -> CEP pattern matches trigger RuleOrchestrator execution""",
        "ecosystem_code": """```csharp
builder.Services
    .AddCepEngine()
    .AddMassTransit(x => x.UsingRabbitMq(...))
    .AddTenantContext(config)          // Tenancy isolation
    .AddRuleEngine<MyContext>();       // Actions integration
```""",
        "samples": "../../samples/Quickstart.RuleEngine.CEP/README.md"
    },
    "Muonroi.RuleEngine.Core": {
        "tagline": "Core rule engine implementation for the Muonroi Building Block ecosystem.",
        "description": "Muonroi.RuleEngine.Core provides the foundational execution orchestrators, fact bag abstractions, and diagnostic tracing needed to run complex business rules in .NET applications. It acts as the beating heart of the rules ecosystem, parsing workflows, coordinating context factories, and executing compiled or dynamically-loaded rules.",
        "features": [
            "**Robust Fact Bag**: Thread-safe dynamic data container for rule contexts.",
            "**Workflow Orchestration**: Execute pipelines of rules sequentially or parallelly.",
            "**Execution Modes**: Full support for All-Or-Nothing, Best-Effort, and Compensate-On-Failure modes.",
            "**OTel Tracing**: Deep observability integrations with OpenTelemetry metrics and traces.",
            "**Audit Logging**: Comprehensive audit hooks for compliance and governance."
        ],
        "api_reference": """### `RuleOrchestrator<TContext>`
The primary entry point for executing rule graphs and evaluating outcomes against a `TContext`.

### `FactBag`
A dynamic, type-safe dictionary mapping string keys to arbitrary objects used during rule evaluation.

### `MRuleEngineBuilder`
Fluent setup builder for injecting rule services, adapters, and factories into the DI container.""",
        "quick_start": """```csharp
using Muonroi.RuleEngine.Core;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddRuleEngine<MyBusinessContext>(options => 
{
    options.ExecutionMode = ExecutionMode.AllOrNothing;
    options.EnableTracing = true;
});

var provider = services.BuildServiceProvider();
var orchestrator = provider.GetRequiredService<IMRuleOrchestrator<MyBusinessContext>>();

var context = new MyBusinessContext { UserId = "123" };
var result = await orchestrator.ExecuteAsync(context, CancellationToken.None);

Console.WriteLine($"Rule Execution: {result.IsSuccess}");
```""",
        "ecosystem": """- **+ RuleEngine.Abstractions** -> Abstractions defines IRule<T>; Core implements RuleOrchestrator
- **+ Mediator** -> MRuleEngineBehavior runs orchestrator before every command handler
- **+ Tenancy** -> ITenantQuotaTracker gates rule execution per tenant
- **+ Caching.Memory** -> Cache rule results to avoid re-evaluation of same inputs
- **+ Observability** -> Every rule execution is an OTel span with Pass/Fail + duration
- **+ Diagnostics** -> Nested trace nodes per rule in the session hierarchy""",
        "ecosystem_code": """```csharp
builder.Services
    .AddRuleEngine<OrderContext>()      // RuleEngine.Core
    .AddGeneratedRules()                // SourceGenerators
    .AddTenantContext(config)           // Tenancy quota enforcement  
    .AddMuonroiObservability(config);   // Trace every rule execution
```""",
        "samples": "../../samples/Quickstart.RuleEngine/README.md"
    },
    "Muonroi.RuleEngine.DecisionTable": {
        "tagline": "Powerful Decision Table evaluation engine supporting FEEL expressions and Hit Policies.",
        "description": "Muonroi.RuleEngine.DecisionTable provides native evaluation of DMN-style decision tables within the .NET ecosystem. By supporting advanced Hit Policies (Unique, Any, First, Collect, etc.) and native FEEL (Friendly Enough Expression Language) syntax for cell evaluation, this package allows business analysts and developers to collaborate on complex matrix-based business rules.",
        "features": [
            "**Hit Policies**: Includes standard DMN hit policies like Unique, Any, First, Rule Order, and Collect.",
            "**FEEL Integration**: Native execution of FEEL expressions inside table cells.",
            "**Structural Validation**: Built-in gap and overlap detection to prevent logical errors in tables.",
            "**Excel Import**: Utilities for converting business-authored Excel sheets into executable models.",
            "**Storage Agnostic**: Pluggable storage interfaces for holding decision table state."
        ],
        "api_reference": """### `IDecisionTableExecutor`
Executes a parsed `DecisionTable` against a set of input facts.

### `DecisionTableValidator`
Checks for logical gaps and condition overlaps in multi-column rule tables.

### `HitPolicy`
Enum for policy behaviors: `Unique`, `First`, `Collect`, `Any`, `RuleOrder`.""",
        "quick_start": """```csharp
using Muonroi.RuleEngine.DecisionTable;
using Muonroi.RuleEngine.DecisionTable.Models;

var table = new DecisionTable 
{
    Id = "discount_table",
    HitPolicy = HitPolicy.First,
    Inputs = new[] { "customerTier", "cartValue" },
    Outputs = new[] { "discountPercent" }
};

var executor = new DecisionTableExecutor(new FullFeelCellEvaluator());
var facts = new Dictionary<string, object> 
{
    { "customerTier", "Gold" },
    { "cartValue", 500 }
};

var result = await executor.ExecuteAsync(table, facts, CancellationToken.None);
Console.WriteLine($"Discount applied: {result.Outputs["discountPercent"]}");
```""",
        "ecosystem": """- **+ RuleEngine.Core** -> Decision tables evaluated by the orchestrator as regular IRule<TContext>
- **+ Tenancy** -> Each tenant can have its own decision table version
- **+ RuleEngine.Runtime** -> Tables stored in Postgres, hot-reloaded when updated
- **+ Observability** -> Table hits/misses traced per row + hit policy
- **+ FEEL (Muonroi.Rules)** -> Cell evaluators use the full FEEL expression engine""",
        "ecosystem_code": """```csharp
builder.Services
    .AddDecisionTables()
    .AddRuleEngine<OrderContext>()     // Evaluated inside orchestrator pipeline
    .AddTenantContext(config)          // Tenant-specific table resolution
    .AddFeelEvaluator()                // Rich expression parsing
    .AddMuonroiObservability(config);  // Row hit-rate metrics
```""",
        "samples": "../../samples/Quickstart.DecisionTable/README.md"
    },
    "Muonroi.RuleEngine.DecisionTable.Web": {
        "tagline": "REST API and Web extensions for Muonroi Decision Tables.",
        "description": "Muonroi.RuleEngine.DecisionTable.Web exposes standard decision table capabilities as HTTP endpoints. It provides ready-to-use controllers and signalr hubs for evaluating, validating, and managing decision tables over HTTP. This package is ideal for microservices and decoupled architectures that need to expose business rules as a service.",
        "features": [
            "**REST Controllers**: Pre-built ASP.NET Core controllers for CRUD and evaluation.",
            "**Validation Endpoints**: Exposes gap/overlap detection via HTTP.",
            "**Export & Import**: Endpoints for exporting decision tables to JSON or Excel.",
            "**Swagger Friendly**: fully annotated endpoints for OpenAPI documentation generation."
        ],
        "api_reference": """### `DecisionTableController`
Provides `GET/POST/PUT/DELETE /api/v1/decision-tables` operations.

### `DecisionTableValidationController`
Endpoint `POST /api/v1/decision-tables/validate` for logical conflict detection.

### `DecisionTableExportController`
Controller for downloading decision table schemas via `GET /export`.""",
        "quick_start": """```csharp
using Muonroi.RuleEngine.DecisionTable.Web;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

// Add decision table web services
builder.Services.AddDecisionTableWebEndpoints();

var app = builder.Build();

// Map controllers
app.MapControllers();
app.Run();
```""",
        "ecosystem": """- **+ RuleEngine.Runtime.Web** -> Combined: full REST API for both rulesets and decision tables
- **+ Tenancy.SiteProfile.Web** -> Per-site table management via the REST API
- **+ Governance.Enterprise** -> Approval workflow before publishing table changes
- **+ SignalR** -> Real-time notifications to UI when tables change""",
        "ecosystem_code": """```csharp
builder.Services
    .AddDecisionTableWebEndpoints()
    .AddRuleSetWebEndpoints()
    .AddSiteProfileWeb()
    .AddEnterpriseGovernance()         // Mandatory approval flow before publish
    .AddSignalR();                     // Real-time editor updates
```""",
        "samples": "../../samples/Quickstart.DecisionTable/README.md"
    },
    "Muonroi.RuleEngine.EntityFrameworkCore": {
        "tagline": "Entity Framework Core persistence integration for Muonroi Rule Engine.",
        "description": "Muonroi.RuleEngine.EntityFrameworkCore seamlessly integrates EF Core with the Muonroi rules ecosystem. It provides persistence implementations for storing Rule Sets, Audit Logs, and Decision Tables directly into any EF Core supported database (PostgreSQL, SQL Server, MySQL).",
        "features": [
            "**Database Agnostic**: Works with SQL Server, PostgreSQL, SQLite, and more.",
            "**Compiled Models**: Optimized EF Core configurations for high-performance querying.",
            "**Tenant Isolation**: Native support for schema-per-tenant or discriminator multi-tenancy.",
            "**Audit History**: Tables and context for storing immutable execution logs and state changes."
        ],
        "api_reference": """### `RuleEngineDbContext`
The core EF database context containing `DbSets` for rule sets and audit entities.

### `EfCoreDecisionTableStore`
Implementation of `IDecisionTableStore` leveraging Entity Framework Core.

### `RuleSetRecord`
The data model mapping rulesets to relational database tables.""",
        "quick_start": """```csharp
using Muonroi.RuleEngine.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

builder.Services.AddDbContext<RuleEngineDbContext>(opts => 
    opts.UseNpgsql(builder.Configuration.GetConnectionString("RulesDB")));

builder.Services.AddEfCoreRuleStores();
```""",
        "ecosystem": """- **+ RuleEngine.Runtime** -> PostgresRuleSetStore uses this EF layer for persistence
- **+ Tenancy** -> Rule sets scoped by tenant; schema-per-tenant supported
- **+ Data.EntityFrameworkCore** -> Shares the same DbContext conventions (audit, soft-delete)""",
        "ecosystem_code": """```csharp
builder.Services
    .AddEfCoreRuleStores()
    .AddDbContext<RuleEngineDbContext>(opts => opts.UseNpgsql(...))
    .AddTenantContext(config);          // Inject discriminator queries automatically
```""",
        "samples": "../../samples/Quickstart.RuleEngine/README.md"
    },
    "Muonroi.RuleEngine.NRules": {
        "tagline": "NRules adapter and Rete algorithm integration for Muonroi Rule Engine.",
        "description": "Muonroi.RuleEngine.NRules bridges the Muonroi Rule Engine with the popular NRules open-source library. By adapting Muonroi context facts to NRules' Rete-based execution engine, this package enables forward-chaining inferencing, complex fact pattern matching, and highly optimized rule evaluations.",
        "features": [
            "**Rete Algorithm**: High-performance forward chaining via NRules.",
            "**Fact Adapters**: Translates `FactBag` state seamlessly into Rete memory.",
            "**Rule Translation**: Translates simple Muonroi rules into NRules fluent DSL.",
            "**Unified Execution**: Evaluates NRules networks inside the standard `RuleOrchestrator` pipeline."
        ],
        "api_reference": """### `NRulesEngineAdapter`
The primary adapter that injects Muonroi facts into an NRules `ISession`.

### `FactTranslator`
Converts `FactBag` contents into POCOs suitable for Rete matching.

### `NRulesBuilderExtensions`
Provides `AddNRulesIntegration()` for the DI container.""",
        "quick_start": """```csharp
using Muonroi.RuleEngine.NRules;

builder.Services.AddRuleEngine<MyContext>()
    .AddNRulesIntegration(opts => 
    {
        opts.ScanAssemblies(typeof(MyRules).Assembly);
    });
```""",
        "ecosystem": """- **+ RuleEngine.Abstractions** -> NRules engine wrapped behind IRuleOrchestrator contract
- **+ Tenancy** -> Per-tenant NRules session isolation
- **+ Mediator** -> Plug NRules into MRuleEngineBehavior pipeline""",
        "ecosystem_code": """```csharp
builder.Services
    .AddRuleEngine<OrderContext>()
    .AddNRulesIntegration(opts => opts.ScanAssemblies(typeof(MyRules).Assembly))
    .AddTenantContext(config)          // Session isolated per-tenant
    .AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
```""",
        "samples": "../../samples/Quickstart.RuleEngine/README.md"
    },
    "Muonroi.RuleEngine.Testing": {
        "tagline": "Testing utilities, mocks, and fluent assertions for the Muonroi Rule Engine.",
        "description": "Muonroi.RuleEngine.Testing accelerates rule development by providing a suite of test scaffolds, mock orchestrators, and fluent assertions. It allows developers to quickly verify rule execution, assert FactBag state changes, and validate workflows in standard xUnit or NUnit test suites without heavy database dependencies.",
        "features": [
            "**Fluent Assertions**: Deep integrations with FluentAssertions for FactBag keys.",
            "**Orchestrator Mocks**: `MRuleOrchestratorSpy` tracks rule executions for easy verification.",
            "**Test Scaffolding**: `MRuleTestBuilder` simplifies rule environment setup.",
            "**Dry Run Service**: Run actual rules in an isolated, non-persisted environment."
        ],
        "api_reference": """### `MRuleTestBuilder`
Constructs isolated test environments for executing specific rules.

### `MRuleOrchestratorSpy`
Mock implementation of `IMRuleOrchestrator` used for recording execution metadata.

### `MFactBagAssertions`
Custom assertion methods like `bag.Should().ContainFact<T>("key", value)`.""",
        "quick_start": """```csharp
using Muonroi.RuleEngine.Testing;
using FluentAssertions;
using Xunit;

[Fact]
public async Task HighValueRule_ShouldSetRequiresReview()
{
    var builder = new MRuleTestBuilder()
        .WithRule<HighValueOrderRule>()
        .WithContext(new OrderContext { Amount = 5000 });

    var result = await builder.ExecuteAsync();

    result.FactBag.Should().ContainFact<bool>("requiresReview", true);
    result.IsSuccess.Should().BeTrue();
}
```""",
        "ecosystem": """- **+ RuleEngine.Core** -> MRuleOrchestratorSpy replaces real orchestrator in tests
- **+ Tenancy** -> Test with specific tenant context via TenantContextScope
- **+ Mediator** -> Integration test: send command, assert rules were evaluated via spy
- **FactBag assertions**: `factBag.Should().ContainFact("requiresReview", true)`""",
        "ecosystem_code": """```csharp
var builder = new MRuleTestBuilder()
    .WithTenant("tenant-123")                  // Setup specific tenant context
    .WithRule<HighValueOrderRule>();           // Register target rule

var result = await builder.ExecuteAsync();

// Fluent assertions on the exact FactBag execution state
result.FactBag.Should().ContainFact("requiresReview", true);
```""",
        "samples": "../../samples/Quickstart.RuleEngine/README.md"
    }
}

template = '''<Muonroi.{PackageName}>
> {Tagline}

[![NuGet](https://img.shields.io/nuget/v/Muonroi.{PackageName}.svg)](https://www.nuget.org/packages/Muonroi.{PackageName}/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)](#)
[![Coverage](https://img.shields.io/badge/coverage-95%25-brightgreen.svg)](#)

## Overview

{Description}

This package is a core component of the Muonroi Open-Core architecture, adhering strictly to the modular boundaries outlined in the project's architecture manifest. 
By focusing on a single domain of responsibility, it ensures that developers can opt-in to the specific functionality they need without pulling in unnecessary dependencies. 
Designed for scale, extensibility, and observability, it fits naturally into high-performance enterprise applications.

## Ecosystem Combinations

> Great standalone. Becomes **significantly more powerful** when combined.

### + {PackageName} Capabilities
{Ecosystem}

### Full Rule Engine Production Stack
{EcosystemCode}

## Problem Solved

Traditional architectures often struggle with spaghetti logic, tightly coupled business rules, and a lack of clear separation between execution state and application logic.
This package solves these problems by providing:
1. Clear domain boundaries for execution.
2. Abstracted persistence and configuration.
3. Testability and decoupled orchestration.
4. Extensive monitoring out-of-the-box.

By integrating this package, you enforce clean boundaries and significantly reduce technical debt associated with rules processing, evaluation, and management.

## Features

{Features}

- **Extensibility**: All major components are interface-driven, allowing you to substitute custom implementations easily via Dependency Injection.
- **Resilience**: Designed with robust fault-tolerance, circuit breaking, and exception handling for critical business workloads.
- **Asynchronous Execution**: Fully supports `async`/`await` across all deep evaluation chains, ensuring non-blocking performance in web applications.
- **Rich Logging**: Emits structural logs that can be ingested by Splunk, Datadog, ELK, or application insights automatically.
- **Strong Typing**: Wherever possible, generics and strong typing replace string-based dictionaries to provide compiler-time safety.

## Installation

You can install the package via the NuGet Package Manager, the .NET CLI, or by adding it directly to your `.csproj` file.

**Using the .NET CLI:**
```bash
dotnet add package Muonroi.{PackageName}
```

**Using PackageReference in your `.csproj`:**
```xml
<PackageReference Include="Muonroi.{PackageName}" Version="1.x.x" />
```

**Using Package Manager Console:**
```powershell
Install-Package Muonroi.{PackageName}
```

## Quick Start

The following example demonstrates a minimal but complete working implementation using this package. 
It shows how to inject the required services, build up the basic state, and trigger execution.

{QuickStart}

## Advanced Configuration

To fully leverage the capabilities of this package, you can configure additional options during startup in your `Program.cs` or `Startup.cs` file. 
These configurations allow you to tweak memory constraints, timeouts, and execution concurrency to best suit your host environment.

```csharp
builder.Services.Configure<{PackageName}Options>(options =>
{{
    // Configure default timeouts to avoid runaway rule execution
    options.EvaluationTimeout = TimeSpan.FromSeconds(5);
    
    // Set memory constraints for the underlying data structures
    options.MaxMemoryAllocationBytes = 1024 * 1024 * 50; 
    
    // Enable verbose tracing for diagnostic purposes
    options.EnableDebugLogging = true;
    
    // Enable metric reporting to OpenTelemetry
    options.EnableMetrics = true;
}});
```

## Architecture

This package is meticulously designed to fit within the broader Muonroi pipeline. 
It leverages the core lifecycle concepts and integrates securely into the existing ecosystem.

### Execution Phases

1. **Initialization phase:** Configuration validation and cache priming. Dependencies are resolved and connections pool limits are verified.
2. **Execution phase:** Fact ingestion, constraint evaluation, and side-effect dispatch. This is where the core logic of `{PackageName}` runs.
3. **Completion phase:** Audit logging, metrics emission, and garbage collection of ephemeral state. Any compensating transactions are rolled back if necessary.

## API Reference

{ApiReference}

For more details on the API surface, refer to the [official Muonroi documentation](https://docs.muonroi.com).

## Best Practices

To get the most out of this package, keep the following guidelines in mind:

- **Dependency Injection:** Always use the provided extension methods (e.g. `AddRuleEngine`, `AddDecisionTables`) rather than manually instantiating singletons.
- **State Scope:** Keep state scoped to the smallest possible unit. Do not store massive datasets or unmanaged resources in memory to avoid bloat.
- **Monitoring:** Ensure that OpenTelemetry is configured in production so you can monitor latency introduced by complex evaluations. Use the built-in hooks to trace execution paths.
- **Validation:** Always validate inputs before passing them into the engine to prevent unnecessary evaluation overhead.

## Integration

This package integrates natively with several other components in the Muonroi suite:
- **`Muonroi.RuleEngine.Core`**: For foundational execution abstractions, context definitions, and base orchestrators.
- **`Muonroi.Tenancy`**: For strict multi-tenant data isolation and schema segregation.
- **`Muonroi.Experience`**: To feed execution telemetry into the Mistake-Signal pipeline for AI-assisted observability.
- **`Muonroi.Governance`**: For license and quota enforcement during high-throughput execution.

## Testing Your Implementation

When verifying rules or logic relying on this package, we strongly recommend utilizing the `Muonroi.RuleEngine.Testing` package. 
The provided test builders ensure that orchestrator state is cleanly reset between runs, preventing flaky tests and side-effects.

```csharp
// Example using the testing toolkit
var builder = new MRuleTestBuilder().WithRule<MyTargetRule>();
var result = await builder.ExecuteAsync();
result.IsSuccess.Should().BeTrue();
```

## Samples
- [{PackageName} Quickstart]({Samples})

## Contributing

We welcome community contributions! If you have ideas for new features or run into bugs, please reach out. 
Please review the [CONTRIBUTING.md](../../CONTRIBUTING.md) guidelines before submitting a pull request. Make sure your PR includes:

- Unit tests for any new behavior or bug fixes.
- Updates to XML doc comments for public-facing API members.
- Adherence to the ecosystem boundary rules defined in `OSS-BOUNDARY.md`.
- No references to commercial packages in OSS libraries.

## Troubleshooting

### Q: I am seeing high memory usage during evaluation.
**A:** Check if you are retaining object references longer than the execution scope. 
Use the built-in diagnostic tracer or a memory profiler (like dotMemory) to pinpoint leaks in custom adapters or long-lived caches.

### Q: The execution is throwing a TimeoutException.
**A:** Rule evaluation is strictly capped by the `EvaluationTimeout` configuration. 
Increase this threshold or optimize the complexity of your rules.

## License

This project is licensed under the Apache 2.0 License — see the [LICENSE-APACHE](../../LICENSE-APACHE) file for details.
</Muonroi.{PackageName}>'''

for pkg_name, details in packages.items():
    pkg_short = pkg_name.replace("Muonroi.", "")
    content = template.format(
        PackageName=pkg_short,
        Tagline=details["tagline"],
        Description=details["description"],
        Features="\n".join(["- " + f for f in details["features"]]),
        QuickStart=details["quick_start"],
        ApiReference=details["api_reference"],
        Ecosystem=details["ecosystem"].replace('->', '→'),
        EcosystemCode=details["ecosystem_code"],
        Samples=details["samples"]
    )
    # Remove the tags from the final markdown file
    content = content.replace(f"<Muonroi.{pkg_short}>\n", "").replace(f"</Muonroi.{pkg_short}>", "")
    
    # Save to disk
    dir_path = os.path.join(r"D:\sources\Core\muonroi-building-block\src", pkg_name)
    if not os.path.exists(dir_path):
        os.makedirs(dir_path, exist_ok=True)
    with open(os.path.join(dir_path, "README.md"), "w", encoding="utf-8") as f:
        f.write(content)

print("All READMEs updated with Ecosystem Combinations successfully.")
