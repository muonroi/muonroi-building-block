# Muonroi.Resilience

## Description
Provides resilience and fault-tolerance patterns (retries, circuit breakers, timeouts) for the Muonroi ecosystem, leveraging Polly.

## Features
- Standardized retry and circuit breaker policies.
- Seamless integration with `HttpClientFactory`.
- Distributed caching fallback mechanisms.

## Minimal Usage
```csharp
services.AddHttpClient("MyClient")
        .AddMuonroiResiliencePolicies();
```
