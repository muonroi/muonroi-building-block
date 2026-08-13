# Muonroi.Caching.Abstractions

## Description
Core abstractions for distributed and in-memory caching in Muonroi applications.

## Features
- Standardized `ICacheService` interface.
- Cache invalidation strategies.
- Cache entry configuration options.

## Usage
```csharp
public class MyService(ICacheService cache) { ... }
```
