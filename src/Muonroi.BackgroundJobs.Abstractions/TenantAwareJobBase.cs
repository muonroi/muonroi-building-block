namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Base class for background jobs that executes inside a canonical Muonroi system context.
/// </summary>
/// <param name="executionContextAccessor">Accessor for the ambient execution context.</param>
/// <param name="tenantContextPolicy">Policy used to validate tenant context.</param>
public abstract class TenantAwareJobBase(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy)
{
    private readonly ISystemExecutionContextAccessor _executionContextAccessor = executionContextAccessor;
    private readonly ITenantContextPolicy _tenantContextPolicy = tenantContextPolicy;

    /// <summary>
    /// Executes the job logic.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task ExecuteAsync();

    /// <summary>
    /// Entry point for background schedulers. Context is restored before execution.
    /// </summary>
    /// <param name="executionContext">The job execution context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected async Task RunAsync(IMuonroiJobExecutionContext executionContext)
    {
        _ = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        ISystemExecutionContext resolved = _tenantContextPolicy.ResolveAndValidate(executionContext);
        try
        {
            using SystemExecutionContextScope scope = new(_executionContextAccessor, resolved);
            TenantContext.CurrentTenantId = resolved.TenantId;
            UserContext.CurrentUserGuid = resolved.UserId;
            UserContext.CurrentUsername = resolved.Username;
            await ExecuteAsync();
        }
        finally
        {
            TenantContext.CurrentTenantId = null;
            UserContext.CurrentUserGuid = null;
            UserContext.CurrentUsername = null;
        }
    }
}
