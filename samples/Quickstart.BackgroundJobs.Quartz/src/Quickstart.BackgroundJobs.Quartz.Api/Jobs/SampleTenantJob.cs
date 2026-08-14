namespace Quickstart.BackgroundJobs.Quartz.Api.Jobs;

public class SampleTenantJob(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    ILogger<SampleTenantJob> logger) : TenantAwareJobBase(executionContextAccessor, tenantContextPolicy)
{
    protected override Task ExecuteAsync()
    {
        var context = ExecutionContextAccessor.Get();
        logger.LogInformation("Executing tenant-aware Quartz job for tenant: {TenantId}, CorrelationId: {CorrelationId}", 
            context?.TenantId ?? "none", context?.CorrelationId ?? "none");
            
        return Task.CompletedTask;
    }
}
