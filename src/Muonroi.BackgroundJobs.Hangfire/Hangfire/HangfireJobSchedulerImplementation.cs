namespace Muonroi.BackgroundJobs.Hangfire.Hangfire;

/// <summary>
/// Hangfire implementation of <see cref="IBackgroundJobScheduler"/>.
/// </summary>
/// <param name="jobClient">The Hangfire background job client.</param>
/// <param name="recurringJobManager">The Hangfire recurring job manager.</param>
public sealed class HangfireJobScheduler(
    IBackgroundJobClient jobClient,
    IRecurringJobManager recurringJobManager) : IBackgroundJobScheduler
{
    /// <inheritdoc />
    public string Enqueue<T>(Expression<Func<T, Task>> methodCall)
    {
        return jobClient.Enqueue(methodCall);
    }

    /// <inheritdoc />
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt)
    {
        return jobClient.Schedule(methodCall, enqueueAt);
    }

    /// <inheritdoc />
    public void AddOrUpdateRecurring<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression)
    {
        recurringJobManager.AddOrUpdate(recurringJobId, methodCall, cronExpression);
    }

    /// <inheritdoc />
    public void RemoveRecurring(string recurringJobId)
    {
        recurringJobManager.RemoveIfExists(recurringJobId);
    }
}
