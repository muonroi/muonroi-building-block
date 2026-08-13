# Muonroi.Caching.Memory

## Description
In-memory caching provider implementation for the Muonroi Caching abstractions.

## Features
- `IMemoryCache` backed implementation.
- Fast, local caching for single-instance apps.
- Configurable expiration policies.

## Usage
```csharp
builder.Services.AddMuonroiMemoryCache();
```
