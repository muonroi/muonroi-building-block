using Quartz;
using Muonroi.BackgroundJobs.Abstractions;
using System.Linq.Expressions;

namespace Muonroi.BackgroundJobs.Quartz.Quartz;

/// <summary>
/// Quartz implementation of <see cref="IBackgroundJobScheduler"/>.
/// </summary>
public sealed class QuartzJobScheduler(ISchedulerFactory schedulerFactory) : IBackgroundJobScheduler
{
    public string Enqueue<T>(Expression<Func<T, Task>> methodCall)
    {
        // Quartz typically uses Class-based jobs. 
        // For Expression-based jobs, a wrapper job would be needed.
        // For now, we provide a placeholder or basic implementation.
        throw new NotSupportedException("Expression-based jobs are not yet supported in Quartz provider. Use Class-based jobs or Hangfire.");
    }

    public string Schedule<T>(Expression<Func<T, Task>> methodCall, DateTimeOffset enqueueAt)
    {
        throw new NotSupportedException("Expression-based jobs are not yet supported in Quartz provider.");
    }

    public void AddOrUpdateRecurring<T>(string recurringJobId, Expression<Func<T, Task>> methodCall, string cronExpression)
    {
        throw new NotSupportedException("Expression-based recurring jobs are not yet supported in Quartz provider.");
    }

    public void RemoveRecurring(string recurringJobId)
    {
        // Implementation to remove Quartz trigger/job by ID
    }
}
