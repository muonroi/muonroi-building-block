using System.Reflection;
using Muonroi.AspNetCore.Services;
using Muonroi.AspNetCore.Tests.Helpers;
using Muonroi.Core.Abstractions.Models;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceBuildTreeEdgeTests
{
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
    public void BuildPermissionTree_TabWithoutView_TreatedAsAction()
    {
        PermissionTree tree = InvokeBuildTree(["Orders.Tab"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        Assert.Empty(menu.Tabs);
        ActionPermission action = Assert.Single(menu.Actions);
        Assert.Equal("tab", action.Key);
        Assert.True(action.CanExec);
    }

    [Fact]
    public void BuildPermissionTree_FieldWithoutAction_NoFlags()
    {
        PermissionTree tree = InvokeBuildTree(["Orders.Field.Price"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        FieldPermission field = Assert.Single(menu.Fields);
        Assert.Equal("Price", field.Key);
        Assert.False(field.CanView);
        Assert.False(field.CanEdit);
    }

    [Fact]
    public void BuildPermissionTree_ViewWithExtraSegment_IgnoresExtra()
    {
        PermissionTree tree = InvokeBuildTree(["Orders.View.Delete"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        Assert.True(menu.CanView);
        Assert.Empty(menu.Actions);
    }

    [Fact]
    public void BuildPermissionTree_UnknownAction_AddsAction()
    {
        PermissionTree tree = InvokeBuildTree(["Orders.Export"]);
        MenuPermission menu = Assert.Single(tree.Menus);
        ActionPermission action = Assert.Single(menu.Actions);
        Assert.Equal("export", action.Key);
        Assert.True(action.CanExec);
    }
}
