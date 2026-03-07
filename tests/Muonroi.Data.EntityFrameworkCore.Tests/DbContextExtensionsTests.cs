namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class DbContextExtensionsTests
{
    private sealed class DbExtensionsTestDbContext(DbContextOptions<DbExtensionsTestDbContext> options)
        : MDbContext(options, new NoMediator(), new TestLicenseGuard(), null, new MDateTimeService())
    {
    }

    [Fact]
    public async Task BulkInsertAsync_InsertsEntities()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DbExtensionsTestDbContext> options = new DbContextOptionsBuilder<DbExtensionsTestDbContext>()
            .UseSqlite(connection)
            .Options;

        using DbExtensionsTestDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        MUser user = CreateUser("insert");

        await dbContext.BulkInsertAsync([user]);

        Assert.Equal(1, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task BulkInsertAsync_NullOrEmptyList_DoesNothing()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DbExtensionsTestDbContext> options = new DbContextOptionsBuilder<DbExtensionsTestDbContext>()
            .UseSqlite(connection)
            .Options;

        using DbExtensionsTestDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        await dbContext.BulkInsertAsync<MUser>(null!);
        await dbContext.BulkInsertAsync<MUser>([]);

        Assert.Equal(0, await dbContext.Users.CountAsync());
    }

    [Fact]
    public async Task BulkInsertAsync_NullEntityInList_Throws()
    {
        using SqliteConnection connection = new("DataSource=:memory:");
        await connection.OpenAsync();

        DbContextOptions<DbExtensionsTestDbContext> options = new DbContextOptionsBuilder<DbExtensionsTestDbContext>()
            .UseSqlite(connection)
            .Options;

        using DbExtensionsTestDbContext dbContext = new(options);
        await dbContext.Database.EnsureCreatedAsync();

        MUser user = CreateUser("null");
        List<MUser> entities = [user, null!];

        await Assert.ThrowsAsync<ArgumentNullException>(() => dbContext.BulkInsertAsync(entities));
        Assert.Equal(0, await dbContext.Users.CountAsync());
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
