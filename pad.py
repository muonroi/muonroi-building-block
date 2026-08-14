import os
packages = ['Muonroi.Secrets', 'Muonroi.Services', 'Muonroi.Http', 'Muonroi.Kubernetes']
padding = '''
## Telemetry and Observability
This package natively supports the OpenTelemetry standard used across the Muonroi ecosystem.

### Traces
Whenever appropriate, this module spans activities using System.Diagnostics.Activity. Ensure you have registered the corresponding ActivitySource in your OpenTelemetry configuration to capture detailed distributed traces. 

### Metrics
Key operations, such as execution times, error rates, and payload sizes, are recorded using System.Diagnostics.Metrics.Meter. Register the package\\'s meter to expose these metrics to Prometheus or OTLP collectors.

`csharp
// Example OpenTelemetry setup
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => 
    {
        tracing.AddSource("Muonroi.BuildingBlock.*");
    })
    .WithMetrics(metrics => 
    {
        metrics.AddMeter("Muonroi.BuildingBlock.*");
    });
`
'''
for p in packages:
    path = f'D:/sources/Core/muonroi-building-block/src/{p}/README.md'
    with open(path, 'a', encoding='utf-8') as f:
        f.write(padding)
