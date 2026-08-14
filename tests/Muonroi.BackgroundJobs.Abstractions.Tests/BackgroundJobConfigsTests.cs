namespace Muonroi.BackgroundJobs.Abstractions.Tests;

public class BackgroundJobConfigsTests
{
    [Fact]
    public void SectionName_Should_Be_BackgroundJobConfigs()
    {
        BackgroundJobConfigs.SectionName.Should().Be("BackgroundJobConfigs");
    }

    [Fact]
    public void Default_JobType_Should_Be_Hangfire()
    {
        BackgroundJobConfigs cfg = new();

        cfg.JobType.Should().Be(JobType.Hangfire);
    }

    [Fact]
    public void ConnectionString_Should_Default_To_Null()
    {
        BackgroundJobConfigs cfg = new();

        cfg.ConnectionString.Should().BeNull();
    }

    [Fact]
    public void Properties_Should_Be_Settable()
    {
        BackgroundJobConfigs cfg = new()
        {
            JobType = JobType.Quartz,
            ConnectionString = "Server=localhost"
        };

        cfg.JobType.Should().Be(JobType.Quartz);
        cfg.ConnectionString.Should().Be("Server=localhost");
    }
}

public class JobTypeEnumTests
{
    [Fact]
    public void JobType_Has_Hangfire_And_Quartz()
    {
        Enum.GetValues<JobType>().Should().HaveCount(2);
        Enum.IsDefined(JobType.Hangfire).Should().BeTrue();
        Enum.IsDefined(JobType.Quartz).Should().BeTrue();
    }
}

public class MuonroiJobExecutionContextTests
{
    [Fact]
    public void Constructor_Should_Set_Properties()
    {
        DateTimeOffset scheduledAt = DateTimeOffset.UtcNow;

        MuonroiJobExecutionContext ctx = new(
            tenantId: "tenant-1",
            userId: "user-1",
            username: "admin",
            correlationId: "corr-123",
            accessToken: "token",
            apiKey: "key",
            isAuthenticated: true,
            permissions: new List<string> { "read", "write" },
            sourceType: "hangfire",
            jobId: "job-42",
            jobType: "recurring",
            scheduledAt: scheduledAt);

        ctx.TenantId.Should().Be("tenant-1");
        ctx.UserId.Should().Be("user-1");
        ctx.Username.Should().Be("admin");
        ctx.CorrelationId.Should().Be("corr-123");
        ctx.JobId.Should().Be("job-42");
        ctx.JobType.Should().Be("recurring");
        ctx.ScheduledAt.Should().Be(scheduledAt);
        ctx.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void Blank_JobId_Should_Generate_Guid()
    {
        MuonroiJobExecutionContext ctx = new(
            tenantId: null, userId: null, username: null,
            correlationId: "c", accessToken: null, apiKey: null,
            isAuthenticated: false, permissions: null,
            sourceType: "test",
            jobId: "", jobType: "", scheduledAt: default);

        ctx.JobId.Should().NotBeNullOrWhiteSpace();
        ctx.JobType.Should().Be("unknown");
    }

    [Fact]
    public void Null_JobId_Should_Generate_Guid()
    {
        MuonroiJobExecutionContext ctx = new(
            tenantId: null, userId: null, username: null,
            correlationId: "c", accessToken: null, apiKey: null,
            isAuthenticated: false, permissions: null,
            sourceType: "test",
            jobId: null!, jobType: null!, scheduledAt: default);

        ctx.JobId.Should().NotBeNullOrWhiteSpace();
        ctx.JobType.Should().Be("unknown");
    }
}
