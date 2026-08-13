# Muonroi.AspNetCore.OpenApi

## Description
Provides OpenAPI (Swagger) integration and configuration for Muonroi ASP.NET Core applications.

## Features
- Automatic Swagger documentation generation.
- Pre-configured security definitions (e.g., Bearer auth).
- UI customization options.

## Usage
```csharp
builder.Services.AddMuonroiOpenApi(options => { ... });
app.UseMuonroiOpenApi();
```
