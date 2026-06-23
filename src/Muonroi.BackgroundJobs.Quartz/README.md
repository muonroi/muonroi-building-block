# Muonroi.BackgroundJobs.Quartz

> Quartz.NET provider for the Muonroi background-jobs abstraction — swap in Quartz with a single config change, no code rewrites.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BackgroundJobs.Quartz.svg)](https://www.nuget.org/packages/Muonroi.BackgroundJobs.Quartz/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-red.svg)](LICENSE-COMMERCIAL)

This package wires [Quartz.NET](https://www.quartz-scheduler.net/) into the `IBackgroundJobScheduler` abstraction defined by `Muonroi.BackgroundJobs.Abstractions`. It registers a Quartz hosted service, a global `QuartzContextJobListener` that restores Muonroi execution context (tenant ID, user, correlation ID, access token) before each job runs, and an `IBackgroundJobScheduler` scoped service. The assembly uses a `[ModuleInitializer]` to self-register the `JobType.Quartz` provider with `BackgroundJobHandler` at load time — no manual wiring beyond adding the package reference.

> **Note:** Expression-based scheduling (`Enqueue<T>`, `Schedule<T>`, `AddOrUpdateRecurring<T>`) is **not yet supported** in this provider. Quartz jobs must be class-based (implement `IJob`). For expression-based scheduling use `Muonroi.BackgroundJobs.Hangfire` instead.

## Installation

```bash
dotnet add package Muonroi.BackgroundJobs.Quartz --prerelease
```

## Quick Start

Add the configuration section to `appsettings.json`:

```json
{
  "BackgroundJobConfigs": {
    "JobType": "Quartz",
    "ConnectionString": null
  }
}
```

Register in `Program.cs`:

```csharp
// Reference Muonroi.BackgroundJobs.Quartz — its [ModuleInitializer] registers
// the Quartz provider with BackgroundJobHandler automatically on assembly load.
using Muonroi.BackgroundJobs.Abstractions;

builder.Services.AddBackgroundJobs(builder.Configuration);
```

Define a class-based job (Quartz `IJob`):

```csharp
using Quartz;

public sealed class DataCleanupJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        // Muonroi execution context (tenant, user, correlationId) is already
        // restored by QuartzContextJobListener before this method is called.
        return Task.CompletedTask;
    }
}
```

Schedule the job at startup using the native Quartz API (inject `ISchedulerFactory`):

```csharp
IScheduler scheduler = await schedulerFactory.GetScheduler();
IJobDetail job = JobBuilder.Create<DataCleanupJob>()
    .WithIdentity("data-cleanup")
    .Build();
ITrigger trigger = TriggerBuilder.Create()
    .WithCronSchedule("0 2 * * ?")
    .Build();
await scheduler.ScheduleJob(job, trigger);
```

## Features

- Registers `Quartz`, `Quartz.Extensions.DependencyInjection`, and `Quartz.Extensions.Hosting` with a single `AddBackgroundJobs` call.
- Validates that `BackgroundJobConfigs:JobType` is `Quartz`; throws `MConfigurationException` if misconfigured.
- `QuartzContextJobListener` restores the full Muonroi `SystemExecutionContext` (tenant ID, user ID, username, correlation ID, access token, API key, permissions) for every job execution via `IJobExecutionContext.MergedJobDataMap`.
- Quartz hosted service is configured with `WaitForJobsToComplete = true` for graceful shutdown.
- AOT-safe: provider registration uses a compile-time delegate, no reflection.
- Self-registering: merely adding the package reference causes `[ModuleInitializer]` to enroll the `JobType.Quartz` provider — no extra DI calls needed.

## Configuration

### `BackgroundJobConfigs` options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `JobType` | `JobType` | `Quartz` | Must be `Quartz` for this package. |
| `ConnectionString` | `string?` | `null` | Optional; passed through for persistence store setup if needed. |

`BackgroundJobConfigs.SectionName` is the constant `"BackgroundJobConfigs"`.

## API Reference

| Type | Purpose |
|------|---------|
| `BackgroundJobHandler` (static) | Extension class; exposes `AddBackgroundJobs(IServiceCollection, IConfiguration)` |
| `BackgroundJobConfigs` | Options POCO bound from `"BackgroundJobConfigs"` config section |
| `QuartzContextJobListener` | `IJobListener` that restores Muonroi `SystemExecutionContext` before each job |
| `QuartzJobScheduler` | Scoped `IBackgroundJobScheduler` implementation (class-based jobs only) |

## Samples

- [Quickstart.BackgroundJobs](../../samples/Quickstart.BackgroundJobs/) — demonstrates provider selection (Hangfire vs Quartz via `JobType` config), class-based job definitions, and `IBackgroundJobScheduler` usage.

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation

## Related Packages

- [`Muonroi.BackgroundJobs.Abstractions`](../Muonroi.BackgroundJobs.Abstractions/) — `IBackgroundJobScheduler`, `JobType`, and shared contracts
- [`Muonroi.BackgroundJobs.Hangfire`](../Muonroi.BackgroundJobs.Hangfire/) — Hangfire provider; supports expression-based scheduling

## License

This package is distributed under a **commercial license**. A valid Muonroi license is required for production use. See [LICENSE-COMMERCIAL](LICENSE-COMMERCIAL) for terms.
