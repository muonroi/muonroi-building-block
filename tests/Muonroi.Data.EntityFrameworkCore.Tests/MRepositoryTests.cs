namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MRepositoryTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    private static MAuthenticateInfoContext CreateAuth()
    {
        return new MAuthenticateInfoContext(true)
        {
            CurrentUserGuid = Guid.NewGuid().ToString(),
            CurrentUsername = "tester"
        };
    }

    private static MRepository<MUser> CreateRepository(TestDbContext dbContext)
    {
        return new MRepository<MUser>(dbContext, CreateAuth(), new TestLicenseGuard(), new MDateTimeService());
    }

    [Fact]
    public async Task AddBatchAsync_Adds_Entities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext dbContext = new(options);
        MRepository<MUser> repository = CreateRepository(dbContext);

        int result = await repository.AddBatchAsync([CreateUser("batch-1")]);

        Assert.Equal(1, result);
        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_Updates_Entity()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext dbContext = new(options);
        MRepository<MUser> repository = CreateRepository(dbContext);
        MUser user = CreateUser("update");
        _ = await repository.AddBatchAsync([user]);

        user.Name = "Changed";
        int result = await repository.UpdateAsync(user);
        MUser dbUser = await dbContext.Users.FirstAsync();

        Assert.Equal(1, result);
        Assert.Equal("Changed", dbUser.Name);
    }

    [Fact]
    public async Task DeleteBatchAsync_Marks_Entity_Deleted()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext dbContext = new(options);
        MRepository<MUser> repository = CreateRepository(dbContext);
        MUser user = CreateUser("delete");
        _ = await repository.AddBatchAsync([user]);

        int result = await repository.DeleteBatchAsync(entity => entity.UserName == user.UserName);
        MUser dbUser = await dbContext.Users.FirstAsync();

        Assert.Equal(1, result);
        Assert.True(dbUser.IsDeleted);
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_Updates_Existing_And_Adds_New()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext dbContext = new(options);
        MRepository<MUser> repository = CreateRepository(dbContext);
        MUser existing = CreateUser("existing");
        _ = await repository.AddBatchAsync([existing]);

        existing.Name = "Updated";
        MUser created = CreateUser("created");

        int result = await repository.AddOrUpdateBatchAsync([existing, created]);
        MUser updated = await dbContext.Users.FirstAsync(x => x.UserName == existing.UserName);

        Assert.True(result > 0);
        Assert.Equal(2, await dbContext.Users.CountAsync());
        Assert.Equal("Updated", updated.Name);
    }

    [Fact]
    public async Task ExecuteTransactionAsync_Commits_When_Result_Ok()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        using TestDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();
        MRepository<MUser> repository = CreateRepository(dbContext);

        await repository.ExecuteTransactionAsync(async () =>
        {
            await dbContext.Users.AddAsync(CreateUser("tx-ok"));
            await dbContext.SaveChangesAsync();
            return new MVoidMethodResult();
        });

        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task ExecuteTransactionAsync_Rolls_Back_When_Result_Has_Error()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        using TestDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();
        MRepository<MUser> repository = CreateRepository(dbContext);

        await repository.ExecuteTransactionAsync(async () =>
        {
            await dbContext.Users.AddAsync(CreateUser("tx-fail"));
            await dbContext.SaveChangesAsync();

            MVoidMethodResult result = new();
            result.AddError("rollback");
            return result;
        });

        Assert.Equal(0, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task SoftRestoreAsync_Restores_Deleted_Entity()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext dbContext = new(options);
        MRepository<MUser> repository = CreateRepository(dbContext);
        MUser user = CreateUser("restore");
        _ = await repository.AddBatchAsync([user]);
        _ = await repository.DeleteBatchAsync(entity => entity.UserName == user.UserName);

        bool restored = await repository.SoftRestoreAsync(user);

        Assert.True(restored);
        Assert.False(user.IsDeleted);
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
