namespace Muonroi.AspNetCore.Tests.Attributes;

public class GenericCrudPermissionAttributeTests
{
    [Fact]
    public void Properties_CanBeSet()
    {
        var attr = new GenericCrudPermissionAttribute
        {
            ReadPermission = "read",
            CreatePermission = "create",
            UpdatePermission = "update",
            DeletePermission = "delete",
            PermissionPrefix = "prefix",
            SkipPermissionCheck = true
        };

        Assert.Equal("read", attr.ReadPermission);
        Assert.Equal("create", attr.CreatePermission);
        Assert.Equal("update", attr.UpdatePermission);
        Assert.Equal("delete", attr.DeletePermission);
        Assert.Equal("prefix", attr.PermissionPrefix);
        Assert.True(attr.SkipPermissionCheck);
    }
}
