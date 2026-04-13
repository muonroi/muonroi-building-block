using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class TenantAwareJobBaseTests
{
    private class TestJob(Action onExecute, bool throwException = false)
        : TenantAwareJobBase(new SystemExecutionContextAccessor(), new DefaultTenantContextPolicy(new NullContextResolver()))
    {
        protected override Task ExecuteAsync()
        {
            onExecute();
            if (throwException)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.CompletedTask;
        }

        public Task Handle(IMuonroiJobExecutionContext executionContext)
        {
            return RunAsync(executionContext);
        }
    }

    [Fact]
    public async Task RunAsync_Sets_And_Clears_Context_On_Success()
    {
        string tenant = "t";
        string guid = "g";
        string user = "u";
        bool executed = false;
        TestJob job = new(() =>
        {
            Assert.Equal(tenant, TenantContext.CurrentTenantId);
            Assert.Equal(guid, UserContext.CurrentUserGuid);
            Assert.Equal(user, UserContext.CurrentUsername);
            executed = true;
        });

        await job.Handle(CreateContext(tenant, guid, user));

        Assert.True(executed);
        Assert.Null(TenantContext.CurrentTenantId);
        Assert.Null(UserContext.CurrentUserGuid);
        Assert.Null(UserContext.CurrentUsername);
    }

    [Fact]
    public async Task RunAsync_Clears_Context_When_Execute_Throws()
    {
        TestJob job = new(() => { }, true);

        await Assert.ThrowsAsync<MInternalException>(() => job.Handle(CreateContext("t", "g", "u")));

        Assert.Null(TenantContext.CurrentTenantId);
        Assert.Null(UserContext.CurrentUserGuid);
        Assert.Null(UserContext.CurrentUsername);
    }

    [Fact]
    public async Task RunAsync_WithTenantOnly_Sets_And_Clears_Tenant_Context()
    {
        TenantContext.CurrentTenantId = null;
        UserContext.CurrentUserGuid = null;
        UserContext.CurrentUsername = null;

        string tenant = Guid.NewGuid().ToString();
        bool executed = false;
        TestJob job = new(() =>
        {
            executed = true;
            Assert.Equal(tenant, TenantContext.CurrentTenantId);
            Assert.Null(UserContext.CurrentUserGuid);
            Assert.Null(UserContext.CurrentUsername);
        });

        await job.Handle(CreateContext(tenant, null, null));

        Assert.True(executed);
        Assert.Null(TenantContext.CurrentTenantId);
        Assert.Null(UserContext.CurrentUserGuid);
        Assert.Null(UserContext.CurrentUsername);
    }

    private static IMuonroiJobExecutionContext CreateContext(string? tenantId, string? userId, string? username)
    {
        return new MuonroiJobExecutionContext(
            tenantId: tenantId,
            userId: userId,
            username: username,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: string.IsNullOrWhiteSpace(userId) ? null : "token",
            apiKey: null,
            isAuthenticated: !string.IsNullOrWhiteSpace(userId),
            permissions: [],
            sourceType: "hangfire",
            jobId: Guid.NewGuid().ToString("N"),
            jobType: "test-job",
            scheduledAt: DateTimeOffset.UtcNow);
    }
}
