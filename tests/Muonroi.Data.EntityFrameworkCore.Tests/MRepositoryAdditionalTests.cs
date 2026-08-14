namespace Muonroi.Data.EntityFrameworkCore.Tests;

/// <summary>
/// Additional MRepository tests covering edge cases and methods not in MRepositoryTests.
/// </summary>
public class MRepositoryAdditionalTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    private static MAuthenticateInfoContext CreateAuth(string? userGuid = null)
    {
        return new MAuthenticateInfoContext(true)
        {
            CurrentUserGuid = userGuid ?? Guid.NewGuid().ToString(),
            CurrentUsername = "tester"
        };
    }

    private static MRepository<MUser> CreateRepository(TestDbContext dbContext, MAuthenticateInfoContext? auth = null)
    {
        return new MRepository<MUser>(dbContext, auth ?? CreateAuth(), new TestLicenseGuard(), new MDateTimeService());
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

    [Fact]
    public void Constructor_Throws_When_DbContext_Null()
    {
        Action act = () => new MRepository<MUser>(null!, CreateAuth(), new TestLicenseGuard(), new MDateTimeService());

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_AuthContext_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;
        using TestDbContext db = new(options);

        Action act = () => new MRepository<MUser>(db, null!, new TestLicenseGuard(), new MDateTimeService());

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_LicenseGuard_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;
        using TestDbContext db = new(options);

        Action act = () => new MRepository<MUser>(db, CreateAuth(), null!, new MDateTimeService());

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_DateTimeService_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;
        using TestDbContext db = new(options);

        Action act = () => new MRepository<MUser>(db, CreateAuth(), new TestLicenseGuard(), null!);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Add_Returns_Entity_With_New_EntityId()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);
        MUser user = CreateUser("add-single");

        MUser result = repo.Add(user);

        result.Should().NotBeNull();
        result.EntityId.Should().NotBe(Guid.Empty);
        result.CreationTime.Should().NotBe(default);
    }

    [Fact]
    public void Add_Throws_When_Entity_Already_Tracked()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);
        MUser user = CreateUser("add-dup");
        repo.Add(user);

        Action act = () => repo.Add(user);

        act.Should().Throw<MInternalException>();
    }

    [Fact]
    public async Task DeleteAsync_Marks_Entity_As_Deleted()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);
        MUser user = CreateUser("del-single");
        _ = await repo.AddBatchAsync([user]);

        bool result = await repo.DeleteAsync(user);

        result.Should().BeTrue();
        user.IsDeleted.Should().BeTrue();
        user.DeletionTime.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_Returns_Zero_When_Null()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        int result = await repo.UpdateAsync(null);

        result.Should().Be(0);
    }

    [Fact]
    public async Task AddBatchAsync_Returns_Zero_For_Null_Input()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        int result = await repo.AddBatchAsync(null);

        result.Should().Be(0);
    }

    [Fact]
    public async Task AddBatchAsync_Returns_Zero_For_Empty_Input()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        int result = await repo.AddBatchAsync([]);

        result.Should().Be(0);
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_Returns_Zero_For_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        int result = await repo.AddOrUpdateBatchAsync(null);

        result.Should().Be(0);
    }

    [Fact]
    public async Task DeleteBatchAsync_ByPredicate_Returns_Zero_When_No_Match()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        int result = await repo.DeleteBatchAsync(u => u.UserName == "nonexistent");

        result.Should().Be(0);
    }

    [Fact]
    public async Task DeleteBatchAsync_ByEntities_Returns_Zero_For_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        int result = await repo.DeleteBatchAsync((IEnumerable<MUser>?)null);

        result.Should().Be(0);
    }

    [Fact]
    public async Task DeleteBatchAsync_ByEntities_Deletes_Entities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);
        MUser user = CreateUser("del-batch");
        _ = await repo.AddBatchAsync([user]);

        int result = await repo.DeleteBatchAsync([user]);

        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateBatchAsync_Returns_Zero_When_No_Match()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        int result = await repo.UpdateBatchAsync(u => u.UserName == "nonexistent", u => u.Name = "Changed");

        result.Should().Be(0);
    }

    [Fact]
    public async Task UpdateBatchAsync_Updates_Matching_Entities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);
        _ = await repo.AddBatchAsync([CreateUser("ubatch-1"), CreateUser("ubatch-2")]);

        int result = await repo.UpdateBatchAsync(
            u => u.UserName.StartsWith("user-ubatch"),
            u => u.Name = "BatchUpdated");

        result.Should().BeGreaterThan(0);
        List<MUser> updated = await db.Users.Where(u => u.Name == "BatchUpdated").ToListAsync();
        updated.Should().HaveCount(2);
    }

    [Fact]
    public async Task SoftRestoreAsync_Returns_False_When_Not_Deleted()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);
        MUser user = CreateUser("not-deleted");
        _ = await repo.AddBatchAsync([user]);

        bool result = await repo.SoftRestoreAsync(user);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task BulkInsertAsync_Throws_On_Null()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        Func<Task> act = () => repo.BulkInsertAsync(null!);

        await act.Should().ThrowAsync<MArgumentException>();
    }

    [Fact]
    public async Task BulkInsertAsync_Does_Nothing_For_Empty_Collection()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        await repo.BulkInsertAsync([]);

        int count = await db.Users.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task BulkInsertAsync_Inserts_Entities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        await repo.BulkInsertAsync([CreateUser("bulk-1"), CreateUser("bulk-2")]);

        int count = await db.Users.CountAsync();
        count.Should().Be(2);
    }

    [Fact]
    public void CurrentUserId_Returns_Auth_Value()
    {
        string userId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db, CreateAuth(userId));

        repo.CurrentUserId.Should().Be(userId);
    }

    [Fact]
    public void CurrentUsername_Returns_Auth_Value()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        repo.CurrentUsername.Should().Be("tester");
    }

    [Fact]
    public void UnitOfWork_Returns_DbContext()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        repo.UnitOfWork.Should().BeSameAs(db);
    }

    [Fact]
    public async Task RollbackTransactionAsync_NoOp_When_No_Transaction()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);

        // Should not throw when there is no transaction
        await repo.RollbackTransactionAsync();
    }

    [Fact]
    public async Task ExecuteTransactionAsync_Runs_Action_Directly_On_InMemory()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        using TestDbContext db = new(options);
        MRepository<MUser> repo = CreateRepository(db);
        bool executed = false;

        await repo.ExecuteTransactionAsync(async () =>
        {
            executed = true;
            return await Task.FromResult(new MVoidMethodResult());
        });

        executed.Should().BeTrue();
    }
}
