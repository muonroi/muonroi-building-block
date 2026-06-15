using Microsoft.AspNetCore.Mvc;
using Muonroi.BackgroundJobs.Abstractions;
using Quickstart.BackgroundJobs.Api.Jobs;

namespace Quickstart.BackgroundJobs.Api.Controllers;

/// <summary>
/// Demonstrates all four scheduling operations exposed by
/// <see cref="IBackgroundJobScheduler"/>:
///   1. Enqueue   — fire-and-forget, immediate execution
///   2. Schedule  — delayed, one-shot execution
///   3. AddOrUpdateRecurring — CRON-based recurring job
///   4. RemoveRecurring      — cancel a recurring job
/// </summary>
[ApiController]
[Route("api/jobs")]
public sealed class JobsController(IBackgroundJobScheduler scheduler) : ControllerBase
{
    private readonly IBackgroundJobScheduler _scheduler = scheduler;

    // ── 1. Enqueue ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enqueues a <see cref="ReportEmailJob"/> for immediate background execution.
    ///
    /// Feature: IBackgroundJobScheduler.Enqueue&lt;T&gt;
    ///
    /// The <c>null!</c> passed as the <see cref="IMuonroiJobExecutionContext"/> argument is
    /// intentional: HangfireJobScheduler captures the current ambient
    /// ISystemExecutionContextAccessor state at enqueue time and serialises it into the
    /// Hangfire job record. The actual <c>IMuonroiJobExecutionContext</c> parameter received at
    /// runtime is restored by <c>JobContextActivatorFilter</c> from that stored state —
    /// not from the null placeholder.
    /// </summary>
    [HttpPost("report/enqueue")]
    [ProducesResponseType(typeof(EnqueueResult), StatusCodes.Status202Accepted)]
    public IActionResult EnqueueReport()
    {
        string jobId = _scheduler.Enqueue<ReportEmailJob>(j => j.RunAsync(null!));

        return Accepted(new EnqueueResult(jobId, "ReportEmailJob enqueued for immediate execution."));
    }

    // ── 2. Schedule ───────────────────────────────────────────────────────────

    /// <summary>
    /// Schedules a <see cref="ReportEmailJob"/> to run after a specified delay in minutes.
    ///
    /// Feature: IBackgroundJobScheduler.Schedule&lt;T&gt;
    ///
    /// Query parameter <paramref name="delayMinutes"/> defaults to 5. The resulting
    /// Hangfire job will be visible in the Hangfire dashboard at /hangfire and will
    /// fire at the scheduled time.
    /// </summary>
    /// <param name="delayMinutes">How many minutes from now to run the job (default: 5).</param>
    [HttpPost("report/schedule")]
    [ProducesResponseType(typeof(EnqueueResult), StatusCodes.Status202Accepted)]
    public IActionResult ScheduleReport([FromQuery] int delayMinutes = 5)
    {
        DateTimeOffset runAt = DateTimeOffset.UtcNow.AddMinutes(delayMinutes);

        string jobId = _scheduler.Schedule<ReportEmailJob>(
            j => j.RunAsync(null!),
            runAt);

        return Accepted(new EnqueueResult(
            jobId,
            $"ReportEmailJob scheduled to run at {runAt:O} ({delayMinutes} minute(s) from now)."));
    }

    // ── 3. AddOrUpdateRecurring ───────────────────────────────────────────────

    /// <summary>
    /// Registers (or updates) the <see cref="DataCleanupJob"/> as a recurring job
    /// that fires every day at 02:00 UTC.
    ///
    /// Feature: IBackgroundJobScheduler.AddOrUpdateRecurring&lt;T&gt;
    ///
    /// Calling this endpoint again while the job exists will update its schedule
    /// without creating a duplicate — idempotent by design.
    ///
    /// CRON format: "0 2 * * *" = minute 0, hour 2, any day/month/weekday.
    /// </summary>
    [HttpPost("cleanup/recurring")]
    [ProducesResponseType(typeof(RecurringResult), StatusCodes.Status200OK)]
    public IActionResult RegisterCleanupRecurring()
    {
        const string RecurringJobId = "data-cleanup";
        const string CronExpression = "0 2 * * *"; // every day at 02:00 UTC

        _scheduler.AddOrUpdateRecurring<DataCleanupJob>(
            RecurringJobId,
            j => j.RunAsync(),
            CronExpression);

        return Ok(new RecurringResult(
            RecurringJobId,
            CronExpression,
            "DataCleanupJob recurring schedule registered/updated. Check /hangfire for details."));
    }

    // ── 4. RemoveRecurring ────────────────────────────────────────────────────

    /// <summary>
    /// Removes the <see cref="DataCleanupJob"/> recurring schedule.
    ///
    /// Feature: IBackgroundJobScheduler.RemoveRecurring
    ///
    /// Calling this when the job does not exist is a no-op (RemoveIfExists).
    /// </summary>
    [HttpDelete("cleanup/recurring")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult RemoveCleanupRecurring()
    {
        _scheduler.RemoveRecurring("data-cleanup");
        return NoContent();
    }

    // ── Response models ───────────────────────────────────────────────────────

    /// <summary>Response body for fire-and-forget and delayed scheduling.</summary>
    public sealed record EnqueueResult(string JobId, string Message);

    /// <summary>Response body for recurring job registration.</summary>
    public sealed record RecurringResult(string RecurringJobId, string CronExpression, string Message);
}
