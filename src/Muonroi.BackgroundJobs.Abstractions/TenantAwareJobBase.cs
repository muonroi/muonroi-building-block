using Muonroi.Core.Abstractions.Guards;

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
    /// <summary>
    /// Provides access to the current system execution context for derived classes.
    /// </summary>
    /// <remarks>Intended for use by subclasses to retrieve information about the current execution context,
    /// such as user identity or request metadata. The accessor instance is assigned at construction and should not be
    /// modified.</remarks>
    protected readonly ISystemExecutionContextAccessor ExecutionContextAccessor = executionContextAccessor;
    /// <summary>
    /// Provides access to the tenant context policy used to determine or enforce tenant-specific behavior within the
    /// application.
    /// </summary>
    /// <remarks>This field is intended for use by derived classes to interact with tenant resolution or
    /// enforcement logic. The specific behavior depends on the implementation of <see
    /// cref="ITenantContextPolicy"/>.</remarks>
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
        MGuard.NotNull(executionContext);

        // If context is already restored by a filter (e.g. Hangfire/Quartz Listener), 
        // we just execute. 
        var currentContext = ExecutionContextAccessor.Get();
        if (currentContext != null &&
            !string.IsNullOrWhiteSpace(currentContext.CorrelationId) &&
            currentContext.CorrelationId == executionContext.CorrelationId)
        {
            await ExecuteAsync();
            return;
        }

        // Fallback: Manually restore context if not already established (typical in Unit Tests)
        ISystemExecutionContext resolved = TenantContextPolicy.ResolveAndValidate(executionContext);
        using SystemExecutionContextScope scope = new(ExecutionContextAccessor, resolved);

        // Better Together: Ensure static contexts are also populated during manual run
        using (ContextMirrorScope.Apply(resolved))
        {
            await ExecuteAsync();
        }
    }
}
