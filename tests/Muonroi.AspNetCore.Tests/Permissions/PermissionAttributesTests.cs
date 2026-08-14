namespace Muonroi.AspNetCore.Tests.Permissions;

public enum SamplePermission
{
    Read = 1,
    Write = 2
}

public class PermissionAttributesTests
{
    [Fact]
    public void PermissionAttribute_Throws_On_Invalid_Value()
    {
        _ = Assert.Throws<InvalidPermissionException>(() => new PermissionAttribute<SamplePermission>((SamplePermission)999));
    }

    [Fact]
    public void AuthorizePermissionAttribute_Throws_On_Empty()
    {
        _ = Assert.Throws<InvalidPermissionException>(() => new AuthorizePermissionAttribute(""));
    }

    [Fact]
    public void PermissionAttribute_Stores_MatchMode()
    {
        PermissionAttribute<SamplePermission> attribute = new(SamplePermission.Read, PermissionMatchMode.All);
        Assert.Equal(PermissionMatchMode.All, attribute.MatchMode);
    }

    [Fact]
    public void AuthorizePermissionAttribute_Stores_Default_All_MatchMode()
    {
        AuthorizePermissionAttribute attribute = new("orders.view");
        Assert.Equal(PermissionMatchMode.All, attribute.MatchMode);
    }

    [Fact]
    public void AuthorizePermissionAttribute_Throws_On_Invalid_MatchMode()
    {
        _ = Assert.Throws<InvalidPermissionException>(() =>
            new AuthorizePermissionAttribute("orders.view", (PermissionMatchMode)999));
    }
}
