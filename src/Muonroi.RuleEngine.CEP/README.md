# Muonroi.RuleEngine.CEP

## Description
Complex Event Processing (CEP) extensions for the Muonroi Rule Engine, enabling stateful evaluation of streams of events over time.

## Features
- Temporal reasoning and sliding windows.
- Pattern matching across sequences of events.
- Highly optimized event stream processing.

## Minimal Usage
```csharp
var cepEngine = serviceProvider.GetRequiredService<ICepEngine>();
cepEngine.ProcessEvent(new TemperatureReading { Value = 100 });
```
