namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Base class for background jobs that executes inside a canonical Muonroi system context.
/// Relies on <see cref="ISystemExecutionContextAccessor"/> and automated filters to restore state.
/// </summary>
/// <param name="executionContextAccessor">Accessor for the ambient execution context.</param>
/// <param name="tenantContextPolicy">Policy used to validate tenant context.</param>
public abstract class TenantAwareJobBase(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy)
{
    protected readonly ISystemExecutionContextAccessor ExecutionContextAccessor = executionContextAccessor;
    protected readonly ITenantContextPolicy TenantContextPolicy = tenantContextPolicy;

    /// <summary>
    /// Executes the job logic.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task ExecuteAsync();

    /// <summary>
    /// Entry point for background schedulers. Context is automatically restored by engine-specific filters.
    /// This method ensures a safe execution scope is present if not already established.
    /// </summary>
    /// <param name="executionContext">The job execution context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public virtual async Task RunAsync(IMuonroiJobExecutionContext executionContext)
    {
        _ = executionContext ?? throw new ArgumentNullException(nameof(executionContext));

        // If context is already restored by a filter (e.g. Hangfire/Quartz Listener), 
        // we just execute. Otherwise, we establish a local scope.
        var currentContext = ExecutionContextAccessor.Get();
        if (currentContext != null && 
            currentContext.CorrelationId == executionContext.CorrelationId)
        {
            await ExecuteAsync();
            return;
        }

        ISystemExecutionContext resolved = TenantContextPolicy.ResolveAndValidate(executionContext);
        using SystemExecutionContextScope scope = new(ExecutionContextAccessor, resolved);
        
        await ExecuteAsync();
    }
}
