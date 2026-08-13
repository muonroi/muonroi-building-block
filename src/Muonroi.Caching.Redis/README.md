# Muonroi.Caching.Redis

## Description
Redis-backed distributed caching implementation for Muonroi applications.

## Features
- StackExchange.Redis integration.
- Distributed cache support for multi-instance deployments.
- Redis-specific caching features (e.g., tagging).

## Usage
```csharp
builder.Services.AddMuonroiRedisCache(options => { ... });
```
