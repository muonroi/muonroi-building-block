# Muonroi.BackgroundJobs.Hangfire

> Hangfire provider for the Muonroi background-jobs rail — wires `IBackgroundJobScheduler` to Hangfire with automatic execution-context restoration per job run.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BackgroundJobs.Hangfire.svg)](https://www.nuget.org/packages/Muonroi.BackgroundJobs.Hangfire/)
[![License: Commercial](https://img.shields.io/badge/license-Commercial-blue.svg)](../../LICENSE-COMMERCIAL)

This package implements the `IBackgroundJobScheduler` abstraction (defined in `Muonroi.BackgroundJobs.Abstractions`) using Hangfire as the backing engine. It registers itself as a provider at module load time, so `AddBackgroundJobs(configuration)` dispatches to Hangfire automatically when `BackgroundJobConfigs.JobType` is set to `Hangfire`. A built-in `JobContextActivatorFilter` restores the Muonroi execution context (tenant ID, user, correlation ID, permissions) for every job before it runs.

## Installation

```bash
dotnet add package Muonroi.BackgroundJobs.Hangfire --prerelease
```

> **License required.** This is a commercial package. A valid Muonroi commercial license must be accepted at install time (`PackageRequireLicenseAcceptance = true`).

## Quick Start

```csharp
// Program.cs — based on samples/Quickstart.BackgroundJobs
using Hangfire;
using Hangfire.MemoryStorage;            // swap for SqlServer/Postgres in production
using Muonroi.BackgroundJobs.Abstractions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// 1. Configure Hangfire storage (before AddBackgroundJobs).
builder.Services.AddHangfire(config =>
    config.UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings()
          .UseMemoryStorage());          // production: UseSqlServerStorage(cs) etc.

// 2. Register the Muonroi BackgroundJobs rail.
//    Reads "BackgroundJobConfigs" section; dispatches to the Hangfire provider.
builder.Services.AddBackgroundJobs(builder.Configuration);

// 3. Register your job classes (Hangfire resolves them from DI).
builder.Services.AddTransient<ReportEmailJob>();
builder.Services.AddTransient<DataCleanupJob>();

WebApplication app = builder.Build();

// 4. Mount Hangfire middleware.
app.UseHangfireDashboard("/hangfire");
app.UseHangfireServer();

app.Run();
```

Inject `IBackgroundJobScheduler` wherever you need to dispatch work:

```csharp
public class OrderService(IBackgroundJobScheduler jobs)
{
    public async Task PlaceOrderAsync(Order order)
    {
        // Fire-and-forget
        jobs.Enqueue<InvoiceJob>(j => j.SendAsync(order.Id));

        // Delayed one-shot
        jobs.Schedule<ArchiveJob>(
            j => j.RunAsync(order.Id),
            DateTimeOffset.UtcNow.AddDays(30));

        // Recurring
        jobs.AddOrUpdateRecurring<DataCleanupJob>(
            "nightly-cleanup",
            j => j.RunAsync(),
            Cron.Daily());
    }
}
```

**appsettings.json**

```json
{
  "BackgroundJobConfigs": {
    "JobType": "Hangfire",
    "ConnectionString": null
  }
}
```

## Features

- Implements `IBackgroundJobScheduler` via Hangfire (`IBackgroundJobClient` + `IRecurringJobManager`).
- **Fire-and-forget** — `Enqueue<T>(Expression<Func<T, Task>>)`.
- **Delayed one-shot** — `Schedule<T>(Expression<Func<T, Task>>, DateTimeOffset)`.
- **Recurring (CRON)** — `AddOrUpdateRecurring<T>(id, expression, cronExpression)`.
- **Cancel recurring** — `RemoveRecurring(id)`.
- **Automatic retry** — 3 attempts with delays of 5 s / 10 s / 30 s, configured globally via `AutomaticRetryAttribute`.
- **Execution-context restoration** — `JobContextActivatorFilter` (auto-registered as an `IServerFilter`) extracts `IMuonroiJobExecutionContext` from job arguments and restores tenant ID, user ID, correlation ID, access token, permissions, and log scope before `OnPerforming`, disposes all scopes in `OnPerformed`.
- **Provider registration via `[ModuleInitializer]`** — loading this assembly calls `BackgroundJobHandler.RegisterProvider(JobType.Hangfire, ...)` automatically; no explicit provider wiring needed.
- **Hangfire Dashboard** — expose at any path via `app.UseHangfireDashboard("/hangfire")`.

## Configuration

`AddBackgroundJobs(IConfiguration)` reads the `BackgroundJobConfigs` section:

| Key | Type | Default | Purpose |
|-----|------|---------|---------|
| `BackgroundJobConfigs:JobType` | `JobType` enum | `Hangfire` | Must be `Hangfire` for this package; throws `MConfigurationException` otherwise. |
| `BackgroundJobConfigs:ConnectionString` | `string?` | `null` | Passed to your Hangfire storage setup (not consumed by this package directly — configure storage via `AddHangfire`). |

The section name constant is `BackgroundJobConfigs.SectionName` (`"BackgroundJobConfigs"`).

## API Reference

| Type | Purpose |
|------|---------|
| `BackgroundJobHandler` (static) | Extension class hosting `AddBackgroundJobs(IServiceCollection, IConfiguration)` — the single DI entry point. |
| `HangfireJobScheduler` | `IBackgroundJobScheduler` implementation backed by `IBackgroundJobClient` and `IRecurringJobManager`. |
| `JobContextActivatorFilter` | Hangfire `IServerFilter` that restores `ISystemExecutionContext` (tenant, user, correlation) before each job executes. |
| `BackgroundJobConfigs` | Options POCO bound from the `BackgroundJobConfigs` appsettings section; exposes `JobType` and `ConnectionString`. |

## Samples

- [Quickstart.BackgroundJobs](../../samples/Quickstart.BackgroundJobs/) — end-to-end demo: fire-and-forget, delayed, recurring, cancel recurring, tenant-aware jobs, plain jobs, and the Hangfire Dashboard.

## Compatibility

- Target framework: `net8.0`
- License: Commercial — requires activation (see [LICENSE-COMMERCIAL](../../LICENSE-COMMERCIAL))

## Related Packages

- [`Muonroi.BackgroundJobs.Abstractions`](../Muonroi.BackgroundJobs.Abstractions/) — `IBackgroundJobScheduler`, `IMuonroiJobExecutionContext`, `JobType`, and the provider-dispatch mechanism. Required peer dependency.

## License

This package is distributed under the **Muonroi Commercial License**. License acceptance is required at install time. Contact [leanhphi1706@gmail.com](mailto:leanhphi1706@gmail.com) for licensing details.
