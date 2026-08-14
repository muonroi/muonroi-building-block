namespace Quickstart.BackgroundJobs.Hangfire.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class JobsController(IBackgroundJobScheduler scheduler) : ControllerBase
{
    [HttpPost("enqueue")]
    public IActionResult EnqueueJob([FromQuery] string tenantId = "T-12345")
    {
        // For demonstration, we just schedule it. Real usage would set execution context for current request.
        string jobId = scheduler.Enqueue<SampleTenantJob>(job => job.RunAsync(
            new MuonroiJobExecutionContext(Guid.NewGuid().ToString(), tenantId, "user-1", DateTimeOffset.UtcNow)));
            
        return Ok(new { JobId = jobId, Message = "Job enqueued successfully" });
    }
}

public record MuonroiJobExecutionContext(string CorrelationId, string TenantId, string UserId, DateTimeOffset TriggeredAt) : IMuonroiJobExecutionContext;
