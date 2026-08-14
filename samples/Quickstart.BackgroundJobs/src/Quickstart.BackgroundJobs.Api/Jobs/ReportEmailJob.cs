namespace Quickstart.BackgroundJobs.Api.Jobs;

/// <summary>
/// A background job that is tenant-aware.
///
/// Demonstrates feature: <see cref="TenantAwareJobBase"/>.
///
/// How it works:
///   1. The Hangfire job filter (<c>JobContextActivatorFilter</c>) runs before this job executes.
///      It reads the <see cref="IMuonroiJobExecutionContext"/> that was serialised into the job
///      arguments at enqueue time and restores it to the ambient
///      <see cref="ISystemExecutionContextAccessor"/> so it is available during <see cref="ExecuteAsync"/>.
///
///   2. <see cref="TenantAwareJobBase.RunAsync"/> is the entry-point called by Hangfire.
///      It is passed a <c>null!</c> placeholder from the scheduler expression because the real
///      context object is embedded in the Hangfire job args by <c>HangfireJobScheduler</c>
///      and injected by the filter — not passed as a live parameter.
///
///   3. <see cref="ExecuteAsync"/> is called by the base class after context is confirmed live.
///      Derived classes put their domain logic here; they should not need to touch the context
///      manually — just call <c>ExecutionContextAccessor.Get()</c>.
/// </summary>
public sealed class ReportEmailJob(
    ISystemExecutionContextAccessor executionContextAccessor,
    ITenantContextPolicy tenantContextPolicy,
    ILogger<ReportEmailJob> logger)
    : TenantAwareJobBase(executionContextAccessor, tenantContextPolicy)
{
    private readonly ILogger<ReportEmailJob> _logger = logger;

    /// <inheritdoc />
    protected override Task ExecuteAsync()
    {
        // Retrieve the ambient execution context that was restored by the Hangfire filter
        // (or by TenantAwareJobBase's fallback scope in unit tests).
        ISystemExecutionContext ctx = ExecutionContextAccessor.Get();

        _logger.LogInformation(
            "[ReportEmailJob] Running — " +
            "TenantId={TenantId} | UserId={UserId} | Username={Username} | " +
            "CorrelationId={CorrelationId} | IsAuthenticated={IsAuthenticated} | " +
            "SourceType={SourceType}",
            ctx.TenantId   ?? "(none)",
            ctx.UserId     ?? "(none)",
            ctx.Username   ?? "(none)",
            ctx.CorrelationId,
            ctx.IsAuthenticated,
            ctx.SourceType);

        // ── Simulated domain work ────────────────────────────────────────────
        // In a real application this would compose a report, render an email
        // template, and dispatch it via an IEmailSender — all enriched with
        // the tenant / user context retrieved above.
        _logger.LogInformation("[ReportEmailJob] Report email dispatched successfully.");

        return Task.CompletedTask;
    }
}
