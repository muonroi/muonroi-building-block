namespace Muonroi.BackgroundJobs.Abstractions;

/// <summary>
/// Base class for background jobs that executes inside a canonical Muonroi system context.
/// </summary>
public abstract class TenantAwareJobBase(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy)
{
    private readonly ISystemExecutionContextAccessor _executionContextAccessor = executionContextAccessor;
    private readonly ITenantContextPolicy _tenantContextPolicy = tenantContextPolicy;

    protected abstract Task ExecuteAsync();

    /// <summary>
    /// Entry point for background schedulers. Context is restored before execution.
    /// </summary>
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
