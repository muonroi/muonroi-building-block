# Muonroi.Integration.Abstractions

> Contracts-only package for the Muonroi Connector Registry — defines the interfaces and data types that every integration connector must implement.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Integration.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.Integration.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships **contracts only** — interfaces, sealed records, and DTOs that define the shape of the Muonroi Connector Registry. It contains no runtime behavior. The implementation package [`Muonroi.Integration.Connectors`](../Muonroi.Integration.Connectors/) provides built-in connectors (HTTP, SMTP, Slack, SQL, Redis, and a growing set of SaaS presets) and the `DefaultConnectorRegistry`.

Reference this package when:
- Building a **custom connector** that plugs into the registry alongside the built-ins.
- Consuming `IConnectorRegistry` or `IConnectorCredentialStore` from a host that already registers implementations.
- Authoring a test double or alternative implementation for any registry contract.

## Installation

```bash
dotnet add package Muonroi.Integration.Abstractions --prerelease
```

## Quick Start

### Implementing a custom connector

Implement `IServiceTaskConnector` and register it as a singleton alongside the built-ins (the `DefaultConnectorRegistry` scans every `IServiceTaskConnector` registration at startup):

```csharp
using System.Text.Json;
using Muonroi.Integration.Abstractions;

public sealed class GitHubConnector(IHttpClientFactory httpClientFactory) : IServiceTaskConnector
{
    public ConnectorMetadata Metadata => new()
    {
        Type        = "github",
        DisplayName = "GitHub API",
        Category    = "DevOps",
        IconSvg     = "<path d=\"...\"/>",
        Description = "Interact with the GitHub REST API.",
        RequiresCredentials = true,
        FieldSchema =
        [
            new ConnectorFieldDescriptor { Key = "action", Label = "Action", FieldType = "text", Required = true }
        ]
    };

    public async Task<ConnectorResult> ExecuteAsync(ConnectorContext context, CancellationToken ct)
    {
        string? token = context.Credentials.GetValueOrDefault("token");
        if (string.IsNullOrEmpty(token))
            return ConnectorResult.Fail("GitHub PAT is required.");

        // ... call GitHub REST API using context.Config for operation parameters
        return ConnectorResult.Ok(new() { ["githubLogin"] = "octocat" });
    }

    public Task<bool> TestConnectionAsync(ConnectorContext context, CancellationToken ct)
    {
        // verify credentials, return true on success
        return Task.FromResult(true);
    }

    public JsonElement GetConfigSchema()
    {
        return JsonDocument.Parse("""{"type":"object","properties":{"action":{"type":"string"}}}""")
                           .RootElement.Clone();
    }
}
```

Register it next to the built-ins from `Muonroi.Integration.Connectors`:

```csharp
// Program.cs
builder.Services.AddMBuiltInConnectors();                          // from Muonroi.Integration.Connectors
builder.Services.AddSingleton<IServiceTaskConnector, GitHubConnector>();
```

Resolve connectors at runtime through `IConnectorRegistry`:

```csharp
app.MapGet("/health", (IConnectorRegistry registry) => Results.Ok(new
{
    RegisteredConnectors = registry.ListAvailable().Select(m => m.Type)
}));
```

## Features

- `IServiceTaskConnector` — core connector contract: execute, test-connection, config schema, document browse, and document fetch (last three have safe defaults that return `null`).
- `IConnectorRegistry` — resolve a connector by type key or enumerate all registered connectors with their metadata.
- `IConnectorCredentialStore` — per-tenant, encrypted-at-rest credential CRUD (get, save, delete).
- `IConnectorConfigStore` — CRUD for named connector configuration instances (`ConnectorConfigDto`), scoped to tenant and owner.
- `ConnectorContext` — immutable execution context: JSON config, `FactBag` input facts, resolved credentials, tenant ID, correlation ID.
- `ConnectorResult` — uniform result envelope with `Ok(...)` / `Fail(...)` static factories, output facts, status code, and duration.
- `ConnectorMetadata` — UI catalog descriptor: type key, display name, category, SVG icon, field schema, credential fields, and auth builder key.
- `ConnectorResilienceConfig` — declarative Polly resilience parameters: retry count/delay, timeout, and circuit-breaker thresholds.
- Browse API — `ConnectorBrowseQuery`, `ConnectorBrowseResult`, `ConnectorBrowseItem`, `ConnectorScope`, `ConnectorDocumentContent` for optional document-discovery and ingestion flows.

## Configuration

This package defines `ConnectorResilienceConfig`, which the implementation package reads from options:

```json
{
  "ConnectorResilience": {
    "RetryCount": 3,
    "RetryDelay": "00:00:01",
    "Timeout": "00:00:30",
    "CircuitBreakerThreshold": 0.5,
    "CircuitBreakerSamplingDuration": "00:00:30",
    "CircuitBreakerMinimumThroughput": 5,
    "CircuitBreakerBreakDuration": "00:00:30"
  }
}
```

## API Reference

| Type | Purpose |
|------|---------|
| `IServiceTaskConnector` | Core connector contract — `ExecuteAsync`, `TestConnectionAsync`, `GetConfigSchema`, and optional `ListDocumentsAsync` / `FetchDocumentAsync` / `ListScopesAsync` |
| `IConnectorRegistry` | Resolve a connector by `string` type key or list all via `ListAvailable()` |
| `IConnectorCredentialStore` | Per-tenant encrypted credential store — `GetAsync`, `SaveAsync`, `DeleteAsync` |
| `IConnectorConfigStore` | Connector configuration CRUD — `GetByIdAsync`, `ListAsync`, `SaveAsync`, `DeleteAsync` |
| `ConnectorContext` | Immutable execution context: `Config` (JsonDocument), `InputFacts` (FactBag), `Credentials`, `TenantId`, `CorrelationId` |
| `ConnectorResult` | Result envelope: `Ok(outputFacts, statusCode, duration)` / `Fail(error, statusCode, duration)` |
| `ConnectorMetadata` | UI descriptor: `Type`, `DisplayName`, `Category`, `IconSvg`, `FieldSchema`, `CredentialFields`, `AuthBuilder` |
| `ConnectorFieldDescriptor` | Individual form-field descriptor used in `ConnectorMetadata.FieldSchema` and `CredentialFields` |
| `ConnectorResilienceConfig` | Polly retry/timeout/circuit-breaker settings consumed by the implementation package |
| `ConnectorConfigDto` | DTO for a named connector configuration instance, including `ConnectorType`, `Name`, `ConfigJson`, `CredentialId`, `OwnerId` |
| `ConnectorBrowseQuery` | Typed input to `ListDocumentsAsync` — `SearchText`, `Scope`, `TypeFilter`, `Cursor`, `PageSize` |
| `ConnectorBrowseResult` | Paged result from `ListDocumentsAsync` — list of `ConnectorBrowseItem` plus next-page cursor |
| `ConnectorBrowseItem` | Single discoverable document: external reference, title, type, and URL |
| `ConnectorScope` | Scope unit for narrowing browse results (e.g. Jira project key, Confluence space key) — `Id`, `Label` |
| `ConnectorDocumentContent` | Raw document body returned by `FetchDocumentAsync` — body text and normalizer format key |

## Samples

- [Quickstart.Integration](../../samples/Quickstart.Integration/) — ASP.NET Core API demonstrating `IConnectorRegistry`, built-in connectors, and a custom `GitHubConnector` implementation.
- [Quickstart.Integration.Persistence](../../samples/Quickstart.Integration.Persistence/) — demonstrates `IConnectorConfigStore` and `IConnectorCredentialStore` with the persistence implementation.

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Integration.Connectors`](../Muonroi.Integration.Connectors/) — built-in connector implementations (HTTP, SMTP, Slack, SQL, Redis, Jira, Confluence, Notion, Azure DevOps, GitHub presets) and `DefaultConnectorRegistry`.
- [`Muonroi.Integration.Persistence`](../Muonroi.Integration.Persistence/) — database-backed implementations of `IConnectorCredentialStore` and `IConnectorConfigStore`.
- [`Muonroi.RuleEngine.Abstractions`](../Muonroi.RuleEngine.Abstractions/) — provides `FactBag` used in `ConnectorContext.InputFacts`.

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
