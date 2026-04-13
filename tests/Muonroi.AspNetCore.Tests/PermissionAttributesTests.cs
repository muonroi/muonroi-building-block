namespace Muonroi.AspNetCore.Tests;

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
        Action action = () => new PermissionAttribute<SamplePermission>((SamplePermission)999);

        action.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void AuthorizePermissionAttribute_Throws_On_Empty()
    {
        Action action = () => new AuthorizePermissionAttribute("");

        action.Should().Throw<InvalidPermissionException>();
    }

    [Fact]
    public void PermissionAttribute_Stores_MatchMode()
    {
        PermissionAttribute<SamplePermission> attribute = new(SamplePermission.Read, PermissionMatchMode.All);

        attribute.MatchMode.Should().Be(PermissionMatchMode.All);
        attribute.RequiredPermission.Should().Be(SamplePermission.Read);
    }

    [Fact]
    public void AuthorizePermissionAttribute_Stores_Default_All_MatchMode()
    {
        AuthorizePermissionAttribute attribute = new("orders.view");

        attribute.MatchMode.Should().Be(PermissionMatchMode.All);
        attribute.PermissionKey.Should().Be("orders.view");
    }

    [Fact]
    public void AuthorizePermissionAttribute_Throws_On_Invalid_MatchMode()
    {
        Action action = () => new AuthorizePermissionAttribute("orders.view", (PermissionMatchMode)999);

        action.Should().Throw<InvalidPermissionException>();
    }
}
