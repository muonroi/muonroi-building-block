using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

public class MRepositoryTests
{
    private static MAuthenticateInfoContext CreateAuth()
    {
        MAuthenticateInfoContext auth = new(false)
        {
            CurrentUserGuid = Guid.NewGuid().ToString()
        };
        return auth;
    }

    [Fact]
    public async Task AddBatchAsync_AddsEntities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_batch").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        int result = await repo.AddBatchAsync([
            user
        ]);
        Assert.Equal(1, result);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEntity()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("update_user").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([user]);
        user.Name = "changed";
        int result = await repo.UpdateAsync(user);
        Assert.Equal(1, result);
        MUser dbUser = await db.Users.FirstAsync();
        Assert.Equal("changed", dbUser.Name);
    }

    [Fact]
    public async Task DeleteBatchAsync_MarksDeleted()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_batch").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([user]);
        int result = await repo.DeleteBatchAsync(u => u.Id > 0);
        Assert.Equal(1, result);
        MUser dbUser = await db.Users.FirstAsync();
        Assert.True(dbUser.IsDeleted);
    }

    [Fact]
    public void Add_Duplicate_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("dup").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        repo.Add(user);
        Assert.Throws<MInternalException>(() => repo.Add(user));
    }

    [Fact]
    public async Task ExecuteStoredProcedureAsync_InvalidName_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("sp").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await Assert.ThrowsAsync<MArgumentException>(() =>
        {
            object[] parameters = [];
            return repo.ExecuteStoredProcedureAsync("", parameters);
        });
    }

    [Fact]
    public async Task BulkInsertAsync_DbError_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<FailingDbContext> options = new DbContextOptionsBuilder<FailingDbContext>().UseInMemoryDatabase("bulk_fail").Options;
        using FailingDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await Assert.ThrowsAsync<MInternalException>(() =>
        {
            MUser user = new()
            {
                UserName = "u",
                EmailAddress = "e",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            return repo.BulkInsertAsync([
                user
            ]);
        });
    }

    [Fact]
    public void CurrentUserId_Returns_Value_Or_Empty()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("current_user").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext auth = new(false)
        {
            CurrentUserGuid = "guid"
        };
        MRepository<MUser> repo = new(db, auth, new TestLicenseGuard());
        Assert.Equal("guid", repo.CurrentUserId);

        auth = new MAuthenticateInfoContext(false);
        repo = new MRepository<MUser>(db, auth, new TestLicenseGuard());
        Assert.Equal(string.Empty, repo.CurrentUserId);
    }

    [Fact]
    public void CurrentUsername_Returns_Value_Or_Empty()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("current_username").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext auth = new(false)
        {
            CurrentUsername = "user"
        };
        MRepository<MUser> repo = new(db, auth, new TestLicenseGuard());
        Assert.Equal("user", repo.CurrentUsername);

        auth = new MAuthenticateInfoContext(false);
        repo = new MRepository<MUser>(db, auth, new TestLicenseGuard());
        Assert.Equal(string.Empty, repo.CurrentUsername);
    }

    [Fact]
    public void UnitOfWork_Returns_DbContext()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("unit_of_work").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        Assert.Same(db, repo.UnitOfWork);
    }

    private class TenantRepo(MDbContext db, MAuthenticateInfoContext ctx)
        : MRepository<MUser>(db, ctx, new TestLicenseGuard())
    {
        public string? GetTenant()
        {
            return TenantId;
        }
    }

    [Fact]
    public void TenantId_Returns_CurrentTenantId_Or_Null()
    {
        string tenant = Guid.NewGuid().ToString();
        TenantContext.CurrentTenantId = tenant;
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("tenant_id").Options;
        using TestDbContext db = new(options);
        TenantRepo repo = new(db, CreateAuth());
        Assert.Equal(tenant, repo.GetTenant());

        TenantContext.CurrentTenantId = null;
        repo = new TenantRepo(db, CreateAuth());
        Assert.Null(repo.GetTenant());
    }

    [Fact]
    public async Task DeleteAsync_MarksEntityDeleted()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_async").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, new MAuthenticateInfoContext(false), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([user]);
        _ = await repo.DeleteAsync(user);
        Assert.True(user.IsDeleted);
        Assert.Null(user.DeletedUserId);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingEntity_AttachesAndMarksDeleted()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_async_notexist").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "x",
            EmailAddress = "x@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.DeleteAsync(user);
        Assert.True(user.IsDeleted);
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_Returns_Zero_When_NullInput()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_update_null").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.AddOrUpdateBatchAsync(null!);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_Updates_And_Adds()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_update_work").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser existing = new()
        {
            UserName = "a",
            EmailAddress = "a@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([existing]);
        existing.Name = "changed";
        MUser newUser = new()
        {
            UserName = "b",
            EmailAddress = "b@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        int result = await repo.AddOrUpdateBatchAsync([existing, newUser]);
        Assert.Equal(1, await db.Users.CountAsync(u => u.UserName == "b"));
        MUser dbUser = await db.Users.FirstAsync(u => u.UserName == "a");
        Assert.Equal("changed", dbUser.Name);
        Assert.True(result > 0);
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_EmptyList_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_update_empty").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.AddOrUpdateBatchAsync([]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_UpdatesExistingEntity()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_update_existing").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "e",
            EmailAddress = "e@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([user]);
        user.Name = "changed";
        int result = await repo.AddOrUpdateBatchAsync([user]);
        MUser dbUser = await db.Users.FirstAsync();
        Assert.Equal("changed", dbUser.Name);
        Assert.True(result > 0);
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_AddsNewEntities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_update_new").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user1 = new()
        {
            UserName = "n1",
            EmailAddress = "n1@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MUser user2 = new()
        {
            UserName = "n2",
            EmailAddress = "n2@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        int result = await repo.AddOrUpdateBatchAsync([user1, user2]);
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.True(result > 0);
    }

    [Fact]
    public async Task DeleteBatchAsync_NullList_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_null_list").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.DeleteBatchAsync((IEnumerable<MUser>)null!);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteBatchAsync_NoMatch_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_nomatch").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.DeleteBatchAsync(u => u.Id < 0);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteBatchAsync_NonExistingEntities_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_nonexist_list").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "d",
            EmailAddress = "d@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        int result = await repo.DeleteBatchAsync([user]);
        Assert.True(user.IsDeleted);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task DeleteBatchAsync_ValidEntities_DeletesSuccessfully()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_valid_list").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "v",
            EmailAddress = "v@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([user]);
        int result = await repo.DeleteBatchAsync([user]);
        Assert.Equal(1, result);
        MUser dbUser = await db.Users.FirstAsync();
        Assert.True(dbUser.IsDeleted);
    }

    [Fact]
    public async Task DeleteBatchAsync_EmptyList_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("delete_empty_list").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.DeleteBatchAsync([]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ExecuteTransactionAsync_Commits_When_ResultOk()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(conn).Options;
        using TestDbContext db = new(options);
        await db.Database.EnsureCreatedAsync();
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await repo.ExecuteTransactionAsync(async () =>
        {
            MUser user = new()
            {
                UserName = "t",
                EmailAddress = "t@a.com",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();
            return new MVoidMethodResult();
        });
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task ExecuteTransactionAsync_RollsBack_When_ResultNotOk()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(conn).Options;
        using TestDbContext db = new(options);
        await db.Database.EnsureCreatedAsync();
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await repo.ExecuteTransactionAsync(async () =>
        {
            MUser user = new()
            {
                UserName = "t2",
                EmailAddress = "t2@a.com",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();
            MVoidMethodResult r = new();
            r.AddError("err");
            return r;
        });
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task ExecuteTransactionAsync_NullAction_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("exec_null").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await Assert.ThrowsAsync<NullReferenceException>(() => repo.ExecuteTransactionAsync(null!));
    }

    [Fact]
    public async Task RollbackTransactionAsync_RollsBack()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        using SqliteConnection conn = new("DataSource=:memory:");
        await conn.OpenAsync();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(conn).Options;
        using TestDbContext db = new(options);
        await db.Database.EnsureCreatedAsync();
        await db.BeginTransactionAsync();
        MUser user = new()
        {
            UserName = "rb",
            EmailAddress = "r@b.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await repo.RollbackTransactionAsync();
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task RollbackTransactionAsync_NoTransaction_DoesNothing()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("rollback_none").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await repo.RollbackTransactionAsync();
        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task SoftRestoreAsync_RestoresDeletedEntity()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("soft_restore").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "s",
            EmailAddress = "s@a.com",
            Name = "n",
            Surname = "s",
            Password = "p",
            IsDeleted = true,
            DeletionTime = DateTime.UtcNow,
            DeletedDateTs = DateTime.UtcNow.GetTimeStamp(true)
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        bool result = await repo.SoftRestoreAsync(user);
        Assert.True(result);
        Assert.False(user.IsDeleted);
    }

    [Fact]
    public async Task SoftRestoreAsync_NonExistingEntity_ReturnsFalse()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("soft_restore_none").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "s2",
            EmailAddress = "s2@a.com",
            Name = "n",
            Surname = "s",
            Password = "p",
            IsDeleted = true
        };
        bool result = await repo.SoftRestoreAsync(user);
        Assert.False(result);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_InvalidName_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("scalar_invalid").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await Assert.ThrowsAsync<MArgumentException>(() => repo.ExecuteStoredProcedureScalarAsync<int>(""));
    }

    private static MRepository<MUser> CreateRepoWithFakeSp(object? result, out DbCommand command)
    {
        command = Substitute.For<DbCommand>();
        command.ExecuteScalarAsync().Returns(Task.FromResult(result));
        DbConnection conn = Substitute.For<DbConnection>();
        conn.CreateCommand().Returns(command);
        ConnectionState state = ConnectionState.Closed;
        conn.State.Returns(_ => state);
        conn.OpenAsync().Returns(_ =>
        {
            state = ConnectionState.Open;
            return Task.CompletedTask;
        });

        IRelationalConnection relConn = Substitute.For<IRelationalConnection>();
        relConn.DbConnection.Returns(conn);

        ServiceCollection services = [];
        services.AddEntityFrameworkSqlite();
        services.AddSingleton(relConn);
        ServiceProvider provider = services.BuildServiceProvider();

        DbContextOptions<FakeDbContext> options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseSqlite(conn)
            .UseInternalServiceProvider(provider)
            .Options;

        FakeDbContext db = new(options);
        return new MRepository<MUser>(db, CreateAuth(), new TestLicenseGuard());
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_ReturnsValue()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        MRepository<MUser> repo = CreateRepoWithFakeSp(5, out DbCommand? cmd);
        int val = await repo.ExecuteStoredProcedureScalarAsync<int>("proc");
        Assert.Equal(5, val);
        await cmd.Received(1).ExecuteScalarAsync();
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_NullResult_ReturnsDefault()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        MRepository<MUser> repo = CreateRepoWithFakeSp(null, out _);
        int val = await repo.ExecuteStoredProcedureScalarAsync<int>("proc");
        Assert.Equal(0, val);
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_CommandThrows_Propagates()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbCommand cmd = Substitute.For<DbCommand>();
        cmd.ExecuteScalarAsync().Returns(_ => throw new InvalidOperationException());
        DbConnection conn = Substitute.For<DbConnection>();
        conn.CreateCommand().Returns(cmd);
        ConnectionState state = ConnectionState.Closed;
        conn.State.Returns(_ => state);
        conn.OpenAsync().Returns(_ =>
        {
            state = ConnectionState.Open;
            return Task.CompletedTask;
        });

        IRelationalConnection relConn = Substitute.For<IRelationalConnection>();
        relConn.DbConnection.Returns(conn);
        ServiceCollection services = [];
        services.AddEntityFrameworkSqlite();
        services.AddSingleton(relConn);
        ServiceProvider provider = services.BuildServiceProvider();
        DbContextOptions<FakeDbContext> options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseSqlite(conn)
            .UseInternalServiceProvider(provider)
            .Options;
        FakeDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await Assert.ThrowsAsync<MInternalException>(() => repo.ExecuteStoredProcedureScalarAsync<int>("proc"));
    }

    [Fact]
    public async Task ExecuteStoredProcedureScalarAsync_NullParameters_DoesNotFail()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        MRepository<MUser> repo = CreateRepoWithFakeSp(1, out _);
        int val = await repo.ExecuteStoredProcedureScalarAsync<int>("proc", null!);
        Assert.Equal(1, val);
    }

    [Fact]
    public async Task UpdateBatchAsync_UpdatesEntities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("update_batch").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser u1 = new()
        {
            UserName = "u1",
            EmailAddress = "u1@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MUser u2 = new()
        {
            UserName = "u2",
            EmailAddress = "u2@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([u1, u2]);
        int result = await repo.UpdateBatchAsync(_ => true, u => u.Name = "x");
        Assert.Equal(2, result);
        Assert.All(db.Users, e => Assert.Equal("x", e.Name));
    }

    [Fact]
    public async Task UpdateBatchAsync_NoEntities_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("update_batch_none").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.UpdateBatchAsync(u => false, u => u.Name = "x");
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AddBatchAsync_NullEntities_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_batch_null").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.AddBatchAsync(null!);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AddBatchAsync_EmptyList_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_batch_empty").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.AddBatchAsync([]);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task AddBatchAsync_DomainEvent_OnFirstEntityOnly()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_batch_event").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser u1 = new()
        {
            UserName = "e1",
            EmailAddress = "e1@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MUser u2 = new()
        {
            UserName = "e2",
            EmailAddress = "e2@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        int result = await repo.AddBatchAsync([u1, u2]);
        Assert.Equal(2, result);
        Assert.Contains(u1.DomainEvents, d => d is MEntitiesCreatedEvent<MUser>);
        Assert.Empty(u2.DomainEvents);
    }

    [Fact]
    public async Task AddBatchAsync_DbError_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<FailingDbContext> options = new DbContextOptionsBuilder<FailingDbContext>().UseInMemoryDatabase("add_batch_fail").Options;
        using FailingDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await Assert.ThrowsAsync<Exception>(() =>
        {
            MUser user = new()
            {
                UserName = "u",
                EmailAddress = "e",
                Name = "n",
                Surname = "s",
                Password = "p"
            };
            return repo.AddBatchAsync([
                user
            ]);
        });
    }

    [Fact]
    public async Task AddOrUpdateBatchAsync_DuplicateEntityId_UsesLatestData()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("add_update_dup").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser existing = new()
        {
            UserName = "dup",
            EmailAddress = "dup@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        _ = await repo.AddBatchAsync([existing]);

        MUser update1 = new()
        {
            EntityId = existing.EntityId,
            UserName = existing.UserName,
            EmailAddress = existing.EmailAddress,
            Name = "first",
            Surname = existing.Surname,
            Password = existing.Password
        };
        MUser update2 = new()
        {
            EntityId = existing.EntityId,
            UserName = existing.UserName,
            EmailAddress = existing.EmailAddress,
            Name = "second",
            Surname = existing.Surname,
            Password = existing.Password
        };

        int result = await repo.AddOrUpdateBatchAsync([update1, update2]);
        Assert.True(result > 0);
        MUser dbUser = await db.Users.FirstAsync();
        Assert.Equal("second", dbUser.Name);
    }

    [Fact]
    public async Task BulkInsertAsync_InsertsEntities()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("bulk_insert_success").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser u1 = new()
        {
            UserName = "b1",
            EmailAddress = "b1@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MUser u2 = new()
        {
            UserName = "b2",
            EmailAddress = "b2@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await repo.BulkInsertAsync([u1, u2]);
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Contains(u1.DomainEvents, d => d is MEntitiesCreatedEvent<MUser>);
    }

    [Fact]
    public async Task BulkInsertAsync_NullEntities_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("bulk_insert_null").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await Assert.ThrowsAsync<MArgumentException>(() => repo.BulkInsertAsync(null!));
    }

    [Fact]
    public async Task BulkInsertAsync_EmptyList_DoesNothing()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("bulk_insert_empty").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        await repo.BulkInsertAsync([]);
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task SoftRestoreAsync_NotDeleted_ReturnsFalse()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("soft_restore_not_deleted")
            .Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        MUser user = new()
        {
            UserName = "n",
            EmailAddress = "n@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        bool result = await repo.SoftRestoreAsync(user);
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAsync_NullEntity_ReturnsZero()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("update_null").Options;
        using TestDbContext db = new(options);
        MRepository<MUser> repo = new(db, CreateAuth(), new TestLicenseGuard());
        int result = await repo.UpdateAsync(null!);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Constructor_NullAuthContext_Throws()
    {
        TenantContext.CurrentTenantId = Guid.NewGuid().ToString();
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("ctor_auth_null").Options;
        using TestDbContext db = new(options);
        Assert.Throws<MArgumentException>(() => new MRepository<MUser>(db, null!, new TestLicenseGuard()));
    }

    [Fact]
    public void Constructor_NullDbContext_Throws()
    {
        Assert.Throws<MArgumentException>(() => new MRepository<MUser>(null!, CreateAuth(), new TestLicenseGuard()));
    }

    private class FailingDbContext(DbContextOptions<FailingDbContext> options) : MDbContext(options, new FakeMediator())
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw new Exception("fail");
        }
    }
}

