<Muonroi.Core.Abstractions>
> Core interfaces defining the boundaries of the Muonroi ecosystem.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Core.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Core.Abstractions/)
[![License](https://img.shields.io/badge/license-Apache%202.0-green.svg)](../../LICENSE-APACHE)

## Overview

The `Muonroi.Core.Abstractions` package provides fundamental building blocks for modern .NET applications. It is part of the Muonroi ecosystem, designed to accelerate enterprise software development by providing robust, scalable, and highly cohesive components. 

Whether you are building microservices, monolithic applications, or serverless functions, this package abstracts the complexity of infrastructure plumbing, letting you focus on business logic. It has been built from the ground up with multi-tenancy, high performance, and deep observability in mind.

This package specifically addresses the complexities of state management, cross-cutting concerns, and uniform API design across distributed systems. It seamlessly integrates with the rest of the Muonroi ecosystem to provide a standardized development experience. 

## Features

- **High Performance**: Optimized for low latency and minimal allocations using modern .NET primitives like `Span<T>` and `ValueTask`.
- **Dependency Injection Ready**: Seamless integration with Microsoft.Extensions.DependencyInjection for lifecycle management.
- **Multi-Tenancy Support**: Native support for tenant isolation and context flow.
- **Observability**: Built-in OpenTelemetry metrics and tracing for deep insights into application behavior.
- **Extensibility**: Interfaces and hooks for custom implementations and overrides.
- **Resilience**: Integrated retry policies and circuit breakers where applicable using Polly.
- **Thread-safe Operations**: All core services guarantee thread safety for concurrent access.
- **Comprehensive Logging**: Detailed structured logging via `ILogger` with appropriate semantic scopes.

## Ecosystem Combinations

> This package works great standalone. It becomes **significantly more powerful** when combined with other Muonroi packages.

### + Muonroi.Core → Core provides the implementations; Abstractions defines what to inject

Always depend on Abstractions in your business logic.

```csharp
public class MyService(IMDateTimeService timeService) { ... }
```

### + Muonroi.Tenancy → ISystemExecutionContext (from Core.Abstractions) carries tenant context

Provides the interface for fetching the current tenant context.

```csharp
public void Run(ISystemExecutionContext context) { ... }
```

### + Muonroi.Mediator → IMTraceContext flows through the mediator pipeline

Allows distributed tracing across mediator boundaries.

```csharp
public Task<Response> Handle(Request req, CancellationToken ct) { ... }
```

### + All Muonroi Packages → Every Muonroi package depends on Core.Abstractions

This is the true root of the ecosystem.

```csharp
// Implied in all packages.
```

### Full Ecosystem Stack Example

```csharp
// Depend on Abstractions in your domain, register Core in Program.cs
```

## Installation

Install via the .NET CLI:

```bash
dotnet add package Muonroi.Core.Abstractions
```

Or via the Package Manager Console:

```powershell
Install-Package Muonroi.Core.Abstractions
```

Alternatively, add it directly to your `.csproj`:

```xml
<PackageReference Include="Muonroi.Core.Abstractions" Version="1.0.0" />
```

## Quick Start

Here is a minimal, complete working example of how to configure and use `Muonroi.Core.Abstractions` in your application.

### Step 1: Service Registration

In your `Program.cs` or `Startup.cs`, register the services:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using System;

var builder = WebApplication.CreateBuilder(args);

// Register the services with default options
builder.Services.AddMuonroiCoreAbstractions(options => 
{
    options.EnableDiagnostics = true;
    options.DefaultTimeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
```

### Step 2: Usage

Inject the required interfaces into your classes:

```csharp
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SampleApp.Services
{
    public class MyBusinessService
    {
        private readonly ILogger<MyBusinessService> _logger;
        
        public MyBusinessService(ILogger<MyBusinessService> logger)
        {
            _logger = logger;
        }
        
        public async Task ExecuteProcessAsync()
        {
            _logger.LogInformation("Executing process using Muonroi.Core.Abstractions components.");
            
            // Example logic
            await Task.Delay(100);
            
            _logger.LogInformation("Process completed successfully.");
        }
    }
}
```

## Configuration

The package supports robust configuration through the Standard .NET `IOptions` pattern. You can bind configurations from `appsettings.json` or environment variables.

### appsettings.json Example

```json
{
  "Muonroi_CoreAbstractions": {
    "EnableDiagnostics": true,
    "DefaultTimeoutSeconds": 30,
    "RetryCount": 3,
    "AdvancedSettings": {
      "BufferPoolSize": 1024,
      "UseNativeAOT": false
    }
  }
}
```

### Binding Configuration

```csharp
builder.Services.Configure<CoreAbstractionsOptions>(
    builder.Configuration.GetSection("Muonroi_CoreAbstractions"));
```

## API Reference

### Core Types

- `IMuonroiCoreAbstractionsService`: The primary contract for interacting with this package. Provides methods to execute subsystem logic.
- `CoreAbstractionsOptions`: Configuration model representing the options available for tuning the subsystem.
- `MuonroiException`: Base exception thrown by the package for known error states.

### Common Methods

```csharp
/// <summary>
/// Initializes the subsystem.
/// </summary>
Task InitializeAsync(CancellationToken cancellationToken = default);

/// <summary>
/// Executes a standard operation.
/// </summary>
Task<OperationResult> ExecuteAsync(RequestContext context, CancellationToken cancellationToken = default);
```

## Samples

Check out our samples repository to see this package in action:
- [Quickstart.RuleEngine](../../samples/Quickstart.RuleEngine)
- [MultiTenantSaaS](../../samples/MultiTenantSaaS)
- [Quickstart.DecisionTable](../../samples/Quickstart.DecisionTable)

## Advanced Usage

### Customizing Behaviors

You can override default behaviors by providing custom implementations of key interfaces.

```csharp
public class CustomPolicyProvider : IPolicyProvider
{
    public Task<Policy> GetPolicyAsync(string name)
    {
        // Custom implementation
        return Task.FromResult(new Policy());
    }
}

// Registration
builder.Services.AddSingleton<IPolicyProvider, CustomPolicyProvider>();
```

### Handling Multi-Tenancy

If your application is multi-tenant, ensure that tenant context is established before calling scoped services.

```csharp
public async Task ProcessTenantDataAsync(ITenantContext tenantContext)
{
    using var scope = _logger.BeginScope(new System.Collections.Generic.Dictionary<string, object>
    {
        ["TenantId"] = tenantContext.TenantId
    });
    
    // Scoped operations executed here will automatically inherit the tenant context
}
```

## Diagnostics & Observability

This package emits OpenTelemetry metrics and traces.

### Metrics

- `muonroi.coreabstractions.operations.count`: Total operations performed.
- `muonroi.coreabstractions.operations.duration`: Histogram of operation latencies in milliseconds.
- `muonroi.coreabstractions.operations.errors`: Counter for exceptions and failures.

### Traces

Traces are emitted under the activity source name `Muonroi.Core.Abstractions`. Span names follow semantic conventions and typically include the operation name.

## FAQ

**Q: Is this package safe to use in native AOT?**
A: Yes, we have designed the core abstractions to be trim-friendly and AOT-compatible where possible. Ensure you use the source-generated variants of reflection-heavy APIs if applicable.

**Q: How does this interact with `Muonroi.Core`?**
A: It builds upon the foundational types defined in `Muonroi.Core` and uses the standard exception hierarchy.

**Q: Can I use this outside the Muonroi ecosystem?**
A: While designed for the ecosystem, the components are modular. You can use them standalone, provided you register the necessary base dependencies.

## Contributing

Please see the repository root `CONTRIBUTING.md` for guidelines on submitting PRs. Make sure to run all unit tests before creating a pull request.

## Support

For issues, please file a ticket in the internal JIRA board under the `ARCH` project. Include trace IDs and error logs when possible.

## License

Apache 2.0 — see [LICENSE-APACHE](../../LICENSE-APACHE).
</Muonroi.Core.Abstractions>
