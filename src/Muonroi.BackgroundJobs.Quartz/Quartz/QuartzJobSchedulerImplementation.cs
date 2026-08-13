using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;
using System.Linq.Expressions;

namespace Muonroi.BackgroundJobs.Quartz.Quartz;

/// <summary>
/// Quartz implementation of <see cref="IBackgroundJobScheduler"/>.
/// </summary>
public sealed class QuartzJobScheduler() : IBackgroundJobScheduler
{
    /// <inheritdoc/>
    public string Enqueue<T>(Expression<Func<T, Task>> methodCall)
    {
        // Quartz typically uses Class-based jobs. 
        // For Expression-based jobs, a wrapper job would be needed.
        // For now, we provide a placeholder or basic implementation.
        return MGuard.Fail<string>("Expression-based jobs are not yet supported in Quartz provider. Use Class-based jobs or Hangfire.", MErrorCodes.BackgroundJobs.ExpressionJobsNotSupported);
    }

    /// <inheritdoc/>
    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt)
    {
        return MGuard.Fail<string>("Expression-based jobs are not yet supported in Quartz provider. Use Class-based jobs or Hangfire.", MErrorCodes.BackgroundJobs.ExpressionJobsNotSupported);
    }

    /// <inheritdoc/>
    public void AddOrUpdateRecurring<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression)
    {
        _ = MGuard.Fail<object>("Expression-based jobs are not yet supported in Quartz provider. Use Class-based jobs or Hangfire.", MErrorCodes.BackgroundJobs.ExpressionJobsNotSupported);
    }

    /// <inheritdoc/>
    public void RemoveRecurring(string recurringJobId)
    {
        // Implementation to remove Quartz trigger/job by ID
    }
}
