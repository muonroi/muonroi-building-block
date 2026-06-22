# Muonroi.BackgroundJobs.Abstractions

> Contracts and DI wiring for Muonroi background job scheduler integrations — provider-agnostic interfaces that work with Hangfire or Quartz interchangeably.

[![NuGet](https://img.shields.io/nuget/v/Muonroi.BackgroundJobs.Abstractions.svg)](https://www.nuget.org/packages/Muonroi.BackgroundJobs.Abstractions/)
[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-green.svg)](https://github.com/muonroi/muonroi-building-block/blob/main/LICENSE-APACHE)

This package ships the shared contracts for the Muonroi background job subsystem: `IBackgroundJobScheduler`, `IMuonroiJobExecutionContext`, `TenantAwareJobBase`, and the `AddBackgroundJobs` DI extension. It contains **no runtime behavior** — the actual job processing is provided by the implementation packages (`Muonroi.BackgroundJobs.Hangfire` or `Muonroi.BackgroundJobs.Quartz`). Reference this package when authoring job classes or libraries that must remain provider-agnostic.

## Installation

```bash
dotnet add package Muonroi.BackgroundJobs.Abstractions --prerelease
```

For a complete setup, also add the provider package:

```bash
dotnet add package Muonroi.BackgroundJobs.Hangfire --prerelease
# or
dotnet add package Muonroi.BackgroundJobs.Quartz --prerelease
```

## Quick Start

This package is a contracts-only library. The runtime wiring is performed by the provider package's `[ModuleInitializer]`, which calls `BackgroundJobHandler.RegisterProvider` before `AddBackgroundJobs` is invoked.

**1. Register in `Program.cs`** (provider package already referenced):

```csharp
// Muonroi.BackgroundJobs.Hangfire's [ModuleInitializer] registers itself automatically.
// AddBackgroundJobs reads "BackgroundJobConfigs" from appsettings.json and dispatches
// to the registered provider.
builder.Services.AddBackgroundJobs(builder.Configuration);

// Register job classes — Hangfire resolves them from the DI container.
builder.Services.AddTransient<DataCleanupJob>();
builder.Services.AddTransient<ReportEmailJob>();
```

**2. `appsettings.json` configuration section:**

```json
{
  "BackgroundJobConfigs": {
    "JobType": "Hangfire",
    "ConnectionString": null
  }
}
```

**3. Plain job (no tenant context needed):**

```csharp
public sealed class DataCleanupJob(ILogger<DataCleanupJob> logger)
{
    public Task RunAsync()
    {
        logger.LogInformation("[DataCleanupJob] Starting cleanup at {UtcNow:O}", DateTimeOffset.UtcNow);
        // domain work here
        return Task.CompletedTask;
    }
}
```

**4. Tenant-aware job (inherits `TenantAwareJobBase`):**

```csharp
public sealed class ReportEmailJob(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    ILogger<ReportEmailJob> logger)
    : TenantAwareJobBase(executionContextAccessor, tenantContextPolicy)
{
    protected override Task ExecuteAsync()
    {
        ISystemExecutionContext ctx = ExecutionContextAccessor.Get();
        logger.LogInformation("[ReportEmailJob] TenantId={TenantId}", ctx.TenantId);
        return Task.CompletedTask;
    }
}
```

**5. Schedule jobs via `IBackgroundJobScheduler`:**

```csharp
public class JobsController(IBackgroundJobScheduler scheduler) : ControllerBase
{
    // Fire-and-forget
    [HttpPost("enqueue")]
    public IActionResult Enqueue()
    {
        string id = scheduler.Enqueue<DataCleanupJob>(j => j.RunAsync());
        return Ok(new { jobId = id });
    }

    // Delayed one-shot
    [HttpPost("schedule")]
    public IActionResult Schedule()
    {
        string id = scheduler.Schedule<DataCleanupJob>(
            j => j.RunAsync(),
            DateTimeOffset.UtcNow.AddMinutes(5));
        return Ok(new { jobId = id });
    }

    // Recurring (CRON)
    [HttpPost("recurring")]
    public IActionResult Recurring()
    {
        scheduler.AddOrUpdateRecurring<DataCleanupJob>(
            "daily-cleanup",
            j => j.RunAsync(),
            "0 2 * * *");          // every day at 02:00 UTC
        return NoContent();
    }

    // Cancel recurring
    [HttpDelete("recurring/{id}")]
    public IActionResult RemoveRecurring(string id)
    {
        scheduler.RemoveRecurring(id);
        return NoContent();
    }
}
```

## Features

- `IBackgroundJobScheduler` — unified interface for fire-and-forget, delayed, and recurring jobs across providers
- `TenantAwareJobBase` — abstract base class that automatically restores `IMuonroiJobExecutionContext` (tenant ID, user ID, correlation ID) before executing domain logic
- `IMuonroiJobExecutionContext` / `MuonroiJobExecutionContext` — job-scoped execution context carrying tenant, user, and scheduling metadata
- `BackgroundJobHandler.AddBackgroundJobs` — single DI extension that reads `BackgroundJobConfigs` and dispatches to the registered provider
- `BackgroundJobHandler.RegisterProvider` — compile-time delegate registry used by provider packages; no reflection, AOT-safe
- `JobProviderRegistration` delegate — typed contract for provider self-registration
- `JobType` enum — `Hangfire` | `Quartz`
- `BackgroundJobConfigs` — strongly-typed options bound from the `BackgroundJobConfigs` configuration section

## Configuration

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `BackgroundJobConfigs:JobType` | `JobType` | `Hangfire` | Selects the active provider |
| `BackgroundJobConfigs:ConnectionString` | `string?` | `null` | Passed through to the provider for storage setup |

`AddBackgroundJobs` throws `MConfigurationException` if `JobType` has no registered provider (e.g. the provider package is not referenced).

## API Reference

| Type | Purpose |
|------|---------|
| `IBackgroundJobScheduler` | Schedule fire-and-forget, delayed, and recurring jobs |
| `TenantAwareJobBase` | Base class for jobs requiring tenant/user context restoration |
| `IMuonroiJobExecutionContext` | Extends `ISystemExecutionContext` with `JobId`, `JobType`, `ScheduledAt` |
| `MuonroiJobExecutionContext` | Default sealed implementation of `IMuonroiJobExecutionContext` |
| `BackgroundJobHandler` | Static DI helper — `AddBackgroundJobs` extension + `RegisterProvider` |
| `JobProviderRegistration` | `delegate IServiceCollection(IServiceCollection, IConfiguration)` — provider contract |
| `BackgroundJobConfigs` | Options class bound from `"BackgroundJobConfigs"` section |
| `JobType` | Enum: `Hangfire`, `Quartz` |

## Samples

- [Quickstart.BackgroundJobs](../../samples/Quickstart.BackgroundJobs/) — full end-to-end demo: Hangfire in-memory storage, fire-and-forget, delayed, recurring, tenant-aware jobs, and Hangfire dashboard

## Compatibility

- Target framework: `net8.0`
- License: Apache-2.0 (OSS)

## Related Packages

- [`Muonroi.BackgroundJobs.Hangfire`](../Muonroi.BackgroundJobs.Hangfire/) — Hangfire provider implementation; references this package and registers itself via `[ModuleInitializer]`
- [`Muonroi.BackgroundJobs.Quartz`](../Muonroi.BackgroundJobs.Quartz/) — Quartz.NET provider implementation
- [`Muonroi.Core.Abstractions`](../Muonroi.Core.Abstractions/) — provides `ISystemExecutionContext`, `ISystemExecutionContextAccessor`, and `ITenantContextPolicy` used by `TenantAwareJobBase`
- [`Muonroi.Tenancy.Core`](../Muonroi.Tenancy.Core/) — tenant resolution primitives

## License

Apache-2.0. See [LICENSE-APACHE](../../LICENSE-APACHE) for details.
