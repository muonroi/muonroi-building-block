namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceCrudTests
{
    private static PermissionService<TestPerm, TestDbContext> CreateService(TestDbContext db)
    {
        TenantContext.CurrentTenantId = TenantContext.CurrentTenantId ?? Guid.NewGuid().ToString();
        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        return new PermissionService<TestPerm, TestDbContext>(db, ctx, new FakeDateTimeService());
    }

    [Fact]
    public async Task CreateRoleAsync_NewRole_Succeeds()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("create_role_ok").Options;
        using TestDbContext db = new(options);
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        CreateRoleRequestModel req = new()
        {
            Name = "admin",
            DisplayName = "Admin",
            IsStatic = false,
            IsDefault = false
        };

        MResponse<MRole> result = await svc.CreateRoleAsync(req, CancellationToken.None);
        Assert.True(result.IsOk);
        Assert.Single(db.Roles.Where(r => r.Name == "admin"));
    }

    [Fact]
    public async Task CreateRoleAsync_Duplicate_ReturnsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("create_role_dup").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "admin",
            DisplayName = "A",
            NormalizedName = "ADMIN"
        };
        _ = await db.Roles.AddAsync(role);
        _ = await db.SaveChangesAsync();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        CreateRoleRequestModel req = new()
        {
            Name = "admin",
            DisplayName = "Admin",
            IsStatic = false,
            IsDefault = false
        };

        MResponse<MRole> result = await svc.CreateRoleAsync(req, CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_Success()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_role_ok").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        AssignRoleRequestModel req = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        MResponse<object> result = await svc.AssignRoleToUserAsync(req, CancellationToken.None);
        Assert.True(result.IsOk);
        Assert.Single(db.UserRoles.Where(ur => ur.RoleId == role.EntityId && ur.UserId == user.EntityId));
    }

    [Fact]
    public async Task AssignRoleToUserAsync_UserAlreadyHasRole()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_role_dup").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Roles.AddAsync(role);
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        MUserRole entity = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        await db.UserRoles.AddAsync(entity);
        await db.SaveChangesAsync();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        AssignRoleRequestModel req = new()
        {
            RoleId = role.EntityId,
            UserId = user.EntityId
        };
        MResponse<object> result = await svc.AssignRoleToUserAsync(req, CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_RoleNotFound()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_role_role_nf").Options;
        using TestDbContext db = new(options);
        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        AssignRoleRequestModel req = new()
        {
            RoleId = Guid.NewGuid(),
            UserId = user.EntityId
        };
        MResponse<object> result = await svc.AssignRoleToUserAsync(req, CancellationToken.None);
        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_UserNotFound()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_role_user_nf").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        await db.Roles.AddAsync(role);
        await db.SaveChangesAsync();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        AssignRoleRequestModel req = new()
        {
            RoleId = role.EntityId,
            UserId = Guid.NewGuid()
        };
        MResponse<object> result = await svc.AssignRoleToUserAsync(req, CancellationToken.None);
        Assert.False(result.IsOk);
    }
}
