namespace Muonroi.BuildingBlock.Test;

public enum CornerPerm
{
    View = 1,
    Edit = 2
}

public class AuthServiceMenuMetadataTests
{
    private static (MTokenInfo Info, MAuthenticateTokenHelper<CornerPerm> Helper) CreateTokenHelper()
    {
        MTokenInfo info = new()
        {
            SymmetricSecretKey = "testkey123456789012345678901234567890",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            RefreshTokenTtl = 5,
            RefreshTokenEim = 5,
            UseRsa = false,
            MultiTenantEnabled = false
        };
        return (info, new MAuthenticateTokenHelper<CornerPerm>(info, new HmacTokenSigner(info.SymmetricSecretKey)));
    }

    private static PermissionService<CornerPerm, TestDbContext> CreateService(TestDbContext db,
        MAuthenticateInfoContext ctx)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        return new PermissionService<CornerPerm, TestDbContext>(db, ctx);
    }

    [Fact]
    public async Task GetMenuMetadata_Multiple_Roles_Groupless_Permission()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("menu_metadata").Options;
        using TestDbContext db = new(options);

        MUser user = new()
        {
            UserName = "u",
            EmailAddress = "u@a.com",
            Name = "n",
            Surname = "s",
            Password = "p"
        };
        MRole role1 = new()
        {
            Name = "r1",
            DisplayName = "r1",
            NormalizedName = "R1"
        };
        MRole role2 = new()
        {
            Name = "r2",
            DisplayName = "r2",
            NormalizedName = "R2"
        };
        MPermissionGroup group = new()
        {
            Name = "group1",
            DisplayName = "group1"
        };
        MPermission perm1 = new()
        {
            Name = CornerPerm.View.ToString(),
            PermissionGroup = group
        };
        MPermission perm2 = new()
        {
            Name = CornerPerm.Edit.ToString()
        };

        await db.Users.AddAsync(user);
        await db.Roles.AddRangeAsync(role1, role2);
        await db.PermissionGroups.AddAsync(group);
        await db.Permissions.AddRangeAsync(perm1, perm2);
        await db.SaveChangesAsync();
        MUserRole role = new()
        {
            UserId = user.EntityId,
            RoleId = role1.EntityId
        };
        await db.UserRoles.AddRangeAsync(
            role,
            new MUserRole { UserId = user.EntityId, RoleId = role2.EntityId });
        MRolePermission entity = new()
        {
            RoleId = role1.EntityId,
            PermissionId = perm1.EntityId
        };
        await db.RolePermissions.AddRangeAsync(
            entity,
            new MRolePermission { RoleId = role2.EntityId, PermissionId = perm2.EntityId });
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        _ = CreateTokenHelper();
        PermissionService<CornerPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<List<MenuMetadata>> result = await svc.GetMenuMetadataAsync(user.EntityId, CancellationToken.None);
        Assert.NotNull(result.Result);

        Assert.Equal(2, result.Result?.Count);

        MenuMetadata g1 = Assert.Single(result.Result!, m => m.GroupName == "group1");
        Assert.Single(g1.Actions);
        Assert.Equal(CornerPerm.View.ToString(), g1.Actions[0].Name);
        Assert.True(g1.Actions[0].IsGranted);

        MenuMetadata none = Assert.Single(result.Result!, m => m.GroupName == string.Empty);
        Assert.Single(none.Actions);
        Assert.Equal(CornerPerm.Edit.ToString(), none.Actions[0].Name);
        Assert.True(none.Actions[0].IsGranted);
    }

    [Fact]
    public async Task GetPermissionDefinitions_Groupless_Permission()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("permission_defs").Options;
        using TestDbContext db = new(options);

        MPermissionGroup group = new()
        {
            Name = "group1",
            DisplayName = "group1"
        };
        MPermission perm1 = new()
        {
            Name = CornerPerm.View.ToString(),
            PermissionGroup = group
        };
        MPermission perm2 = new()
        {
            Name = CornerPerm.Edit.ToString()
        };

        await db.PermissionGroups.AddAsync(group);
        await db.Permissions.AddRangeAsync(perm1, perm2);
        await db.SaveChangesAsync();

        MAuthenticateInfoContext ctx = new(false)
        {
            Language = "en"
        };
        _ = CreateTokenHelper();
        PermissionService<CornerPerm, TestDbContext> svc = CreateService(db, ctx);

        MResponse<List<PermissionDefinition>> result = await svc.GetPermissionDefinitionsAsync(CancellationToken.None);
        Assert.NotNull(result.Result);

        Assert.Equal(2, result.Result?.Count);

        PermissionDefinition p1 = Assert.Single(result.Result!, d => d.Name == CornerPerm.View.ToString());
        Assert.Equal("group1", p1.GroupName);

        PermissionDefinition p2 = Assert.Single(result.Result!, d => d.Name == CornerPerm.Edit.ToString());
        Assert.Equal(string.Empty, p2.GroupName);
    }
}
