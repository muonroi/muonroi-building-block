namespace Muonroi.BuildingBlock.Test;

public class MUserWithPermissionsTests
{
    [Fact]
    public void Permissions_Property_Returns_List_And_Allows_Null()
    {
        MUserWithPermissions user = new();
        Assert.NotNull(user.Permissions);
        Assert.Empty(user.Permissions);
        MPermission permission = new()
        {
            Name = "1"
        };
        List<MPermission> list = [permission];
        user.Permissions = list;
        Assert.Same(list, user.Permissions);
        user.Permissions = null!;
        Assert.Null(user.Permissions);
    }

    [Fact]
    public void HasPermission_Returns_True_When_User_Has_Permission()
    {
        MUserWithPermissions user = new();
        MPermission permission = new()
        {
            Name = ((int)TestPerm.One).ToString()
        };
        user.Permissions = [permission];
        bool result = user.HasPermission(TestPerm.One);
        Assert.True(result);
    }

    [Fact]
    public void HasPermission_Returns_False_When_Permission_Not_Found()
    {
        MUserWithPermissions user = new();
        MPermission permission = new()
        {
            Name = ((int)TestPerm.One).ToString()
        };
        user.Permissions = [permission];
        bool result = user.HasPermission(TestPerm.Read);
        Assert.False(result);
    }

    [Fact]
    public void HasPermission_Returns_False_When_List_Empty()
    {
        MUserWithPermissions user = new();
        bool result = user.HasPermission(TestPerm.One);
        Assert.False(result);
    }
}
