using Microsoft.Extensions.Logging;

namespace Quickstart.BackgroundJobs.Api.Jobs;

/// <summary>
/// A plain background job — no tenant/user context required.
///
/// Demonstrates feature: scheduling a simple class (not inheriting
/// <c>TenantAwareJobBase</c>) with <see cref="Muonroi.BackgroundJobs.Abstractions.IBackgroundJobScheduler"/>.
///
/// Usage pattern:
///   - The class must be registered in the DI container (transient/scoped/singleton).
///   - Hangfire resolves the instance via the service provider before execution.
///   - The method used in the scheduler expression (<see cref="RunAsync"/>) must be
///     public so that Hangfire can serialise and invoke it.
///
/// Recurring job:
///   - Registered in <c>JobsController</c> with cron expression "0 2 * * *"
///     (every day at 02:00 UTC).
///   - Can be cancelled by calling the DELETE endpoint that calls RemoveRecurring.
/// </summary>
public sealed class DataCleanupJob(ILogger<DataCleanupJob> logger)
{
    private readonly ILogger<DataCleanupJob> _logger = logger;

    /// <summary>
    /// Performs the data cleanup operation.
    /// Called by Hangfire when the recurring schedule fires.
    /// </summary>
    public Task RunAsync()
    {
        _logger.LogInformation(
            "[DataCleanupJob] Starting scheduled data cleanup at {UtcNow:O}",
            DateTimeOffset.UtcNow);

        // ── Simulated domain work ────────────────────────────────────────────
        // In a real application this would delete soft-deleted rows, purge
        // expired sessions, compact audit logs, etc.
        _logger.LogInformation("[DataCleanupJob] Data cleanup completed successfully.");

        return Task.CompletedTask;
    }
}
