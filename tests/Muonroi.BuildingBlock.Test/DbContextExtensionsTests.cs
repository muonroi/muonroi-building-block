namespace Muonroi.BuildingBlock.Test;

public class DbContextExtensionsTests
{
    [Fact]
    public async Task BulkInsertAsync_InsertsEntities()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(conn).Options;
        using TestDbContext db = new(options);
        await db.Database.EnsureCreatedAsync();
        MUser u = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.BulkInsertAsync([u]);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task BulkInsertAsync_NullOrEmptyList_DoesNothing()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(conn).Options;
        using TestDbContext db = new(options);
        await db.Database.EnsureCreatedAsync();
        await db.BulkInsertAsync<MUser>(null!);
        Assert.Equal(0, await db.Users.CountAsync());
        await db.BulkInsertAsync<MUser>([]);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task BulkInsertAsync_NullEntityInList_Throws()
    {
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(conn).Options;
        using TestDbContext db = new(options);
        await db.Database.EnsureCreatedAsync();
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        List<MUser> list =
            [user, null!];
        await Assert.ThrowsAsync<ArgumentNullException>(() => db.BulkInsertAsync(list));
        Assert.Equal(0, await db.Users.CountAsync());
    }
}
