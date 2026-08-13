# Muonroi.Observability

## Description
A comprehensive observability package providing logging, metrics, and distributed tracing for Muonroi applications.

## Features
- OpenTelemetry integration out-of-the-box.
- Standardized logging configuration.
- Application performance monitoring (APM) hooks.

## Minimal Usage
```csharp
services.AddMuonroiObservability(options => 
{
    options.EnableTracing = true;
    options.EnableMetrics = true;
});
```
