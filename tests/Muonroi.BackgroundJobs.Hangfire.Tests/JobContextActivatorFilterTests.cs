using FluentAssertions;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Muonroi.BackgroundJobs.Abstractions;
using Muonroi.BackgroundJobs.Hangfire.Hangfire;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Tenancy.Core;
using NSubstitute;
using Xunit;

namespace Muonroi.BackgroundJobs.Hangfire.Tests;

public sealed class JobContextActivatorFilterTests
{
    [Fact]
    public void OnPerforming_WithJobContext_Restores_Context_And_Disposes_Scope_OnPerformed()
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext(
            tenantId: "previous-tenant",
            userId: "previous-user",
            username: "previous-name",
            correlationId: "corr-previous",
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "test"));

        TestLogScopeFactory logScopeFactory = new();
        ITenantContextPolicy policy = Substitute.For<ITenantContextPolicy>();
        MuonroiJobExecutionContext jobContext = new(
            tenantId: "tenant-a",
            userId: "user-a",
            username: "alice",
            correlationId: "corr-a",
            accessToken: "token",
            apiKey: null,
            isAuthenticated: true,
            permissions: ["perm.read"],
            sourceType: "job",
            jobId: "job-1",
            jobType: "sync",
            scheduledAt: DateTimeOffset.UtcNow);
        SystemExecutionContext resolved = new(
            tenantId: "tenant-resolved",
            userId: "user-resolved",
            username: "alice",
            correlationId: "corr-a",
            accessToken: "token",
            apiKey: null,
            isAuthenticated: true,
            permissions: ["perm.read"],
            sourceType: "hangfire");
        policy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>()).Returns(resolved);

        JobContextActivatorFilter filter = new(accessor, policy, logScopeFactory);
        PerformingContext performingContext = CreatePerformingContext(jobContext);

        filter.OnPerforming(performingContext);

        accessor.Get().TenantId.Should().Be("tenant-resolved");
        TenantContext.CurrentTenantId.Should().Be("tenant-resolved");
        UserContext.CurrentUserGuid.Should().Be("user-resolved");
        logScopeFactory.LastProperties!["CorrelationId"].Should().Be("corr-a");

        filter.OnPerformed(new PerformedContext(performingContext, null, false, null));

        accessor.Get().TenantId.Should().Be("previous-tenant");
        TenantContext.CurrentTenantId.Should().BeNull();
        UserContext.CurrentUserGuid.Should().BeNull();
        logScopeFactory.DisposeCount.Should().Be(1);
    }

    [Fact]
    public void OnPerforming_WithoutJobContext_Creates_Anonymous_Hangfire_Context()
    {
        SystemExecutionContextAccessor accessor = new();
        ITenantContextPolicy policy = Substitute.For<ITenantContextPolicy>();
        ISystemExecutionContext? captured = null;
        policy.ResolveAndValidate(Arg.Do<ISystemExecutionContext>(ctx => captured = ctx))
            .Returns(call => call.Arg<ISystemExecutionContext>());

        JobContextActivatorFilter filter = new(accessor, policy, new TestLogScopeFactory());
        PerformingContext performingContext = CreatePerformingContext();

        filter.OnPerforming(performingContext);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().BeNull();
        captured.SourceType.Should().Be("hangfire");
        captured.CorrelationId.Should().NotBeNullOrWhiteSpace();
        accessor.Get().SourceType.Should().Be("hangfire");
    }

    [Fact]
    public void OnPerformed_WithoutScope_DoesNothing()
    {
        JobContextActivatorFilter filter = new();
        PerformingContext performingContext = CreatePerformingContext();

        Action act = () => filter.OnPerformed(new PerformedContext(performingContext, null, false, null));

        act.Should().NotThrow();
    }

    private static PerformingContext CreatePerformingContext(IMuonroiJobExecutionContext? jobContext = null)
    {
        Job job = new(typeof(DummyBackgroundJob), DummyJobMethod, [jobContext]);
        BackgroundJob backgroundJob = new("job-1", job, DateTime.UtcNow);
        JobStorage.Current = new global::Hangfire.MemoryStorage.MemoryStorage();
        IStorageConnection connection = Substitute.For<IStorageConnection>();
        IJobCancellationToken cancellationToken = Substitute.For<IJobCancellationToken>();
        PerformContext performContext = new(JobStorage.Current, connection, backgroundJob, cancellationToken);
        return new PerformingContext(performContext);
    }

    private static readonly System.Reflection.MethodInfo DummyJobMethod =
        typeof(DummyBackgroundJob).GetMethod(nameof(DummyBackgroundJob.Run), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;

    private sealed class TestLogScopeFactory : ILogScopeFactory
    {
        public IReadOnlyDictionary<string, object?>? LastProperties { get; private set; }
        public int DisposeCount { get; private set; }

        public IDisposable? BeginScope(IReadOnlyDictionary<string, object?> properties)
        {
            LastProperties = properties;
            return new DisposableAction(() => DisposeCount++);
        }
    }

    private sealed class DisposableAction(Action callback) : IDisposable
    {
        private readonly Action _callback = callback;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _callback();
            _disposed = true;
        }
    }

    public static class DummyBackgroundJob
    {
        public static void Run(IMuonroiJobExecutionContext? context = null)
        {
        }
    }
}
