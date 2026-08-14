namespace Quickstart.BackgroundJobs.Quartz.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class JobsController(IBackgroundJobScheduler scheduler) : ControllerBase
{
    [HttpPost("enqueue")]
    public IActionResult EnqueueJob([FromQuery] string tenantId = "T-9999")
    {
        // Enqueue a job via Quartz
        string jobId = scheduler.Enqueue<SampleTenantJob>(job => job.RunAsync(
            new MuonroiJobExecutionContext(Guid.NewGuid().ToString(), tenantId, "user-2", DateTimeOffset.UtcNow)));
            
        return Ok(new { JobId = jobId, Message = "Job enqueued successfully via Quartz" });
    }
}

public record MuonroiJobExecutionContext(string CorrelationId, string TenantId, string UserId, DateTimeOffset TriggeredAt) : IMuonroiJobExecutionContext;
