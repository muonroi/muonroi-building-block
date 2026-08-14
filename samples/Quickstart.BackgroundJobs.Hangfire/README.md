# Quickstart.BackgroundJobs.Hangfire
> Demonstrates canonical background job scheduling using Hangfire.

## What This Sample Demonstrates
- IBackgroundJobScheduler registration with Hangfire
- Scheduling a tenant-aware background job (TenantAwareJobBase)
- In-memory storage for Hangfire
- Exposing the Hangfire Dashboard

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.BackgroundJobs.Hangfire/src/Quickstart.BackgroundJobs.Hangfire.Api
dotnet run
```

Then open:
- API/Swagger: http://localhost:5000/swagger
- Hangfire Dashboard: http://localhost:5000/hangfire

## Key Files
- `Program.cs` — Hangfire and scheduler registration
- `Jobs/SampleTenantJob.cs` — Tenant-aware job example
- `Controllers/JobsController.cs` — API endpoint triggering the job

## How It Works
The `Muonroi.BackgroundJobs.Hangfire` package uses a module initializer to self-register with `BackgroundJobHandler`. Calling `builder.Services.AddBackgroundJobs` automatically routes to Hangfire based on configuration.
