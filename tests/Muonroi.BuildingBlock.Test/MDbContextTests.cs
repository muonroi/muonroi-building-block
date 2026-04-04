namespace Muonroi.BuildingBlock.Test;

using Muonroi.Governance.License;
using Muonroi.Core.Abstractions.Exceptions;

public class MDbContextTests
{
    private class RecordingMediator : IMediator
    {
        public int Count { get; private set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Count++;
            return Task.CompletedTask;
        }

        public Task<MResponse> Send<MResponse>(IRequest<MResponse> request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(MResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(null);
        }

        public IAsyncEnumerable<MResponse> CreateStream<MResponse>(IStreamRequest<MResponse> request,
            CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<MResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return AsyncEnumerable.Empty<object?>();
        }
    }

    private class CustomDbContext(DbContextOptions<CustomDbContext> options, IMediator mediator)
        : MDbContext(options, mediator)
    {
    }

    private class FailingDbContext(DbContextOptions<FailingDbContext> options) : MDbContext(options, new FakeMediator())
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new Exception("fail");
        }
    }

    private class NullTransactionDbContext(DbContextOptions<NullTransactionDbContext> options)
        : MDbContext(options, new FakeMediator())
    {
        public new static Task<IDbContextTransaction?> BeginTransactionAsync()
        {
            return Task.FromResult<IDbContextTransaction?>(null);
        }
    }

    private class ThrowingTransactionDbContext(DbContextOptions<ThrowingTransactionDbContext> options)
        : MDbContext(options, new FakeMediator())
    {
        public new static Task<IDbContextTransaction?> BeginTransactionAsync()
        {
            throw new Exception("fail");
        }
    }

    private static DbContextOptions<T> CreateSqliteOptions<T>(SqliteConnection connection) where T : DbContext
    {
        return new DbContextOptionsBuilder<T>().UseSqlite(connection).Options;
    }

    [Fact]
    public async Task HasActiveTransaction_Returns_Correct_State()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<CustomDbContext> options = CreateSqliteOptions<CustomDbContext>(conn);
        using CustomDbContext db = new(options, new FakeMediator());
        Assert.False(db.HasActiveTransaction);
        IDbContextTransaction? tx = await db.BeginTransactionAsync();
        Assert.True(db.HasActiveTransaction);
        Assert.NotNull(tx);
        IDbContextTransaction? second = await db.BeginTransactionAsync();
        Assert.Null(second);
        Assert.True(db.HasActiveTransaction);
        db.RollbackTransaction();
        Assert.False(db.HasActiveTransaction);
    }

    [Fact]
    public async Task GetCurrentTransaction_Returns_Current_Or_Null()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<CustomDbContext> options = CreateSqliteOptions<CustomDbContext>(conn);
        using CustomDbContext db = new(options, new FakeMediator());
        Assert.Null(db.GetCurrentTransaction());
        IDbContextTransaction? tx = await db.BeginTransactionAsync();
        Assert.Same(tx, db.GetCurrentTransaction());
        db.RollbackTransaction();
        Assert.Null(db.GetCurrentTransaction());
    }

    [Fact]
    public void RollbackTransaction_Behaviors()
    {
        DbContextOptions<CustomDbContext> options = new DbContextOptionsBuilder<CustomDbContext>().UseInMemoryDatabase("rollback").Options;
        using CustomDbContext db = new(options, new FakeMediator());
        Exception ex = Record.Exception(() => db.RollbackTransaction());
        Assert.Null(ex);

        IDbContextTransaction tx = Substitute.For<IDbContextTransaction>();
        typeof(MDbContext).GetField("_currentTransaction", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(db,
            tx);
        db.RollbackTransaction();
        tx.Received(1).Rollback();
        Assert.Null(db.GetCurrentTransaction());

        tx = Substitute.For<IDbContextTransaction>();
        tx.When(t => t.Rollback()).Do(_ => throw new InvalidOperationException("fail"));
        typeof(MDbContext).GetField("_currentTransaction", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(db,
            tx);
        Assert.Throws<MInternalException>(() => db.RollbackTransaction());
        Assert.Null(db.GetCurrentTransaction());
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_Dispatches_Events()
    {
        RecordingMediator mediator = new();
        DbContextOptions<CustomDbContext> options = new DbContextOptionsBuilder<CustomDbContext>().UseInMemoryDatabase("dispatch").Options;
        using CustomDbContext db = new(options, mediator);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        user.AddDomainEvent(new MEntityCreatedEvent<MUser>(user));
        await db.Users.AddAsync(user);
        db.TrackEntity(user);
        _ = await db.SaveEntitiesAsync();
        Assert.Equal(1, mediator.Count);
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_NoEvents_NoPublish()
    {
        RecordingMediator mediator = new();
        DbContextOptions<CustomDbContext> options = new DbContextOptionsBuilder<CustomDbContext>().UseInMemoryDatabase("noevent").Options;
        using CustomDbContext db = new(options, mediator);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        _ = await db.SaveEntitiesAsync();
        Assert.Equal(0, mediator.Count);
    }

    [Fact]
    public async Task DispatchDomainEventsAsync_EventNull_Throws()
    {
        RecordingMediator mediator = new();
        DbContextOptions<CustomDbContext> options = new DbContextOptionsBuilder<CustomDbContext>().UseInMemoryDatabase("eventnull").Options;
        using CustomDbContext db = new(options, mediator);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        db.TrackEntity(user);
        FieldInfo fi = typeof(MEntity).GetField("_domainEvents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        List<INotification> list = (List<INotification>)fi.GetValue(user)!;
        list.Add(null!);
        await Assert.ThrowsAsync<NullReferenceException>(() => db.SaveEntitiesAsync());
    }

    [Fact]
    public async Task BeginTransactionAsync_Behaviors()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<CustomDbContext> options = CreateSqliteOptions<CustomDbContext>(conn);
        using CustomDbContext db = new(options, new FakeMediator());
        IDbContextTransaction? tx = await db.BeginTransactionAsync();
        Assert.NotNull(tx);
        IDbContextTransaction? second = await db.BeginTransactionAsync();
        Assert.Null(second);
        db.RollbackTransaction();
    }

    [Fact]
    public async Task BeginTransactionAsync_ReturnsNull()
    {
        DbContextOptions<NullTransactionDbContext> options = new DbContextOptionsBuilder<NullTransactionDbContext>().UseInMemoryDatabase("begin_null").Options;
        using NullTransactionDbContext db = new(options);
        IDbContextTransaction? tx = await NullTransactionDbContext.BeginTransactionAsync();
        Assert.Null(tx);
    }

    [Fact]
    public async Task BeginTransactionAsync_Throws()
    {
        DbContextOptions<ThrowingTransactionDbContext> options = new DbContextOptionsBuilder<ThrowingTransactionDbContext>().UseInMemoryDatabase("begin_throw")
            .Options;
        using ThrowingTransactionDbContext db = new(options);
        await Assert.ThrowsAsync<Exception>(() => ThrowingTransactionDbContext.BeginTransactionAsync());
    }

    [Fact]
    public async Task CommitTransactionAsync_Behaviors()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<CustomDbContext> options = CreateSqliteOptions<CustomDbContext>(conn);
        using CustomDbContext db = new(options, new FakeMediator());
        IDbContextTransaction? tx = await db.BeginTransactionAsync();
        await db.CommitTransactionAsync(tx!);
        Assert.False(db.HasActiveTransaction);
    }

    [Fact]
    public async Task CommitTransactionAsync_NoTransaction_Throws()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<CustomDbContext> options = CreateSqliteOptions<CustomDbContext>(conn);
        using CustomDbContext db = new(options, new FakeMediator());
        IDbContextTransaction dummy = Substitute.For<IDbContextTransaction>();
        await Assert.ThrowsAsync<MInternalException>(() => db.CommitTransactionAsync(dummy));
    }

    [Fact]
    public async Task CommitTransactionAsync_CommitFails()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<CustomDbContext> options = CreateSqliteOptions<CustomDbContext>(conn);
        using CustomDbContext db = new(options, new FakeMediator());
        IDbContextTransaction tx = Substitute.For<IDbContextTransaction>();
        typeof(MDbContext).GetField("_currentTransaction", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(db,
            tx);
        tx.When(t => t.CommitAsync()).Do(_ => throw new InvalidOperationException());
        await Assert.ThrowsAsync<MInternalException>(() => db.CommitTransactionAsync(tx));
        Assert.False(db.HasActiveTransaction);
    }

    [Fact]
    public async Task SaveEntitiesAsync_Behaviors()
    {
        RecordingMediator mediator = new();
        DbContextOptions<CustomDbContext> options = new DbContextOptionsBuilder<CustomDbContext>().UseInMemoryDatabase("save_ok").Options;
        using CustomDbContext db = new(options, mediator);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        db.TrackEntity(user);
        Guid id = await db.SaveEntitiesAsync();
        Assert.NotEqual(Guid.Empty, id);

        using CustomDbContext db2 = new(options, mediator);
        Guid noEntities = await db2.SaveEntitiesAsync();
        Assert.NotEqual(Guid.Empty, noEntities);
    }

    [Fact]
    public async Task SaveEntitiesAsync_Fails()
    {
        DbContextOptions<CustomDbContext> options = new DbContextOptionsBuilder<CustomDbContext>().UseInMemoryDatabase("save_fail").Options;
        using CustomDbContext db = new(options, new FakeMediator());
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        db.TrackEntity(user);
        FieldInfo fi = typeof(MEntity).GetField("_domainEvents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        List<INotification> list = (List<INotification>)fi.GetValue(user)!;
        list.Add(null!);
        await Assert.ThrowsAsync<NullReferenceException>(() => db.SaveEntitiesAsync());
    }


    [Fact]
    public async Task SaveEntitiesAsync_WithActiveTransaction_ReturnsSameId()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<CustomDbContext> options = CreateSqliteOptions<CustomDbContext>(conn);
        using CustomDbContext db = new(options, new FakeMediator());
        await db.Database.EnsureCreatedAsync();
        IDbContextTransaction? tx = await db.BeginTransactionAsync();
        Guid before = tx!.TransactionId;
        Guid id = await db.SaveEntitiesAsync();
        Assert.Equal(before, id);
        Assert.True(db.HasActiveTransaction);
        await db.CommitTransactionAsync(tx);
    }

    [Fact]
    public void AddDbContextConfigure_Behaviors()
    {
        Dictionary<string, string?> data = new()
        {
            ["DatabaseConfigs:DbType"] = nameof(DbTypes.Sqlite),
            ["DatabaseConfigs:ConnectionStrings:SqliteConnectionString"] = "DataSource=:memory:",
            ["EnableEncryption"] = "false"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(data).Build();
        ServiceCollection services = [];
        services.AddSingleton(config);
        services.AddSingleton<ITenantContext, TenantContext>();
        services.AddSingleton<IMediator>(new FakeMediator());
        services.AddSingleton<ILicenseGuard>(new TestLicenseGuard());
        LicenseConfigs configs = new()
        {
            ProjectSeed = "test-project-seed-1234"
        };
        services.AddSingleton(configs);
        services.AddDbContextConfigure<CustomDbContext, TestPerm>(config);
        ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetService<CustomDbContext>());
    }

    [Fact]
    public void AddDbContextConfigure_NullConfig_Throws()
    {
        ServiceCollection services = [];
        services.AddSingleton<ITenantContext, TenantContext>();
        IConfiguration? cfg = null;
        Assert.Throws<NullReferenceException>(() => services.AddDbContextConfigure<CustomDbContext, TestPerm>(cfg!));
    }

    [Fact]
    public void AddDbContextConfigure_InvalidConfig_Throws()
    {
        IConfiguration cfg = new ConfigurationBuilder().Build();
        ServiceCollection services = [];
        services.AddSingleton<ITenantContext, TenantContext>();
        Assert.Throws<InvalidDataException>(() => services.AddDbContextConfigure<CustomDbContext, TestPerm>(cfg));
    }

    [Fact]
    public async Task SaveEntitiesAsync_Null_TrackEntities_Throws()
    {
        RecordingMediator mediator = new();
        DbContextOptions<CustomDbContext> options = new DbContextOptionsBuilder<CustomDbContext>().UseInMemoryDatabase("save_null").Options;
        using CustomDbContext db = new(options, mediator);
        typeof(MDbContext).GetField("_trackEntities", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(db,
            null);
        await Assert.ThrowsAsync<MArgumentException>(() => db.SaveEntitiesAsync());
    }

    private class TenantEntity : MEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? TenantId { get; set; }
    }

    private class CombinedFilterEntity : MEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? TenantId { get; set; }
    }

    private class TenantIndexDbContext(DbContextOptions<TenantIndexDbContext> options)
        : MDbContext(options, new FakeMediator())
    {
        public DbSet<TenantEntity> Items { get; set; } = null!;
    }

    private class CombinedFilterDbContext(DbContextOptions<CombinedFilterDbContext> options)
        : MDbContext(options, new FakeMediator())
    {
        public DbSet<CombinedFilterEntity> Items { get; set; } = null!;
    }

    [Fact]
    public void OnModelCreating_Adds_Tenant_Index()
    {
        DbContextOptions<TenantIndexDbContext> options = new DbContextOptionsBuilder<TenantIndexDbContext>()
            .UseInMemoryDatabase("tenant_index").Options;
        using TenantIndexDbContext db = new(options);
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = db.Model.FindEntityType(typeof(TenantEntity))!;
        bool hasIndex = entity.GetIndexes().Any(i => i.Properties.Any(p => p.Name == "TenantId"));
        Assert.True(hasIndex);
    }

    [Fact]
    public async Task OnModelCreating_Combines_Tenant_And_Creator_Filters()
    {
        DbContextOptions<CombinedFilterDbContext> options = new DbContextOptionsBuilder<CombinedFilterDbContext>()
            .UseInMemoryDatabase("combined_filters").Options;

        Guid creatorA = Guid.NewGuid();
        Guid creatorB = Guid.NewGuid();

        using (CombinedFilterDbContext db = new(options))
        {
            CombinedFilterEntity entity = new()
            {
                Name = "tenant1-userA",
                TenantId = "tenant-1",
                CreatorUserId = creatorA
            };
            await db.Items.AddRangeAsync(
                entity,
                new CombinedFilterEntity { Name = "tenant1-userB", TenantId = "tenant-1", CreatorUserId = creatorB },
                new CombinedFilterEntity { Name = "tenant2-userA", TenantId = "tenant-2", CreatorUserId = creatorA }
            );
            await db.SaveChangesAsync();
        }

        TenantContext.CurrentTenantId = "tenant-1";
        UserContext.CurrentUserGuid = creatorA.ToString();

        using (CombinedFilterDbContext db = new(options))
        {
            List<CombinedFilterEntity> results = await db.Items.ToListAsync();
            Assert.Single(results);
            Assert.Equal("tenant1-userA", results[0].Name);
        }

        TenantContext.CurrentTenantId = null;
        UserContext.CurrentUserGuid = null;
    }
}
