namespace Muonroi.BuildingBlock.Test.MultiTenant;

public enum TestPerm
{
    Read = 1,
    Write = 2
}

public class MultiTenantFlowTests
{
    [Fact]
    public async Task CreateUser_Isolated_PerTenant()
    {
        TenantContext.CurrentTenantId = "tenant1";
        DbContextOptions<TestDbContext> opt1 = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("t1_db").Options;
        using TestDbContext db1 = new(opt1);
        MUser user = new()
        {
            UserName = "u1",
            EmailAddress = "u1@a.com",
            Name = "A",
            Surname = "A",
            Password = "p"
        };
        _ = await db1.Users.AddAsync(user);
        _ = await db1.SaveChangesAsync();

        TenantContext.CurrentTenantId = "tenant2";
        DbContextOptions<TestDbContext> opt2 = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("t2_db").Options;
        using TestDbContext db2 = new(opt2);
        MUser entity = new()
        {
            UserName = "u2",
            EmailAddress = "u2@a.com",
            Name = "B",
            Surname = "B",
            Password = "p"
        };
        _ = await db2.Users.AddAsync(entity);
        _ = await db2.SaveChangesAsync();

        TenantContext.CurrentTenantId = "tenant1";
        Assert.Equal(1, await db1.Users.CountAsync());
        TenantContext.CurrentTenantId = "tenant2";
        Assert.Equal(1, await db2.Users.CountAsync());
    }

    [Fact]
    public void Login_With_Different_Tenants_Generates_Claim()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "defaultkey1234567890123456789012345",
            SigningKeysByTenant = new Dictionary<string, string>
            {
                ["tenant1"] = "t1key123456789012345678901234567",
                ["tenant2"] = "t2key123456789012345678901234567"
            },
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            MultiTenantEnabled = true,
            UseRsa = false
        };
        MAuthenticateTokenHelper<TestPerm> helper = new(info, new HmacTokenSigner(info.SymmetricSecretKey), new MDateTimeService());
        string token1 = helper.GenerateAuthenticateToken(
            new MUserModel("1", "u1", "v1", "name", "surname", "phone", "email", "tenant1"), [TestPerm.Read]);
        string token2 = helper.GenerateAuthenticateToken(
            new MUserModel("2", "u2", "v2", "name", "surname", "phone", "email", "tenant2"), [TestPerm.Read]);
        JwtSecurityToken jwt1 = new(token1);
        JwtSecurityToken jwt2 = new(token2);
        Assert.Equal("tenant1", jwt1.Claims.First(c => c.Type == ClaimConstants.TenantId).Value);
        Assert.Equal("tenant2", jwt2.Claims.First(c => c.Type == ClaimConstants.TenantId).Value);
        Assert.Equal("tenant1", jwt1.Header.Kid);
        Assert.Equal("tenant2", jwt2.Header.Kid);
    }

    [Fact]
    public async Task Permissions_Assigned_PerTenant()
    {
        TenantContext.CurrentTenantId = "t1";
        DbContextOptions<TestDbContext> opt1 = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("perm_t1").Options;
        using TestDbContext db1 = new(opt1);
        MUser u1 = new()
        {
            UserName = "u1",
            EmailAddress = "u1@a.com",
            Name = "n1",
            Surname = "s1",
            Password = "p"
        };
        MRole r1 = new()
        {
            Name = "r1",
            DisplayName = "r1",
            NormalizedName = "R1"
        };
        MPermission p1 = new()
        {
            Name = "Read",
            UiKey = "read"
        };
        _ = await db1.Users.AddAsync(u1);
        _ = await db1.Roles.AddAsync(r1);
        _ = await db1.Permissions.AddAsync(p1);
        _ = await db1.SaveChangesAsync();
        MUserRole role = new()
        {
            UserId = u1.EntityId,
            RoleId = r1.EntityId
        };
        _ = await db1.UserRoles.AddAsync(role);
        MRolePermission entity = new()
        {
            RoleId = r1.EntityId,
            PermissionId = p1.EntityId
        };
        _ = await db1.RolePermissions.AddAsync(entity);
        _ = await db1.SaveChangesAsync();

        TenantContext.CurrentTenantId = "t2";
        DbContextOptions<TestDbContext> opt2 = new DbContextOptionsBuilder<TestDbContext>().UseInMemoryDatabase("perm_t2").Options;
        using TestDbContext db2 = new(opt2);
        MUser u2 = new()
        {
            UserName = "u2",
            EmailAddress = "u2@a.com",
            Name = "n2",
            Surname = "s2",
            Password = "p"
        };
        MRole r2 = new()
        {
            Name = "r2",
            DisplayName = "r2",
            NormalizedName = "R2"
        };
        MPermission p2 = new()
        {
            Name = "Write",
            UiKey = "write"
        };
        _ = await db2.Users.AddAsync(u2);
        _ = await db2.Roles.AddAsync(r2);
        _ = await db2.Permissions.AddAsync(p2);
        _ = await db2.SaveChangesAsync();
        MUserRole userRole = new()
        {
            UserId = u2.EntityId,
            RoleId = r2.EntityId
        };
        _ = await db2.UserRoles.AddAsync(userRole);
        MRolePermission permission = new()
        {
            RoleId = r2.EntityId,
            PermissionId = p2.EntityId
        };
        _ = await db2.RolePermissions.AddAsync(permission);
        _ = await db2.SaveChangesAsync();

        TenantContext.CurrentTenantId = "t1";
        List<string> perms1 = await (from user in db1.Users
            join ur in db1.UserRoles on user.EntityId equals ur.UserId
            join rp in db1.RolePermissions on ur.RoleId equals rp.RoleId
            join perm in db1.Permissions on rp.PermissionId equals perm.EntityId
            where user.EntityId == u1.EntityId
            select perm.Name).ToListAsync();
        TenantContext.CurrentTenantId = "t2";
        List<string> perms2 = await (from user in db2.Users
            join ur in db2.UserRoles on user.EntityId equals ur.UserId
            join rp in db2.RolePermissions on ur.RoleId equals rp.RoleId
            join perm in db2.Permissions on rp.PermissionId equals perm.EntityId
            where user.EntityId == u2.EntityId
            select perm.Name).ToListAsync();
        _ = Assert.Single(perms1);
        Assert.Equal("Read", perms1[0]);
        _ = Assert.Single(perms2);
        Assert.Equal("Write", perms2[0]);
    }
}
