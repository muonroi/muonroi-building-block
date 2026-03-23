using FluentAssertions;
using Muonroi.BackgroundJobs.Abstractions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Tenancy.Core;
using NSubstitute;
using Quartz;
using Xunit;

namespace Muonroi.BackgroundJobs.Quartz.Tests;

public class QuartzContextJobListenerTests
{
    [Fact]
    public async Task JobToBeExecuted_ShouldApplyAndCleanupMuonroiContext()
    {
        SystemExecutionContextAccessor accessor = new();
        ITenantContextPolicy policy = Substitute.For<ITenantContextPolicy>();
        policy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>())
            .Returns(call => call.Arg<ISystemExecutionContext>());

        Muonroi.BackgroundJobs.Quartz.Quartz.QuartzContextJobListener listener = new(accessor, policy);
        JobDataMap jobDataMap = new();
        jobDataMap["muonroi_execution_context"] = new MuonroiJobExecutionContext(
            tenantId: "tenant-a",
            userId: "user-a",
            username: "alice",
            correlationId: "corr-a",
            accessToken: null,
            apiKey: null,
            isAuthenticated: true,
            permissions: ["jobs.execute"],
            sourceType: "test",
            jobId: "job-1",
            jobType: "quartz",
            scheduledAt: DateTimeOffset.UtcNow);

        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(jobDataMap);

        await listener.JobToBeExecuted(context);

        accessor.Get().TenantId.Should().Be("tenant-a");
        TenantContext.CurrentTenantId.Should().Be("tenant-a");
        jobDataMap.ContainsKey("muonroi_execution_context_scope").Should().BeTrue();

        await listener.JobWasExecuted(context, null);

        accessor.Get().TenantId.Should().BeNull();
        TenantContext.CurrentTenantId.Should().BeNull();
        jobDataMap.ContainsKey("muonroi_execution_context_scope").Should().BeFalse();
    }

    [Fact]
    public async Task JobExecutionVetoed_ShouldDisposeExistingScope()
    {
        Muonroi.BackgroundJobs.Quartz.Quartz.QuartzContextJobListener listener = new();
        JobDataMap jobDataMap = new();
        TestDisposable disposable = new();
        jobDataMap["muonroi_execution_context_scope"] = disposable;

        IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
        context.MergedJobDataMap.Returns(jobDataMap);

        await listener.JobExecutionVetoed(context);

        disposable.Disposed.Should().BeTrue();
        jobDataMap.ContainsKey("muonroi_execution_context_scope").Should().BeFalse();
    }

    private sealed class TestDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }
}
