# Quickstart.Templating
> Demonstrates the abstraction and integration of the Scriban templating engine.

## What This Sample Demonstrates
- Registering the `ITemplateEngine` abstraction with the Scriban implementation using `AddScribanTemplating()`.
- Injecting `ITemplateEngine` into an endpoint.
- Rendering a user-provided template with a dynamic JSON model.

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.Templating/src/Quickstart.Templating.Api
dotnet run
```

Then open [http://localhost:5000/swagger](http://localhost:5000/swagger).

You can test the `POST /render` endpoint with a payload like:

```json
{
  "template": "<h1>Hello {{ Model.Name }}!</h1><p>Your score is {{ Model.Score }}.</p>",
  "model": {
    "Name": "Alice",
    "Score": 95
  }
}
```

## Key Files
- `Program.cs` — service registration and rendering endpoint

## How It Works
The `Muonroi.Templating.Abstractions` package provides a unified `ITemplateEngine` interface for templating. The `Muonroi.Templating.Scriban` package implements this interface using the fast and secure Scriban text templating language. This allows you to decouple your application code from a specific template engine implementation.
