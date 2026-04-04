using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;

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
