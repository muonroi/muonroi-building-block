using System.Linq.Expressions;

namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Unified interface for scheduling background jobs in Muonroi ecosystem.
/// Automatically handles context capture and propagation.
/// </summary>
public interface IBackgroundJobScheduler
{
    /// <summary>
    /// Enqueues a job for immediate execution.
    /// </summary>
    /// <typeparam name="T">The job type.</typeparam>
    /// <param name="methodCall">The expression representing the job method.</param>
    /// <returns>The unique identifier of the created job.</returns>
    string Enqueue<T>(Expression<Func<T, Task>> methodCall);

    /// <summary>
    /// Schedules a job for execution at a specific time.
    /// </summary>
    /// <typeparam name="T">The job type.</typeparam>
    /// <param name="methodCall">The expression representing the job method.</param>
    /// <param name="enqueueAt">The time at which the job should be executed.</param>
    /// <returns>The unique identifier of the created job.</returns>
    string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt);

    /// <summary>
    /// Adds or updates a recurring job.
    /// </summary>
    /// <typeparam name="T">The job type.</typeparam>
    /// <param name="recurringJobId">A unique identifier for the recurring job.</param>
    /// <param name="methodCall">The expression representing the job method.</param>
    /// <param name="cronExpression">The CRON expression.</param>
    void AddOrUpdateRecurring<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression);

    /// <summary>
    /// Removes a recurring job.
    /// </summary>
    /// <param name="recurringJobId">The unique identifier of the recurring job.</param>
    void RemoveRecurring(string recurringJobId);
}
