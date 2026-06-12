using Microsoft.EntityFrameworkCore;

namespace Muonroi.BuildingBlock.Test;

public class PermissionServiceBranchCoverageTests
{
    private static PermissionService<TestPerm, TestDbContext> CreateService(TestDbContext db)
    {
        TenantContext.CurrentTenantId ??= Guid.NewGuid().ToString();
        MAuthenticateInfoContext ctx = new(false)
        {
            CurrentUserGuid = Guid.NewGuid().ToString(),
            Language = "en"
        };
        return new PermissionService<TestPerm, TestDbContext>(db, ctx);
    }

    private static PermissionTree InvokeBuildTree(IEnumerable<string> names)
    {
        Type svcType = typeof(PermissionService<TestPerm, TestDbContext>);
        MethodInfo? mi = svcType.GetMethod("BuildPermissionTree", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mi);
        object? result = mi!.Invoke(null, [names]);
        Assert.NotNull(result);
        return (PermissionTree)result!;
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_RoleNotFound_ReturnsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_role_nf_single").Options;
        using TestDbContext db = new(options);
        MPermission perm = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Permissions.Add(perm);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);
        AssignPermissionRequestModel req = new()
        {
            RoleId = Guid.NewGuid(),
            PermissionId = perm.EntityId
        };

        MResponse<object> result = await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task AssignPermissionToRoleAsync_PermissionNotFound_ReturnsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("assign_perm_nf_single").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        db.Roles.Add(role);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);
        AssignPermissionRequestModel req = new()
        {
            RoleId = role.EntityId,
            PermissionId = Guid.NewGuid()
        };

        MResponse<object> result = await svc.AssignPermissionToRoleAsync(req, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task RemovePermissionFromRoleAsync_RoleNotFound_ReturnsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("remove_perm_role_nf").Options;
        using TestDbContext db = new(options);
        MPermission perm = new()
        {
            Name = TestPerm.Read.ToString()
        };
        db.Permissions.Add(perm);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<object> result = await svc.RemovePermissionFromRoleAsync(Guid.NewGuid(), perm.EntityId, CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task RemovePermissionFromRoleAsync_PermissionNotFound_ReturnsError()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("remove_perm_perm_nf").Options;
        using TestDbContext db = new(options);
        MRole role = new()
        {
            Name = "r",
            DisplayName = "r",
            NormalizedName = "R"
        };
        db.Roles.Add(role);
        db.SaveChanges();
        PermissionService<TestPerm, TestDbContext> svc = CreateService(db);

        MResponse<object> result = await svc.RemovePermissionFromRoleAsync(role.EntityId, Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public void BuildPermissionTree_FlatPermissions_BuildsMenus()
    {
        PermissionTree tree = InvokeBuildTree(["Orders.Create", "Reports.Export"]);
        Assert.Equal(2, tree.Menus.Count);
        Assert.Contains(tree.Menus, m => m.Key == "Orders" && m.Actions.Any(a => a.Key == "create"));
        Assert.Contains(tree.Menus, m => m.Key == "Reports" && m.Actions.Any(a => a.Key == "export"));
    }

    [Fact]
    public void BuildPermissionTree_NoParent_CreatesMenu()
    {
        PermissionTree tree = InvokeBuildTree(["Settings.Field.Theme.Edit"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        FieldPermission field = Assert.Single(menu.Fields);
        Assert.Equal("Theme", field.Key);
        Assert.True(field.CanEdit);
    }

    [Fact]
    public void BuildPermissionTree_ExtraDepth_IgnoresSegments()
    {
        PermissionTree tree = InvokeBuildTree(["Menu.Tab.Settings.Advanced.View"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        TabPermission tab = Assert.Single(menu.Tabs);
        Assert.Equal("Settings", tab.Key);
        Assert.False(tab.CanView);
    }

    [Fact]
    public void BuildPermissionTree_CaseSensitive_DuplicatesSeparated()
    {
        PermissionTree tree = InvokeBuildTree(["Orders.View", "orders.view"]);
        Assert.Equal(2, tree.Menus.Count);
    }
}
