namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MDbContextTests
{
    private sealed class RecordingMediator : IMediator
    {
        public int Count { get; private set; }

        public Task<MResponse> Send<MResponse>(IRequest<MResponse> request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(MResponse)!);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(null);
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Count++;
            return Task.CompletedTask;
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Count++;
            return Task.CompletedTask;
        }

        public IAsyncEnumerable<MResponse> CreateStream<MResponse>(
            IStreamRequest<MResponse> request,
            CancellationToken cancellationToken = default)
        {
            return EmptyAsync<MResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<object?>();
        }

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class TestDbContext(
        DbContextOptions<TestDbContext> options,
        IMediator mediator,
        ILicenseGuard? licenseGuard = null,
        IMDateTimeService? dateTimeService = null)
        : MDbContext(options, mediator, licenseGuard, null, dateTimeService)
    {
    }

    private sealed class TestDomainEvent : Muonroi.Core.Abstractions.SeedWorks.IMDomainEvent, INotification
    {
    }

    private static DbContextOptions<TestDbContext> CreateSqliteOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
    }

    [Fact]
    public async Task HasActiveTransaction_Returns_Correct_State()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        using TestDbContext dbContext = new(
            CreateSqliteOptions(connection),
            new NoMediator(),
            new TestLicenseGuard(),
            new MDateTimeService());

        Assert.False(dbContext.HasActiveTransaction);

        IDbContextTransaction? transaction = await dbContext.BeginTransactionAsync();
        Assert.NotNull(transaction);
        Assert.True(dbContext.HasActiveTransaction);

        IDbContextTransaction? second = await dbContext.BeginTransactionAsync();
        Assert.Null(second);

        dbContext.RollbackTransaction();
        Assert.False(dbContext.HasActiveTransaction);
        Assert.Null(dbContext.GetCurrentTransaction());
    }

    [Fact]
    public async Task GetCurrentTransaction_Returns_Current_Or_Null()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        using TestDbContext dbContext = new(
            CreateSqliteOptions(connection),
            new NoMediator(),
            new TestLicenseGuard(),
            new MDateTimeService());

        Assert.Null(dbContext.GetCurrentTransaction());

        IDbContextTransaction? transaction = await dbContext.BeginTransactionAsync();
        Assert.Same(transaction, dbContext.GetCurrentTransaction());

        dbContext.RollbackTransaction();
        Assert.Null(dbContext.GetCurrentTransaction());
    }

    [Fact]
    public async Task SaveEntitiesAsync_Dispatches_DomainEvents()
    {
        RecordingMediator mediator = new();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext dbContext = new(options, mediator, new TestLicenseGuard(), new MDateTimeService());
        MUser user = CreateUser("dispatch");
        user.AddDomainEvent(new TestDomainEvent());

        await dbContext.Users.AddAsync(user);
        dbContext.TrackEntity(user);
        Guid result = await dbContext.SaveEntitiesAsync();

        Assert.NotEqual(Guid.Empty, result);
        Assert.Equal(1, mediator.Count);
    }

    [Fact]
    public async Task CommitTransactionAsync_Completes_And_Clears_Current_Transaction()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        using TestDbContext dbContext = new(
            CreateSqliteOptions(connection),
            new NoMediator(),
            new TestLicenseGuard(),
            new MDateTimeService());
        await dbContext.Database.EnsureCreatedAsync();

        IDbContextTransaction? transaction = await dbContext.BeginTransactionAsync();
        Assert.NotNull(transaction);

        await dbContext.CommitTransactionAsync(transaction!);

        Assert.False(dbContext.HasActiveTransaction);
        Assert.Null(dbContext.GetCurrentTransaction());
    }

    private static MUser CreateUser(string suffix)
    {
        return new MUser
        {
            UserName = $"user-{suffix}",
            EmailAddress = $"{suffix}@muonroi.test",
            Name = "Test",
            Surname = "User",
            Password = "p@ss"
        };
    }
}
