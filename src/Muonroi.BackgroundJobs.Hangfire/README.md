# Muonroi.BackgroundJobs.Hangfire

## Description
Hangfire integration for the Muonroi Background Jobs building block.

## Features
- Hangfire-backed job execution.
- Dashboard configuration.
- Storage extensions (SQL, Redis, etc.).

## Usage
```csharp
builder.Services.AddMuonroiHangfire();
app.UseMuonroiHangfireDashboard();
```
