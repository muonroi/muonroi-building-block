# Muonroi.Integration.Connectors

> Ready-to-use connector implementations for the Muonroi Connector Registry — HTTP/REST, SMTP, Slack, SQL, Redis, and a suite of third-party presets.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.Integration.Connectors.svg)](https://www.nuget.org/packages/Muonroi.Integration.Connectors/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships the concrete `IServiceTaskConnector` implementations defined in
[`Muonroi.Integration.Abstractions`](../Muonroi.Integration.Abstractions/). It registers
all connectors and the `DefaultConnectorRegistry` through a single DI extension, giving
workflows immediate access to HTTP, email, Slack, SQL, Redis, and preset integrations for
GitHub, Jira Cloud, Confluence, Azure DevOps, Notion, and Jira Server / Confluence Server.

## Installation

```bash
dotnet add package Muonroi.Integration.Connectors --prerelease
```

## Quick Start

```csharp
using Muonroi.Integration.Connectors.Registration;
using Muonroi.Integration.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Register all built-in connectors + DefaultConnectorRegistry in one call.
builder.Services.AddMBuiltInConnectors();

var app = builder.Build();

// Resolve the registry and dispatch to a connector by its type key.
app.MapGet("/ping-api", async (IConnectorRegistry registry, CancellationToken ct) =>
{
    IServiceTaskConnector? connector = registry.Resolve("http");
    if (connector is null) return Results.NotFound("connector not found");

    using JsonDocument config = JsonDocument.Parse("""{"url":"https://httpbin.org/get"}""");
    var context = new ConnectorContext
    {
        Config = config,
        InputFacts = [],
        Credentials = [],
        TenantId = "demo",
        CorrelationId = Guid.NewGuid().ToString()
    };

    ConnectorResult result = await connector.ExecuteAsync(context, ct);
    return result.Success ? Results.Ok(result.OutputFacts) : Results.Problem(result.ErrorMessage);
});

 await app.RunAsync();
```

## Features

- **HTTP / REST** (`type = "http"`) — GET, POST, PUT, DELETE, PATCH; configurable headers,
  body (Scriban template syntax), content-type, timeout, and `responseMapping` to project
  JSON response fields into the `FactBag`. Bearer and API-key auth from credentials.
- **Email / SMTP** (`type = "email"`) — Sends plain-text or HTML email via MailKit over
  STARTTLS. Server, port, and credentials supplied at runtime through `ConnectorContext`.
- **Slack Webhook** (`type = "slack"`) — Posts messages to Slack channels via incoming
  webhooks. Webhook URL sourced from credentials or connector config.
- **SQL Query** (`type = "sql"`) — Parameterized, read-only-by-default ADO.NET queries.
  Resolves `IDbConnection` from DI; write operations (`INSERT`, `UPDATE`, `DELETE`, `DROP`,
  `ALTER`, `CREATE`) are blocked unless `readOnly: false` is set explicitly.
- **Redis** (`type = "redis"`) — `GET`, `SET` (with optional TTL), and `PUBLISH` operations
  via `IConnectionMultiplexer`. Connector is a no-op when Redis is not registered in DI.
- **Third-party presets** — Delegate 100% to `HttpConnector` with pre-wired auth and field
  schemas for fast UI-driven configuration:

  | Connector type key | Display name | Auth style |
  |--------------------|--------------|-----------|
  | `jira-cloud` | Jira Cloud | Basic (email + API token) |
  | `confluence` | Confluence Cloud | Basic (email + API token) |
  | `generic-rest` | Generic REST | Configurable |
  | `jira-server` | Jira Server | PAT (Bearer) |
  | `confluence-server` | Confluence Server | PAT (Bearer) |
  | `azure-devops` | Azure DevOps | PAT (Bearer) |
  | `notion` | Notion | Bearer token |
  | `github` | GitHub | PAT (Bearer) |

- **`DefaultConnectorRegistry`** — In-memory registry built from all DI-registered
  `IServiceTaskConnector` instances. Supports `Resolve(type)` (case-insensitive) and
  `ListAvailable()`.

## Configuration

### DI registration

```csharp
services.AddMBuiltInConnectors();
```

This single call:
1. Registers a named `HttpClient` (`"MuonroiConnector"`) for all HTTP-based connectors.
2. Registers `HttpConnector`, `SmtpConnector`, `SlackWebhookConnector`, `SqlQueryConnector`,
   and `RedisConnector` as `IServiceTaskConnector`.
3. Registers all preset connectors as `IServiceTaskConnector`.
4. Registers `DefaultConnectorRegistry` as `IConnectorRegistry` (using `TryAddSingleton`).

### Per-connector configuration schema

Each connector exposes its accepted configuration via `GetConfigSchema()`, which returns a
JSON Schema `JsonElement`. The schemas below document the `ConnectorContext.Config` fields
each connector reads.

**HTTP** (`HttpConnector`)

| Field | Type | Required | Default | Notes |
|-------|------|----------|---------|-------|
| `url` | `string (uri)` | yes | — | Target URL |
| `method` | `string` | no | `"GET"` | GET, POST, PUT, DELETE, PATCH |
| `headers` | `object` | no | — | Custom HTTP headers |
| `body` | `string` | no | — | Request body; supports Scriban template syntax |
| `contentType` | `string` | no | `"application/json"` | Content-Type header |
| `responseMapping` | `object` | no | — | Maps JSON response properties to FactBag keys |
| `timeout` | `integer` | no | `30` | Timeout in seconds |

Auth is read from `ConnectorContext.Credentials`:
- `authorization` → raw `Authorization` header value
- `apiKey` + `apiKeyHeader` → custom header name + value

**Email** (`SmtpConnector`) — config fields

| Field | Type | Required | Default |
|-------|------|----------|---------|
| `to` | `string (email)` | yes | — |
| `from` | `string (email)` | no | `"noreply@muonroi.dev"` |
| `subject` | `string` | no | `""` |
| `body` | `string` | no | `""` |
| `isHtml` | `boolean` | no | `false` |

Credentials: `smtpHost`, `smtpPort`, `smtpUsername`, `smtpPassword`.

**Slack** (`SlackWebhookConnector`) — config fields

| Field | Type | Required |
|-------|------|----------|
| `text` | `string` | yes |
| `channel` | `string` | no |

Credentials: `webhookUrl` (overrides any config value).

**SQL** (`SqlQueryConnector`) — config fields

| Field | Type | Required | Default |
|-------|------|----------|---------|
| `query` | `string` | yes | — |
| `parameters` | `object` | no | — |
| `readOnly` | `boolean` | no | `true` |

Requires `IDbConnection` registered in DI. Output facts: `sqlRows` (list of row objects),
`sqlRowCount` (integer).

**Redis** (`RedisConnector`) — config fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `operation` | `string` | yes | `GET`, `SET`, or `PUBLISH` |
| `key` | `string` | yes | Redis key or channel name |
| `value` | `string` | SET only | — |
| `message` | `string` | PUBLISH only | — |
| `ttlSeconds` | `integer` | no | TTL for SET |

Requires `IConnectionMultiplexer` registered in DI.

## API Reference

| Type | Purpose |
|------|---------|
| `ConnectorRegistration.AddMBuiltInConnectors()` | DI extension — registers all connectors and `DefaultConnectorRegistry` |
| `DefaultConnectorRegistry` | In-memory `IConnectorRegistry`; built from all DI `IServiceTaskConnector` singletons |
| `HttpConnector` | HTTP / REST connector; also exposes `ReadJsonAsync` for preset use |
| `SmtpConnector` | SMTP email sender via MailKit |
| `SlackWebhookConnector` | Slack incoming webhook poster |
| `SqlQueryConnector` | Parameterized ADO.NET query executor |
| `RedisConnector` | Redis GET / SET / PUBLISH connector via StackExchange.Redis |
| `JiraCloudPresetConnector` | Jira Cloud API v3 (basic-email auth) |
| `ConfluencePresetConnector` | Confluence Cloud (basic-email auth) |
| `GenericRestPresetConnector` | Configurable REST preset |
| `JiraServerPresetConnector` | Jira Server / Data Center (PAT) |
| `ConfluenceServerPresetConnector` | Confluence Server (PAT) |
| `AzureDevOpsPresetConnector` | Azure DevOps (PAT) |
| `NotionPresetConnector` | Notion API (Bearer token) |
| `GitHubPresetConnector` | GitHub REST API (PAT); supports `ListDocumentsAsync` via `/search/code` |

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.Integration.Abstractions`](../Muonroi.Integration.Abstractions/) — Contracts: `IServiceTaskConnector`, `IConnectorRegistry`, `ConnectorContext`, `ConnectorResult`, `ConnectorMetadata`
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — Core exception and shared types used by connectors


## Ecosystem Combinations

### + Integration.Abstractions → Implements IServiceTaskConnector
Connectors are the concrete `IServiceTaskConnector` implementations — HTTP APIs, gRPC services, database adapters.

### + Resilience → Resilient Connector Calls
Every connector call is wrapped with a Polly pipeline (retry + circuit breaker + timeout) automatically when `Muonroi.Resilience` is registered.

### + Tenancy → Per-Tenant Connector Configuration
Each tenant can have different connector endpoints and credentials resolved via `ITenantConnectionStringFactory`.

### + Observability → Connector Call Tracing
Each connector execution creates an OTel span with connector type, target endpoint, and result status.

## Samples
- [`Quickstart.Integration`](../../samples/Quickstart.Integration)
- [`Quickstart.Integration.Abstractions`](../../samples/Quickstart.Integration.Abstractions)


## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE).
