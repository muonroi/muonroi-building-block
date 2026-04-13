using Muonroi.Core.Abstractions.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using Muonroi.BackgroundJobs.Abstractions;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;
using Xunit;

namespace Muonroi.BackgroundJobs.Abstractions.Tests;

public class TenantAwareJobBaseTests
{
    private readonly Mock<ISystemExecutionContextAccessor> _accessorMock = new();
    private readonly Mock<ITenantContextPolicy> _policyMock = new();

    private class TestJob(
        ISystemExecutionContextAccessor accessor,
        ITenantContextPolicy policy) : TenantAwareJobBase(accessor, policy)
    {
        public bool Executed { get; private set; }
        public string? CapturedTenantId { get; private set; }

        protected override Task ExecuteAsync()
        {
            Executed = true;
            CapturedTenantId = TenantContext.CurrentTenantId;
            return Task.CompletedTask;
        }

        public Task RunPublic(IMuonroiJobExecutionContext ctx) => RunAsync(ctx);
    }

    private IMuonroiJobExecutionContext CreateContext(string tenantId = "tenant-1")
    {
        return new MuonroiJobExecutionContext(
            tenantId: tenantId,
            userId: "user-1",
            username: "admin",
            correlationId: "corr-1",
            accessToken: null,
            apiKey: null,
            isAuthenticated: true,
            permissions: null,
            sourceType: "test",
            jobId: "job-1",
            jobType: "recurring",
            scheduledAt: DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RunAsync_Should_Execute_Job()
    {
        IMuonroiJobExecutionContext jobCtx = CreateContext();
        SystemExecutionContext resolvedCtx = new(
            "tenant-1", "user-1", "admin", "corr-1", null, null, true, null, "test");

        _policyMock.Setup(x => x.ResolveAndValidate(jobCtx)).Returns(resolvedCtx);

        TestJob job = new(_accessorMock.Object, _policyMock.Object);
        await job.RunPublic(jobCtx);

        job.Executed.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_Should_Set_TenantContext_During_Execution()
    {
        IMuonroiJobExecutionContext jobCtx = CreateContext("my-tenant");
        SystemExecutionContext resolvedCtx = new(
            "my-tenant", "user-1", "admin", "corr-1", null, null, true, null, "test");

        _policyMock.Setup(x => x.ResolveAndValidate(jobCtx)).Returns(resolvedCtx);

        TestJob job = new(_accessorMock.Object, _policyMock.Object);
        await job.RunPublic(jobCtx);

        job.CapturedTenantId.Should().Be("my-tenant");
    }

    [Fact]
    public async Task RunAsync_Should_Clear_TenantContext_After_Execution()
    {
        IMuonroiJobExecutionContext jobCtx = CreateContext();
        SystemExecutionContext resolvedCtx = new(
            "tenant-1", "user-1", "admin", "corr-1", null, null, true, null, "test");

        _policyMock.Setup(x => x.ResolveAndValidate(jobCtx)).Returns(resolvedCtx);

        TestJob job = new(_accessorMock.Object, _policyMock.Object);
        await job.RunPublic(jobCtx);

        TenantContext.CurrentTenantId.Should().BeNull();
        UserContext.CurrentUserGuid.Should().BeNull();
        UserContext.CurrentUsername.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_Should_Clear_Context_Even_On_Exception()
    {
        IMuonroiJobExecutionContext jobCtx = CreateContext();
        SystemExecutionContext resolvedCtx = new(
            "tenant-1", "user-1", "admin", "corr-1", null, null, true, null, "test");

        _policyMock.Setup(x => x.ResolveAndValidate(jobCtx)).Returns(resolvedCtx);

        var failingJob = new FailingJob(_accessorMock.Object, _policyMock.Object);

        Func<Task> act = () => failingJob.RunPublic(jobCtx);
        await act.Should().ThrowAsync<InvalidOperationException>();

        TenantContext.CurrentTenantId.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_Should_Throw_On_Null_Context()
    {
        TestJob job = new(_accessorMock.Object, _policyMock.Object);

        Func<Task> act = () => job.RunPublic(null!);
        await act.Should().ThrowAsync<MArgumentException>();
    }

    private class FailingJob(
        ISystemExecutionContextAccessor accessor,
        ITenantContextPolicy policy) : TenantAwareJobBase(accessor, policy)
    {
        protected override Task ExecuteAsync() => throw new InvalidOperationException("Job failed");
        public Task RunPublic(IMuonroiJobExecutionContext ctx) => RunAsync(ctx);
    }
}

public class BackgroundJobHandlerLegacyTests
{
    [Fact]
    public void AddBackgroundJobs_With_Unsupported_Type_Should_Throw_MConfigurationException()
    {
        // SEC-02: Unknown job type now throws MConfigurationException (not MInternalException).
        // This replaces the unsafe Type.GetType reflection-based dispatch.
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobConfigs:JobType"] = "999"
            })
            .Build();

        Action act = () => BackgroundJobHandler.AddBackgroundJobs(services, config);
        act.Should().Throw<MConfigurationException>();
    }
}
