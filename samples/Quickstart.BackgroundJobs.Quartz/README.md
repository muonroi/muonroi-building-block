# Quickstart.BackgroundJobs.Quartz
> Demonstrates canonical background job scheduling using Quartz.NET.

## What This Sample Demonstrates
- IBackgroundJobScheduler registration with Quartz
- Scheduling a tenant-aware background job (TenantAwareJobBase)
- In-memory storage for Quartz

## Prerequisites
- .NET 8 SDK

## Run

```bash
cd samples/Quickstart.BackgroundJobs.Quartz/src/Quickstart.BackgroundJobs.Quartz.Api
dotnet run
```

Then open:
- API/Swagger: http://localhost:5000/swagger

## Key Files
- `Program.cs` — Quartz and scheduler registration
- `Jobs/SampleTenantJob.cs` — Tenant-aware job example
- `Controllers/JobsController.cs` — API endpoint triggering the job

## How It Works
The `Muonroi.BackgroundJobs.Quartz` package registers its provider with `BackgroundJobHandler`. When `JobType` is configured to `Quartz`, the `QuartzContextJobListener` ensures `TenantAwareJobBase` contexts are correctly restored across executions.
