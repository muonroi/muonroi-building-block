namespace Muonroi.BuildingBlock.Test;

using Muonroi.Governance.License;

public class MGenericControllerTenantIsolationTests
{
    private sealed class StubMediator : IMediator
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult(default(TResponse)!);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => AsyncEnumerable.Empty<TResponse>();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<object?>();
    }

    private sealed class StubLicenseGuard(bool hasMultiTenantFeature) : ILicenseGuard
    {
        public LicenseState Current => LicenseState.CreateFree();
        public LicenseTier Tier => LicenseTier.Free;
        public bool IsFreeMode => true;
        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName) => true;

        public void EnsureFeature(string featureName)
        {
            if (string.Equals(featureName, FreeTierFeatures.Premium.MultiTenant, StringComparison.OrdinalIgnoreCase) &&
                !hasMultiTenantFeature)
                throw new InvalidOperationException("multi-tenant feature not licensed");
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken() => "test";

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("key", encryptedData);
        }
    }

    private sealed class TenantScopedEntity : MEntity, ITenantScoped
    {
        public string Name { get; set; } = string.Empty;
        public string? TenantId { get; set; }
    }

    private sealed class TenantIsolationDbContext(DbContextOptions<TenantIsolationDbContext> options)
        : MDbContext(options, new StubMediator())
    {
        public DbSet<TenantScopedEntity> Items { get; set; } = null!;
    }

    private sealed class TestController(
        TenantIsolationDbContext db,
        ILicenseGuard guard,
        MTokenInfo tokenInfo,
        IConfiguration configuration) : MGenericController<TenantScopedEntity, TenantIsolationDbContext>(
            db,
            guard,
            tokenInfo,
            configuration)
    {
        public IQueryable<TenantScopedEntity> Apply(IQueryable<TenantScopedEntity> query)
        {
            return ApplyTenantFilter(query);
        }

        public void Set(TenantScopedEntity entity)
        {
            SetTenantId(entity);
        }
    }

    private static IConfiguration BuildConfig(bool enabled)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:Enabled"] = enabled.ToString()
            })
            .Build();
    }

    private static TenantIsolationDbContext CreateContext(string dbName)
    {
        DbContextOptions<TenantIsolationDbContext> options = new DbContextOptionsBuilder<TenantIsolationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new TenantIsolationDbContext(options);
    }

    [Fact]
    public async Task ApplyTenantFilter_MultiTenantEnabled_WithoutTenant_ThrowsUnauthorized()
    {
        await using TenantIsolationDbContext db = CreateContext(Guid.NewGuid().ToString());
        TenantScopedEntity entity = new()
        {
            Name = "a",
            TenantId = "tenant-1"
        };
        await db.Items.AddAsync(entity);
        await db.SaveChangesAsync();

        TenantContext.CurrentTenantId = null;

        TestController controller = new(
            db,
            new StubLicenseGuard(true),
            new MTokenInfo { MultiTenantEnabled = true },
            BuildConfig(true));

        Assert.Throws<UnauthorizedAccessException>(() => controller.Apply(db.Items.IgnoreQueryFilters()).ToList());
    }

    [Fact]
    public void SetTenantId_MultiTenantEnabled_WithoutTenant_ThrowsUnauthorized()
    {
        using TenantIsolationDbContext db = CreateContext(Guid.NewGuid().ToString());
        TenantContext.CurrentTenantId = null;

        TestController controller = new(
            db,
            new StubLicenseGuard(true),
            new MTokenInfo { MultiTenantEnabled = true },
            BuildConfig(true));

        Assert.Throws<UnauthorizedAccessException>(() =>
        {
            TenantScopedEntity entity = new()
            {
                Name = "a"
            };
            controller.Set(entity);
        });
    }

    [Fact]
    public async Task ApplyTenantFilter_WhenFeatureNotLicensed_ThrowsInvalidOperation()
    {
        await using TenantIsolationDbContext db = CreateContext(Guid.NewGuid().ToString());
        TenantScopedEntity entity = new()
        {
            Name = "a",
            TenantId = "tenant-1"
        };
        await db.Items.AddAsync(entity);
        await db.SaveChangesAsync();

        TenantContext.CurrentTenantId = "tenant-1";

        TestController controller = new(
            db,
            new StubLicenseGuard(false),
            new MTokenInfo { MultiTenantEnabled = true },
            BuildConfig(true));

        Assert.Throws<InvalidOperationException>(() => controller.Apply(db.Items.IgnoreQueryFilters()).ToList());
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public async Task ApplyTenantFilter_MultiTenantDisabled_DoesNotBlock_WhenTenantMissing()
    {
        await using TenantIsolationDbContext db = CreateContext(Guid.NewGuid().ToString());
        TenantScopedEntity entity = new()
        {
            Name = "a",
            TenantId = "tenant-1"
        };
        await db.Items.AddRangeAsync(
            entity,
            new TenantScopedEntity { Name = "b", TenantId = "tenant-2" });
        await db.SaveChangesAsync();

        TenantContext.CurrentTenantId = null;

        TestController controller = new(
            db,
            new StubLicenseGuard(false),
            new MTokenInfo { MultiTenantEnabled = false },
            BuildConfig(false));

        List<TenantScopedEntity> items = [.. controller.Apply(db.Items.IgnoreQueryFilters())];
        Assert.Equal(2, items.Count);
    }
}
