namespace Muonroi.BuildingBlock.Test;

public class PermissionServiceBuildTreeTests
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
    public void BuildPermissionTree_Empty_ReturnsEmptyTree()
    {
        PermissionTree tree = InvokeBuildTree([]);
        Assert.Empty(tree.Menus);
    }

    [Fact]
    public void BuildPermissionTree_SingleMenu()
    {
        PermissionTree tree = InvokeBuildTree(["Orders"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        Assert.Equal("Orders", menu.Key);
        Assert.False(menu.CanView);
        Assert.Empty(menu.Actions);
    }

    [Fact]
    public void BuildPermissionTree_ComplexHierarchy()
    {
        PermissionTree tree = InvokeBuildTree([
            "Orders.View",
            "Orders.Create",
            "Orders.Delete",
            "Orders.Tab.Details.View",
            "Orders.Field.Price.View",
            "Orders.Field.Price.Edit",
            "Reports.View"
        ]);

        Assert.Equal(2, tree.Menus.Count);
        MenuPermission orders = tree.Menus.First(m => m.Key == "Orders");
        Assert.True(orders.CanView);
        Assert.Contains(orders.Actions, a => a is { Key: "create", CanExec: true });
        Assert.Contains(orders.Actions, a => a is { Key: "delete", CanExec: true });
        TabPermission tab = Assert.Single(orders.Tabs);
        Assert.Equal("Details", tab.Key);
        Assert.True(tab.CanView);
        FieldPermission field = Assert.Single(orders.Fields);
        Assert.Equal("Price", field.Key);
        Assert.True(field.CanView);
        Assert.True(field.CanEdit);
    }

    [Fact]
    public void BuildPermissionTree_DuplicateEntries_NoDuplicates()
    {
        PermissionTree tree = InvokeBuildTree([
            "Orders.View",
            "Orders.View",
            "Orders.Create",
            "Orders.Create"
        ]);

        MenuPermission menu = Assert.Single(tree.Menus);
        Assert.True(menu.CanView);
        ActionPermission action = Assert.Single(menu.Actions);
        Assert.Equal("create", action.Key);
    }

    [Fact]
    public void BuildPermissionTree_InvalidEntries_Ignored()
    {
        PermissionTree tree = InvokeBuildTree(["", ".", "Orders.View"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        Assert.Equal("Orders", menu.Key);
    }

    [Fact]
    public void BuildPermissionTree_NullEntry_Throws()
    {
        string?[] names = ["Orders.View", null];
        Type svcType = typeof(PermissionService<TestPerm, TestDbContext>);
        MethodInfo? mi = svcType.GetMethod("BuildPermissionTree", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mi);
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() => mi!.Invoke(null, [names]));
        Assert.IsType<NullReferenceException>(ex.InnerException);
    }

    [Fact]
    public void BuildPermissionTree_NullList_Throws()
    {
        Type svcType = typeof(PermissionService<TestPerm, TestDbContext>);
        MethodInfo? mi = svcType.GetMethod("BuildPermissionTree", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mi);
        TargetInvocationException ex = Assert.Throws<TargetInvocationException>(() => mi!.Invoke(null, [null]));
        Assert.IsType<NullReferenceException>(ex.InnerException);
    }

    [Fact]
    public void BuildPermissionTree_LargeInput_Performance()
    {
        List<string> names = [];
        for (int i = 0; i < 10000; i++) names.Add($"Menu{i % 10}.Action{i}");

        Stopwatch sw = Stopwatch.StartNew();
        PermissionTree tree = InvokeBuildTree(names);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 1500);
        Assert.True(tree.Menus.Count <= 10);
    }

    [Fact]
    public void Constructor_Allows_Null_Dependencies()
    {
        PermissionService<TestPerm, TestDbContext> svc = new(null!, null!);
        Assert.NotNull(svc);
    }

    [Fact]
    public void Constructor_With_Valid_Dependencies()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("perm_ctor").Options;
        using TestDbContext db = new(options);
        MAuthenticateInfoContext ctx = new(false);
        PermissionService<TestPerm, TestDbContext> svc = new(db, ctx);
        Assert.NotNull(svc);
    }
}
