namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceBuildTreeAdditionalTests
{
    private static PermissionTree InvokeBuildTree(IEnumerable<string?>? names)
    {
        Type svcType = typeof(PermissionService<TestPerm, TestDbContext>);
        MethodInfo? mi = svcType.GetMethod("BuildPermissionTree", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mi);

        IEnumerable<string> nonNullNames = names?.Where(n => n != null).Cast<string>() ?? [];

        object? result = mi!.Invoke(null, [nonNullNames]);
        Assert.NotNull(result);

        return (PermissionTree)result!;
    }

    [Fact]
    public void BuildPermissionTree_ValidPermissions_BuildsTree()
    {
        PermissionTree tree = InvokeBuildTree(["Users.View", "Users.Create"]);

        MenuPermission menu = Assert.Single(tree.Menus);
        Assert.Equal("Users", menu.Key);
        Assert.True(menu.CanView);
        ActionPermission action = Assert.Single(menu.Actions);
        Assert.Equal("create", action.Key);
        Assert.True(action.CanExec);
    }

    [Fact]
    public void BuildPermissionTree_EmptyList_ReturnsEmptyTree()
    {
        PermissionTree tree = InvokeBuildTree([]);
        Assert.Empty(tree.Menus);
    }

    [Fact]
    public void BuildPermissionTree_ComplexHierarchy_BuildsCorrectStructure()
    {
        PermissionTree tree = InvokeBuildTree([
            "Orders.View",
            "Orders.Tab.Detail.View",
            "Orders.Field.Price.View",
            "Orders.Field.Price.Edit",
            "Reports.View"
        ]);

        Assert.Equal(2, tree.Menus.Count);
        MenuPermission orders = tree.Menus.First(m => m.Key == "Orders");
        Assert.True(orders.CanView);
        TabPermission tab = Assert.Single(orders.Tabs);
        Assert.Equal("Detail", tab.Key);
        Assert.True(tab.CanView);
        FieldPermission field = Assert.Single(orders.Fields);
        Assert.Equal("Price", field.Key);
        Assert.True(field.CanView);
        Assert.True(field.CanEdit);
    }

    [Fact]
    public void BuildPermissionTree_DuplicateNestedEntries_Ignored()
    {
        PermissionTree tree = InvokeBuildTree([
            "Orders.View",
            "Orders.Tab.Detail.View",
            "Orders.Tab.Detail.View",
            "Orders.Field.Price.View",
            "Orders.Field.Price.View",
            "Orders.Field.Price.Edit",
            "Orders.Field.Price.Edit"
        ]);

        MenuPermission menu = Assert.Single(tree.Menus);
        Assert.True(menu.CanView);
        TabPermission tab = Assert.Single(menu.Tabs);
        Assert.Equal("Detail", tab.Key);
        FieldPermission field = Assert.Single(menu.Fields);
        Assert.Equal("Price", field.Key);
        Assert.True(field.CanView);
        Assert.True(field.CanEdit);
    }
}
