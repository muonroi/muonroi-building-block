> Quickstart demonstrating Integration Abstractions and Connector metadata.

## What This Sample Demonstrates
- `IConnectorRegistry` for discovering available integrations
- Executing an `IServiceTaskConnector` with `ConnectorContext`
- Connector configuration and credential handling patterns

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Integration.Abstractions/src/Quickstart.Integration.Abstractions.Api
dotnet run
```

Then open http://localhost:5000/swagger.

## Key Files
- `Program.cs` — Mocks the connector registry and implements a dummy email connector
- `Controllers/ConnectorsController.cs` — Lists connectors and executes them

## How It Works
The API exposes endpoints to list all connectors (metadata like fields, auth types) and a generic endpoint to trigger a connector's `ExecuteAsync` logic using the abstraction models.
