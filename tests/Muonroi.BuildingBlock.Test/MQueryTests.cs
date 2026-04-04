using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MQueryTests
{
    private static MAuthenticateInfoContext CreateAuth()
    {
        MAuthenticateInfoContext auth = new(false)
        {
            CurrentUserGuid = Guid.NewGuid().ToString()
        };
        return auth;
    }

    private static MUser CreateUser(string username = "u")
    {
        MUser user = new()
        {
            UserName = username,
            EmailAddress = $"{username}@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        return user;
    }

    private class UserQuery(TestDbContext db, MAuthenticateInfoContext auth)
        : MQuery<MUser>(db, auth, new TestLicenseGuard())
    {
        public string? GetTenant()
        {
            return TenantId;
        }
    }

    private static DbContextOptions<TestDbContext> CreateOptions(string name)
    {
        return new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase(name).Options;
    }

    [Fact]
    public void CurrentUserId_Returns_Value()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("uid");
        using TestDbContext db = new(opt);
        MAuthenticateInfoContext auth = new(true)
        {
            CurrentUserGuid = "id"
        };
        UserQuery q = new(db, auth);
        Assert.Equal("id", q.CurrentUserId);
    }

    [Fact]
    public void CurrentUserId_Null_When_Not_Set()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("uid_null");
        using TestDbContext db = new(opt);
        MAuthenticateInfoContext auth = new(false);
        UserQuery q = new(db, auth);
        Assert.Equal(string.Empty, q.CurrentUserId);
    }

    [Fact]
    public void CurrentUsername_Returns_Value()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("uname");
        using TestDbContext db = new(opt);
        MAuthenticateInfoContext auth = new(true)
        {
            CurrentUsername = "user"
        };
        UserQuery q = new(db, auth);
        Assert.Equal("user", q.CurrentUsername);
    }

    [Fact]
    public void CurrentUsername_Null_When_Not_Set()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("uname_null");
        using TestDbContext db = new(opt);
        MAuthenticateInfoContext auth = new(false);
        UserQuery q = new(db, auth);
        Assert.Equal(string.Empty, q.CurrentUsername);
    }

    [Fact]
    public async Task Queryable_Returns_NotDeleted_Items()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("queryable");
        using TestDbContext db = new(opt);
        MUser user = new()
        {
            UserName = "u1",
            EmailAddress = "e1",
            Name = "n1",
            Surname = "s1",
            Password = "p"
        };
        _ = await db.Users.AddAsync(user);
        MUser entity = new()
        {
            UserName = "u2",
            EmailAddress = "e2",
            Name = "n2",
            Surname = "s2",
            Password = "p",
            IsDeleted = true
        };
        _ = await db.Users.AddAsync(entity);
        _ = await db.SaveChangesAsync();
        UserQuery q = new(db, new MAuthenticateInfoContext(false));
        List<MUser> list = await q.Queryable.ToListAsync();
        Assert.Single(list);
        Assert.Equal("u1", list[0].UserName);
    }

    [Fact]
    public void TenantId_Returns_CurrentTenant()
    {
        TenantContext.CurrentTenantId = "tenant";
        DbContextOptions<TestDbContext> opt = CreateOptions("tid");
        using TestDbContext db = new(opt);
        UserQuery q = new(db, new MAuthenticateInfoContext(false));
        Assert.Equal("tenant", q.GetTenant());
    }

    [Fact]
    public void Constructor_Null_Dependencies_Throws()
    {
        TenantContext.CurrentTenantId = "t";
        Assert.Throws<NullReferenceException>(() => new UserQuery(null!, null!));
    }

    [Fact]
    public async Task AnyAsync_Returns_True_When_Exists()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("any_true");
        using TestDbContext db = new(opt);
        MUser u = new()
        {
            UserName = "u",
            EmailAddress = "e",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await db.Users.AddAsync(u);
        _ = await db.SaveChangesAsync();
        UserQuery q = new(db, new MAuthenticateInfoContext(false));
        bool result = await q.AnyAsync(x => x.Id == u.Id);
        Assert.True(result);
    }

    [Fact]
    public async Task AnyAsync_Returns_False_When_Not_Exists()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("any_false");
        using TestDbContext db = new(opt);
        UserQuery q = new(db, new MAuthenticateInfoContext(false));
        bool result = await q.AnyAsync(x => x.Id == -1);
        Assert.False(result);
    }

    [Fact]
    public async Task AnyAsync_Null_Predicate_Throws()
    {
        TenantContext.CurrentTenantId = "t";
        DbContextOptions<TestDbContext> opt = CreateOptions("any_null");
        using TestDbContext db = new(opt);
        UserQuery q = new(db, new MAuthenticateInfoContext(false));
        await Assert.ThrowsAsync<MArgumentException>(() => q.AnyAsync(null!));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEntityOrNull()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_by_id").Options;
        using TestDbContext db = new(options);
        MUser user = CreateUser();
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        MUser? found = await query.GetByIdAsync((int)user.Id);
        MUser? missing = await query.GetByIdAsync(999);

        Assert.NotNull(found);
        Assert.Equal(user.Id, found!.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetByGuidAsync_ReturnsEntityOrNull()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_by_guid").Options;
        using TestDbContext db = new(options);
        MUser user = CreateUser();
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        MUser? found = await query.GetByGuidAsync(user.EntityId);
        MUser? missing = await query.GetByGuidAsync(Guid.NewGuid());

        Assert.NotNull(found);
        Assert.Equal(user.EntityId, found!.EntityId);
        Assert.Null(missing);
    }

    [Fact]
    public async Task GetByConditionAsync_ReturnsMatchingEntities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_by_condition").Options;
        using TestDbContext db = new(options);
        _ = await db.Users.AddAsync(CreateUser("a"));
        _ = await db.Users.AddAsync(CreateUser("b"));
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        List<MUser> match = await query.GetByConditionAsync(u => u.UserName == "a");
        List<MUser> none = await query.GetByConditionAsync(u => u.UserName == "c");

        Assert.Single(match);
        Assert.Equal("a", match[0].UserName);
        Assert.Empty(none);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_all").Options;
        using TestDbContext db = new(options);
        _ = await db.Users.AddAsync(CreateUser("a"));
        _ = await db.Users.AddAsync(CreateUser("b"));
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        List<MUser>? all = await query.GetAllAsync();
        Assert.Equal(2, all?.Count);

        using TestDbContext emptyDb = new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_all_empty").Options);
        MQuery<MUser> emptyQuery = new(emptyDb, CreateAuth(), new TestLicenseGuard());
        List<MUser>? empty = await emptyQuery.GetAllAsync();
        Assert.Empty(empty!);
    }

    [Fact]
    public async Task GetAllAsync_Paged_ReturnsPagedResult()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("get_all_paged").Options;
        using TestDbContext db = new(options);
        _ = await db.Users.AddAsync(CreateUser("a"));
        _ = await db.Users.AddAsync(CreateUser("b"));
        _ = await db.Users.AddAsync(CreateUser("c"));
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        MPagedResult<MUser>? result = await query.GetAllAsync(1, 2);
        Assert.Equal(3, result?.RowCount);
        Assert.Equal(2, result?.Items.Count());

        MPagedResult<MUser>? emptyResult = await query.GetAllAsync(2, 2);
        Assert.Equal(3, emptyResult?.RowCount);
        Assert.Single(emptyResult?.Items!);
    }

    [Fact]
    public async Task AnyGuidAsync_ReturnsExpected()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("any_guid").Options;
        using TestDbContext db = new(options);
        MUser user = CreateUser();
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        bool exists = await query.AnyGuidAsync(user.EntityId);
        bool notExists = await query.AnyGuidAsync(Guid.NewGuid());

        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public async Task AnyAsync_ReturnsExpected()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("any_id").Options;
        using TestDbContext db = new(options);
        MUser user = CreateUser();
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        bool existsId = await query.AnyAsync((int)user.Id);
        bool notExistsId = await query.AnyAsync(999);
        bool existsPredicate = await query.AnyAsync(u => u.UserName == user.UserName);
        bool notExistsPredicate = await query.AnyAsync(u => u.UserName == "none");

        Assert.True(existsId);
        Assert.False(notExistsId);
        Assert.True(existsPredicate);
        Assert.False(notExistsPredicate);
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("count_async").Options;
        using TestDbContext db = new(options);
        _ = await db.Users.AddAsync(CreateUser("a"));
        _ = await db.Users.AddAsync(CreateUser("b"));
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        int total = await query.CountAsync();
        int filtered = await query.CountAsync(u => u.UserName == "a");

        Assert.Equal(2, total);
        Assert.Equal(1, filtered);

        using TestDbContext emptyDb = new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("count_empty").Options);
        MQuery<MUser> emptyQuery = new(emptyDb, CreateAuth(), new TestLicenseGuard());
        int zero = await emptyQuery.CountAsync();
        Assert.Equal(0, zero);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsExpected()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("first_or_default").Options;
        using TestDbContext db = new(options);
        _ = await db.Users.AddAsync(CreateUser("a"));
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        MUser? user = await query.FirstOrDefaultAsync(u => u.UserName == "a");
        MUser? none = await query.FirstOrDefaultAsync(u => u.UserName == "b");

        Assert.NotNull(user);
        Assert.Equal("a", user!.UserName);
        Assert.Null(none);
    }

    [Fact]
    public async Task ExistsAsync_ReturnsExpected()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("exists_async").Options;
        using TestDbContext db = new(options);
        MUser user = CreateUser();
        _ = await db.Users.AddAsync(user);
        _ = await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());

        bool exists = await query.ExistsAsync(u => u.UserName == user.UserName);
        bool notExists = await query.ExistsAsync(u => u.UserName == "none");

        Assert.True(exists);
        Assert.False(notExists);
    }

    [Fact]
    public async Task GetListPaging_Returns_Paged_List()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("list_paging").Options;
        using TestDbContext db = new(options);
        for (int i = 0; i < 5; i++)
        {
            MUser user = new()
            {
                UserName = $"u{i}",
                EmailAddress = $"u{i}@a.com",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            await db.Users.AddAsync(user);
        }

        await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());
        MPagedResult<MUser> result = await query.GetListPaging(query.Queryable, 1, 2);
        Assert.Equal(5, result.RowCount);
        Assert.Equal(2, result.Items.Count());
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task GetListPaging_NoEntities_Returns_Empty()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("list_empty").Options;
        using TestDbContext db = new(options);
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());
        MPagedResult<MUser> result = await query.GetListPaging(query.Queryable, 1, 2);
        Assert.Equal(0, result.RowCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetListPaging_PageBeyondLimit_Returns_EmptyItems()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("list_over").Options;
        using TestDbContext db = new(options);
        for (int i = 0; i < 3; i++)
        {
            MUser user = new()
            {
                UserName = $"u{i}",
                EmailAddress = $"u{i}@a.com",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            await db.Users.AddAsync(user);
        }

        await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());
        MPagedResult<MUser> result = await query.GetListPaging(query.Queryable, 3, 2);
        Assert.Equal(3, result.RowCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetPagedAsync_Returns_Paged_List()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("paged_async").Options;
        using TestDbContext db = new(options);
        for (int i = 0; i < 5; i++)
        {
            MUser user = new()
            {
                UserName = $"u{i}",
                EmailAddress = $"u{i}@a.com",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            await db.Users.AddAsync(user);
        }

        await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());
        MPagedResult<MUser> result = await query.GetPagedAsync(query.Queryable, 1, 2, x => x);
        Assert.Equal(5, result.RowCount);
        Assert.Equal(2, result.Items.Count());
    }

    [Fact]
    public async Task GetPagedAsync_NoEntities_Returns_Empty()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("paged_async_empty").Options;
        using TestDbContext db = new(options);
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());
        MPagedResult<MUser> result = await query.GetPagedAsync(query.Queryable, 1, 2, x => x);
        Assert.Equal(0, result.RowCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondLimit_Returns_EmptyItems()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("paged_async_over").Options;
        using TestDbContext db = new(options);
        for (int i = 0; i < 3; i++)
        {
            MUser user = new()
            {
                UserName = $"u{i}",
                EmailAddress = $"u{i}@a.com",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            await db.Users.AddAsync(user);
        }

        await db.SaveChangesAsync();
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());
        MPagedResult<MUser> result = await query.GetPagedAsync(query.Queryable, 3, 2, x => x);
        Assert.Equal(3, result.RowCount);
        Assert.Empty(result.Items);
    }
}

