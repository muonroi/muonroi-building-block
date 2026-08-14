namespace Muonroi.Data.EntityFrameworkCore.Tests;

public class MQueryTests
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

    private static (TestDbContext db, MQuery<MUser> query) CreateSut()
    {
        TenantContext.CurrentTenantId = null;
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;

        TestDbContext db = new(options);
        MQuery<MUser> query = new(db, CreateAuth(), new TestLicenseGuard());
        return (db, query);
    }

    private static MUser CreateUser(string suffix, bool deleted = false)
    {
        return new MUser
        {
            EntityId = Guid.NewGuid(),
            UserName = $"user-{suffix}",
            EmailAddress = $"{suffix}@muonroi.test",
            Name = "Test",
            Surname = "User",
            Password = "p@ss",
            IsDeleted = deleted
        };
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Entity_When_Found()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        MUser user = CreateUser("byid");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        MUser? result = await query.GetByIdAsync((int)user.Id);

        result.Should().NotBeNull();
        result!.UserName.Should().Be("user-byid");
    }

    [Fact]
    public async Task GetByIdAsync_Returns_Null_When_NotFound()
    {
        (_, MQuery<MUser> query) = CreateSut();

        MUser? result = await query.GetByIdAsync(999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByGuidAsync_Returns_Entity_When_Found()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        MUser user = CreateUser("byguid");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        MUser? result = await query.GetByGuidAsync(user.EntityId);

        result.Should().NotBeNull();
        result!.UserName.Should().Be("user-byguid");
    }

    [Fact]
    public async Task GetByGuidAsync_Returns_Null_When_NotFound()
    {
        (_, MQuery<MUser> query) = CreateSut();

        MUser? result = await query.GetByGuidAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_Returns_NonDeleted_Entities()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        db.Users.Add(CreateUser("active1"));
        db.Users.Add(CreateUser("active2"));
        db.Users.Add(CreateUser("deleted", deleted: true));
        await db.SaveChangesAsync();

        List<MUser>? result = await query.GetAllAsync();

        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_Paged_Returns_PagedResult()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        for (int i = 0; i < 5; i++)
        {
            db.Users.Add(CreateUser($"paged-{i}"));
        }
        await db.SaveChangesAsync();

        MPagedResult<MUser>? result = await query.GetAllAsync(1, 2);

        result.Should().NotBeNull();
        result!.Items.Count().Should().Be(2);
        result.RowCount.Should().Be(5);
        result.CurrentPage.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task GetByConditionAsync_Filters_Correctly()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        db.Users.Add(CreateUser("cond-match"));
        db.Users.Add(CreateUser("cond-other"));
        await db.SaveChangesAsync();

        List<MUser> result = await query.GetByConditionAsync(u => u.UserName == "user-cond-match");

        result.Should().HaveCount(1);
        result[0].UserName.Should().Be("user-cond-match");
    }

    [Fact]
    public async Task AnyGuidAsync_Returns_True_When_Exists()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        MUser user = CreateUser("anyguid");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        bool result = await query.AnyGuidAsync(user.EntityId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnyGuidAsync_Returns_False_When_NotExists()
    {
        (_, MQuery<MUser> query) = CreateSut();

        bool result = await query.AnyGuidAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AnyAsync_ById_Returns_True_When_Exists()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        MUser user = CreateUser("anyid");
        db.Users.Add(user);
        await db.SaveChangesAsync();

        bool result = await query.AnyAsync((int)user.Id);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AnyAsync_ByPredicate_Returns_Correct_Result()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        db.Users.Add(CreateUser("pred1"));
        await db.SaveChangesAsync();

        bool exists = await query.AnyAsync(u => u.UserName == "user-pred1");
        bool missing = await query.AnyAsync(u => u.UserName == "nonexistent");

        exists.Should().BeTrue();
        missing.Should().BeFalse();
    }

    [Fact]
    public async Task CountAsync_Without_Predicate_Returns_Total()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        db.Users.Add(CreateUser("c1"));
        db.Users.Add(CreateUser("c2"));
        db.Users.Add(CreateUser("c3"));
        await db.SaveChangesAsync();

        int count = await query.CountAsync();

        count.Should().Be(3);
    }

    [Fact]
    public async Task CountAsync_With_Predicate_Returns_Filtered_Count()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        db.Users.Add(CreateUser("cf1"));
        db.Users.Add(CreateUser("cf2"));
        db.Users.Add(CreateUser("other"));
        await db.SaveChangesAsync();

        int count = await query.CountAsync(u => u.UserName.StartsWith("user-cf"));

        count.Should().Be(2);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_Returns_Match()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        db.Users.Add(CreateUser("first"));
        await db.SaveChangesAsync();

        MUser? result = await query.FirstOrDefaultAsync(u => u.UserName == "user-first");

        result.Should().NotBeNull();
        result!.UserName.Should().Be("user-first");
    }

    [Fact]
    public async Task FirstOrDefaultAsync_Returns_Null_When_No_Match()
    {
        (_, MQuery<MUser> query) = CreateSut();

        MUser? result = await query.FirstOrDefaultAsync(u => u.UserName == "nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ExistsAsync_Returns_Correct_Result()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        db.Users.Add(CreateUser("exists"));
        await db.SaveChangesAsync();

        bool exists = await query.ExistsAsync(u => u.UserName == "user-exists");
        bool missing = await query.ExistsAsync(u => u.UserName == "nope");

        exists.Should().BeTrue();
        missing.Should().BeFalse();
    }

    [Fact]
    public async Task GetListPaging_Returns_Correct_Page()
    {
        (TestDbContext db, MQuery<MUser> query) = CreateSut();
        for (int i = 0; i < 10; i++)
        {
            db.Users.Add(CreateUser($"page-{i}"));
        }
        await db.SaveChangesAsync();

        MPagedResult<MUser> result = await query.GetListPaging(
            db.Users.Where(u => !u.IsDeleted).OrderBy(u => u.Id),
            2, 3);

        result.Items.Count().Should().Be(3);
        result.RowCount.Should().Be(10);
        result.CurrentPage.Should().Be(2);
    }

    [Fact]
    public void CurrentUserId_Returns_AuthContext_Value()
    {
        (_, MQuery<MUser> query) = CreateSut();

        query.CurrentUserId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CurrentUsername_Returns_AuthContext_Value()
    {
        (_, MQuery<MUser> query) = CreateSut();

        query.CurrentUsername.Should().Be("tester");
    }

    [Fact]
    public void Constructor_Throws_When_LicenseGuard_Null()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;
        TestDbContext db = new(options);

        Action act = () => new MQuery<MUser>(db, CreateAuth(), null!);

        act.Should().Throw<MArgumentException>();
    }
}
