using Muonroi.Core.Abstractions.Models;
using Muonroi.Data.EntityFrameworkCore.Entity.Identity;
using Xunit;

namespace Muonroi.AspNetCore.Tests.Permissions;

public class MPermissionEntityTests
{
    [Fact]
    public void Type_Get_Returns_Set_Value()
    {
        MPermission perm = new();
        Assert.Equal(PermissionType.Menu, perm.Type);

        perm.Type = PermissionType.Action;
        Assert.Equal(PermissionType.Action, perm.Type);
    }

    [Fact]
    public void ParentUiKey_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.ParentUiKey);

        perm.ParentUiKey = "parent";
        Assert.Equal("parent", perm.ParentUiKey);
    }

    [Fact]
    public void Label_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.Label);

        perm.Label = "label";
        Assert.Equal("label", perm.Label);
    }

    [Fact]
    public void Icon_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.Icon);

        perm.Icon = "icon";
        Assert.Equal("icon", perm.Icon);
    }

    [Fact]
    public void Order_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.Order);

        perm.Order = 5;
        Assert.Equal(5, perm.Order);
    }

    [Fact]
    public void Description_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.Description);

        perm.Description = "desc";
        Assert.Equal("desc", perm.Description);
    }

    [Fact]
    public void ParentId_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.ParentId);

        Guid id = Guid.NewGuid();
        perm.ParentId = id;
        Assert.Equal(id, perm.ParentId);
    }

    [Fact]
    public void Parent_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.Parent);

        MPermission parent = new()
        {
            Name = "p",
            UiKey = "p"
        };
        perm.Parent = parent;
        Assert.Same(parent, perm.Parent);
    }

    [Fact]
    public void Children_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.Children);

        MPermission permission = new()
        {
            Name = "c",
            UiKey = "c"
        };
        List<MPermission> list = [permission];
        perm.Children = list;
        Assert.Same(list, perm.Children);
    }

    [Fact]
    public void PermissionGroupId_Get_Set_Works()
    {
        MPermission perm = new();
        Assert.Null(perm.PermissionGroupId);

        Guid id = Guid.NewGuid();
        perm.PermissionGroupId = id;
        Assert.Equal(id, perm.PermissionGroupId);
    }
}
