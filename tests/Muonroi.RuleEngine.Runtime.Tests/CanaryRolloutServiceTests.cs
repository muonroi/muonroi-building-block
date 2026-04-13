using Muonroi.Core.Abstractions.Exceptions;


namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class CanaryRolloutServiceTests
{
    [Fact]
    public async Task StartCanaryAsync_WithoutTargets_ShouldThrow()
    {
        await using RuleEngineDbContext dbContext = CreateDbContext();
        dbContext.RuleSets.Add(new RuleSetRecord
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 7,
            Json = "{}",
            Status = RuleSetStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        CanaryRolloutService sut = new(
            dbContext,
            Substitute.For<IRuleSetStore>(),
            Substitute.For<IRuleSetAuditStore>(),
            executionContextAccessor: CreateAccessor("control-plane"));

        Func<Task> action = () => sut.StartCanaryAsync(new StartCanaryRequest
        {
            WorkflowName = "wf.orders",
            Version = 7
        });

        await action.Should().ThrowAsync<MInternalException>()
            .WithMessage("*TargetTenantIds or TargetPercentage*");
    }

    [Fact]
    public async Task StartCanaryAsync_SupersedesExistingRollout_AndNormalizesTenantTargets()
    {
        await using RuleEngineDbContext dbContext = CreateDbContext();
        SystemExecutionContextAccessor accessor = CreateAccessor("control-plane");
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        IRuleSetAuditStore auditStore = Substitute.For<IRuleSetAuditStore>();
        IRuleSetChangeNotifier notifier = Substitute.For<IRuleSetChangeNotifier>();
        MemoryCache memoryCache = new(new MemoryCacheOptions());

        dbContext.RuleSets.Add(new RuleSetRecord
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 7,
            Json = "{}",
            Status = RuleSetStatus.Approved
        });
        CanaryRolloutRecord previous = new()
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 6,
            Status = CanaryStatus.Active,
            StartedBy = "prior"
        };
        dbContext.CanaryRollouts.Add(previous);
        await dbContext.SaveChangesAsync();

        CanaryRolloutService sut = new(dbContext, store, auditStore, notifier, memoryCache, accessor);

        CanaryRolloutRecord result = await sut.StartCanaryAsync(new StartCanaryRequest
        {
            WorkflowName = " wf.orders ",
            Version = 7,
            StartedBy = "  alice  ",
            TargetTenantIds = [" tenant-b ", "tenant-a", "TENANT-A", ""]
        });

        result.Version.Should().Be(7);
        result.TargetTenantIds.Should().Equal("tenant-a", "tenant-b");
        result.StartedBy.Should().Be("alice");

        previous.Status.Should().Be(CanaryStatus.RolledBack);
        previous.RolledBackBy.Should().Be("alice");
        previous.RollbackReason.Should().Be("Superseded by new canary rollout.");

        await auditStore.Received(1).AppendAsync(
            Arg.Is<RuleSetAuditEntry>(x =>
                x.TenantId == "control-plane" &&
                x.WorkflowName == "wf.orders" &&
                x.Action == "StartCanary" &&
                x.Version == 7 &&
                x.Actor == "alice" &&
                x.Detail == "tenants:tenant-a,tenant-b"),
            Arg.Any<CancellationToken>());

        await notifier.Received(1).PublishAsync(
            Arg.Is<RuleSetChangeEvent>(x =>
                x.TenantId == "control-plane" &&
                x.WorkflowName == "wf.orders" &&
                x.ChangeType == RuleSetChangeTypes.CanaryStarted &&
                x.Version == 7),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartCanaryAsync_WithInvalidPercentage_ShouldThrow()
    {
        await using RuleEngineDbContext dbContext = CreateDbContext();
        dbContext.RuleSets.Add(new RuleSetRecord
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 7,
            Json = "{}",
            Status = RuleSetStatus.Approved
        });
        await dbContext.SaveChangesAsync();

        CanaryRolloutService sut = new(
            dbContext,
            Substitute.For<IRuleSetStore>(),
            Substitute.For<IRuleSetAuditStore>(),
            executionContextAccessor: CreateAccessor("control-plane"));

        Func<Task> action = () => sut.StartCanaryAsync(new StartCanaryRequest
        {
            WorkflowName = "wf.orders",
            Version = 7,
            TargetPercentage = 100
        });

        await action.Should().ThrowAsync<MInternalException>()
            .WithMessage("*TargetPercentage must be in range*");
    }

    [Fact]
    public async Task PromoteCanaryAsync_ActiveRollout_SetsActiveVersion_AndPublishesNotifications()
    {
        await using RuleEngineDbContext dbContext = CreateDbContext();
        SystemExecutionContextAccessor accessor = CreateAccessor("control-plane");
        IRuleSetStore store = Substitute.For<IRuleSetStore>();
        IRuleSetAuditStore auditStore = Substitute.For<IRuleSetAuditStore>();
        IRuleSetChangeNotifier notifier = Substitute.For<IRuleSetChangeNotifier>();

        CanaryRolloutRecord rollout = new()
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 9,
            Status = CanaryStatus.Active,
            StartedBy = "alice"
        };
        dbContext.CanaryRollouts.Add(rollout);
        await dbContext.SaveChangesAsync();

        CanaryRolloutService sut = new(dbContext, store, auditStore, notifier, executionContextAccessor: accessor);

        await sut.PromoteCanaryAsync(rollout.Id, " bob ");

        rollout.Status.Should().Be(CanaryStatus.Promoted);
        rollout.PromotedBy.Should().Be("bob");
        rollout.CompletedAt.Should().NotBeNull();

        await store.Received(1).SetActiveVersionAsync("wf.orders", 9, Arg.Any<CancellationToken>());
        await auditStore.Received(1).AppendAsync(
            Arg.Is<RuleSetAuditEntry>(x =>
                x.Action == "PromoteCanary" &&
                x.WorkflowName == "wf.orders" &&
                x.Version == 9 &&
                x.Actor == "bob"),
            Arg.Any<CancellationToken>());
        await notifier.Received(1).PublishAsync(
            Arg.Is<RuleSetChangeEvent>(x =>
                x.ChangeType == RuleSetChangeTypes.CanaryPromoted &&
                x.WorkflowName == "wf.orders" &&
                x.Version == 9),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RollbackCanaryAsync_ActiveRollout_UpdatesStatus_AndWritesAudit()
    {
        await using RuleEngineDbContext dbContext = CreateDbContext();
        SystemExecutionContextAccessor accessor = CreateAccessor("control-plane");
        IRuleSetAuditStore auditStore = Substitute.For<IRuleSetAuditStore>();
        IRuleSetChangeNotifier notifier = Substitute.For<IRuleSetChangeNotifier>();

        CanaryRolloutRecord rollout = new()
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 9,
            Status = CanaryStatus.Active,
            StartedBy = "alice"
        };
        dbContext.CanaryRollouts.Add(rollout);
        await dbContext.SaveChangesAsync();

        CanaryRolloutService sut = new(
            dbContext,
            Substitute.For<IRuleSetStore>(),
            auditStore,
            notifier,
            executionContextAccessor: accessor);

        await sut.RollbackCanaryAsync(rollout.Id, " bob ", " rollout failed ");

        rollout.Status.Should().Be(CanaryStatus.RolledBack);
        rollout.RolledBackBy.Should().Be("bob");
        rollout.RollbackReason.Should().Be("rollout failed");
        rollout.CompletedAt.Should().NotBeNull();

        await auditStore.Received(1).AppendAsync(
            Arg.Is<RuleSetAuditEntry>(x =>
                x.Action == "RollbackCanary" &&
                x.WorkflowName == "wf.orders" &&
                x.Version == 9 &&
                x.Actor == "bob" &&
                x.Detail == "rollout failed"),
            Arg.Any<CancellationToken>());
        await notifier.Received(1).PublishAsync(
            Arg.Is<RuleSetChangeEvent>(x =>
                x.ChangeType == RuleSetChangeTypes.CanaryRolledBack &&
                x.WorkflowName == "wf.orders" &&
                x.Version == 9),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCanaryVersionForTenantAsync_UsesCachedValue_UntilMarkerChanges()
    {
        await using RuleEngineDbContext dbContext = CreateDbContext();
        SystemExecutionContextAccessor accessor = CreateAccessor("control-plane");
        MemoryCache memoryCache = new(new MemoryCacheOptions());

        dbContext.CanaryRollouts.Add(new CanaryRolloutRecord
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 11,
            Status = CanaryStatus.Active,
            StartedBy = "alice",
            TargetTenantIds = ["tenant-a"]
        });
        await dbContext.SaveChangesAsync();

        CanaryRolloutService sut = new(
            dbContext,
            Substitute.For<IRuleSetStore>(),
            Substitute.For<IRuleSetAuditStore>(),
            notifier: null,
            memoryCache,
            accessor);

        int? first = await sut.GetCanaryVersionForTenantAsync("wf.orders", "tenant-a");
        first.Should().Be(11);

        dbContext.CanaryRollouts.RemoveRange(dbContext.CanaryRollouts);
        await dbContext.SaveChangesAsync();

        int? cached = await sut.GetCanaryVersionForTenantAsync("wf.orders", "tenant-a");
        cached.Should().Be(11);

        memoryCache.Remove("ruleset:canary:marker:control-plane:wf.orders");

        int? refreshed = await sut.GetCanaryVersionForTenantAsync("wf.orders", "tenant-a");
        refreshed.Should().BeNull();
    }

    [Fact]
    public async Task GetCanaryVersionForTenantAsync_TargetPercentage_OnlyAppliesToBucketedTenants()
    {
        await using RuleEngineDbContext dbContext = CreateDbContext();
        dbContext.CanaryRollouts.Add(new CanaryRolloutRecord
        {
            Id = Guid.NewGuid(),
            TenantId = "control-plane",
            WorkflowName = "wf.orders",
            Version = 11,
            Status = CanaryStatus.Active,
            StartedBy = "alice",
            TargetPercentage = 1
        });
        await dbContext.SaveChangesAsync();

        CanaryRolloutService sut = new(
            dbContext,
            Substitute.For<IRuleSetStore>(),
            Substitute.For<IRuleSetAuditStore>(),
            executionContextAccessor: CreateAccessor("control-plane"));

        int? hit = await sut.GetCanaryVersionForTenantAsync("wf.orders", FindTenantForBucket(lessThanOnePercent: true));
        int? miss = await sut.GetCanaryVersionForTenantAsync("wf.orders", FindTenantForBucket(lessThanOnePercent: false));

        hit.Should().Be(11);
        miss.Should().BeNull();
    }

    private static RuleEngineDbContext CreateDbContext()
    {
        DbContextOptions<RuleEngineDbContext> options = new DbContextOptionsBuilder<RuleEngineDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new RuleEngineDbContext(options);
    }

    private static SystemExecutionContextAccessor CreateAccessor(string tenantId)
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext(
            tenantId,
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "tests"));
        return accessor;
    }

    private static string FindTenantForBucket(bool lessThanOnePercent)
    {
        for (int i = 0; i < 5000; i++)
        {
            string candidate = $"tenant-{i}";
            byte bucket = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(candidate))[0];
            if (lessThanOnePercent == (bucket % 100 < 1))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Unable to find deterministic tenant bucket for canary test.");
    }
}
